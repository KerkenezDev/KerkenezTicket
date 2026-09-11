using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using KerkenezTicket.Models;

namespace KerkenezTicket.Services
{
    public class TicketDatabaseService
    {
        private readonly string _connectionString;
        private static readonly object _dbLock = new object();

        public TicketDatabaseService(string? databasePath = null)
        {
            string dbPath = databasePath ?? ConfigService.DatabaseFilePath;
            string? dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                DefaultTimeout = 5
            }.ToString();

            InitializeDatabase();
        }

        private SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000;";
            cmd.ExecuteNonQuery();

            return conn;
        }

        public void InitializeDatabase()
        {
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS tickets (
                        id TEXT PRIMARY KEY,
                        ticket_number INTEGER UNIQUE,
                        app TEXT NOT NULL,
                        ticket_type TEXT NOT NULL,
                        status TEXT NOT NULL,
                        priority TEXT NOT NULL,
                        title_enc TEXT NOT NULL,
                        description_enc TEXT,
                        tags_enc TEXT,
                        notes_enc TEXT,
                        created_at TEXT NOT NULL,
                        updated_at TEXT NOT NULL,
                        completed_at TEXT,
                        killed_at TEXT
                    );

                    CREATE INDEX IF NOT EXISTS idx_tickets_app ON tickets(app);
                    CREATE INDEX IF NOT EXISTS idx_tickets_status ON tickets(status);
                    CREATE INDEX IF NOT EXISTS idx_tickets_type ON tickets(ticket_type);
                    CREATE INDEX IF NOT EXISTS idx_tickets_priority ON tickets(priority);

                    CREATE TABLE IF NOT EXISTS app_categories (
                        name TEXT PRIMARY KEY,
                        display_name TEXT NOT NULL,
                        color_hex TEXT,
                        description TEXT
                    );

                    CREATE TABLE IF NOT EXISTS ticket_types (
                        name TEXT PRIMARY KEY,
                        color_hex TEXT,
                        icon TEXT
                    );
                ";
                cmd.ExecuteNonQuery();

                // Clean out any unreferenced default apps that have no tickets
                CleanUnreferencedDefaultApps(conn);

                // Ensure minimal clean defaults for apps and types
                EnsureDefaultSeedData(conn);
            }
        }

        private static void EnsureDefaultSeedData(SqliteConnection conn)
        {
            try
            {
                // Solo developer tool: No hardcoded default proprietary apps
                // Users create their own tracked applications and projects in the Apps & Types menu

                using var cmdCountTypes = conn.CreateCommand();
                cmdCountTypes.CommandText = "SELECT COUNT(*) FROM ticket_types;";
                long typeCount = Convert.ToInt64(cmdCountTypes.ExecuteScalar() ?? 0);
                if (typeCount == 0)
                {
                    using var cmdSeedType = conn.CreateCommand();
                    cmdSeedType.CommandText = @"
                        INSERT INTO ticket_types (name, color_hex) VALUES
                        ('Bug', '#D9534F'),
                        ('Feature', '#0275D8'),
                        ('Sync', '#5BC0DE'),
                        ('Task', '#F0AD4E'),
                        ('Improvement', '#5CB85C');
                    ";
                    cmdSeedType.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private static void CleanUnreferencedDefaultApps(SqliteConnection conn)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM app_categories 
                    WHERE name IN ('mail', 'ticket', 'calendar', 'news', 'voice', 'razer');
                ";
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public int GetNextTicketNumber()
        {
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COALESCE(MAX(ticket_number), 0) + 1 FROM tickets;";
                return Convert.ToInt32(cmd.ExecuteScalar() ?? 1);
            }
        }

        public TicketItem AddTicket(TicketItem ticket)
        {
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var transaction = conn.BeginTransaction();

                if (ticket.TicketNumber <= 0)
                {
                    using var nextCmd = conn.CreateCommand();
                    nextCmd.Transaction = transaction;
                    nextCmd.CommandText = "SELECT COALESCE(MAX(ticket_number), 0) + 1 FROM tickets;";
                    ticket.TicketNumber = Convert.ToInt32(nextCmd.ExecuteScalar() ?? 1);
                }

                if (string.IsNullOrWhiteSpace(ticket.Id))
                {
                    ticket.Id = $"KT-{ticket.TicketNumber}";
                }

                ticket.CreatedAt = DateTime.UtcNow;
                ticket.UpdatedAt = DateTime.UtcNow;

                // Encrypt sensitive fields using DPAPI
                string titleEnc = TicketCryptoService.EncryptString(ticket.Title);
                string descEnc = TicketCryptoService.EncryptString(ticket.Description);
                string tagsJson = JsonSerializer.Serialize(ticket.Tags ?? new List<string>());
                string tagsEnc = TicketCryptoService.EncryptString(tagsJson);
                string notesEnc = TicketCryptoService.EncryptString(ticket.Notes);

                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO tickets (
                        id, ticket_number, app, ticket_type, status, priority,
                        title_enc, description_enc, tags_enc, notes_enc,
                        created_at, updated_at, completed_at, killed_at
                    ) VALUES (
                        @id, @num, @app, @type, @status, @priority,
                        @title_enc, @desc_enc, @tags_enc, @notes_enc,
                        @created, @updated, @completed, @killed
                    );
                ";

                cmd.Parameters.AddWithValue("@id", ticket.Id);
                cmd.Parameters.AddWithValue("@num", ticket.TicketNumber);
                cmd.Parameters.AddWithValue("@app", (ticket.App ?? "general").ToLowerInvariant());
                cmd.Parameters.AddWithValue("@type", ticket.TicketType ?? "Bug");
                cmd.Parameters.AddWithValue("@status", ticket.Status.ToKey());
                cmd.Parameters.AddWithValue("@priority", ticket.Priority.ToKey());
                cmd.Parameters.AddWithValue("@title_enc", titleEnc);
                cmd.Parameters.AddWithValue("@desc_enc", descEnc);
                cmd.Parameters.AddWithValue("@tags_enc", tagsEnc);
                cmd.Parameters.AddWithValue("@notes_enc", notesEnc);
                cmd.Parameters.AddWithValue("@created", ticket.CreatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@updated", ticket.UpdatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@completed", ticket.CompletedAt?.ToString("o") ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@killed", ticket.KilledAt?.ToString("o") ?? (object)DBNull.Value);

                cmd.ExecuteNonQuery();
                transaction.Commit();

                return ticket;
            }
        }

        public bool UpdateTicket(TicketItem ticket)
        {
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                ticket.UpdatedAt = DateTime.UtcNow;

                if (ticket.Status == TicketStatus.Done && ticket.CompletedAt == null)
                {
                    ticket.CompletedAt = DateTime.UtcNow;
                }
                else if (ticket.Status != TicketStatus.Done)
                {
                    ticket.CompletedAt = null;
                }

                if (ticket.Status == TicketStatus.Killed && ticket.KilledAt == null)
                {
                    ticket.KilledAt = DateTime.UtcNow;
                }
                else if (ticket.Status != TicketStatus.Killed)
                {
                    ticket.KilledAt = null;
                }

                string titleEnc = TicketCryptoService.EncryptString(ticket.Title);
                string descEnc = TicketCryptoService.EncryptString(ticket.Description);
                string tagsJson = JsonSerializer.Serialize(ticket.Tags ?? new List<string>());
                string tagsEnc = TicketCryptoService.EncryptString(tagsJson);
                string notesEnc = TicketCryptoService.EncryptString(ticket.Notes);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE tickets SET
                        app = @app,
                        ticket_type = @type,
                        status = @status,
                        priority = @priority,
                        title_enc = @title_enc,
                        description_enc = @desc_enc,
                        tags_enc = @tags_enc,
                        notes_enc = @notes_enc,
                        updated_at = @updated,
                        completed_at = @completed,
                        killed_at = @killed
                    WHERE id = @id;
                ";

                cmd.Parameters.AddWithValue("@id", ticket.Id);
                cmd.Parameters.AddWithValue("@app", (ticket.App ?? "general").ToLowerInvariant());
                cmd.Parameters.AddWithValue("@type", ticket.TicketType ?? "Bug");
                cmd.Parameters.AddWithValue("@status", ticket.Status.ToKey());
                cmd.Parameters.AddWithValue("@priority", ticket.Priority.ToKey());
                cmd.Parameters.AddWithValue("@title_enc", titleEnc);
                cmd.Parameters.AddWithValue("@desc_enc", descEnc);
                cmd.Parameters.AddWithValue("@tags_enc", tagsEnc);
                cmd.Parameters.AddWithValue("@notes_enc", notesEnc);
                cmd.Parameters.AddWithValue("@updated", ticket.UpdatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@completed", ticket.CompletedAt?.ToString("o") ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@killed", ticket.KilledAt?.ToString("o") ?? (object)DBNull.Value);

                int rows = cmd.ExecuteNonQuery();
                return rows > 0;
            }
        }

        public bool UpdateTicketStatus(string idOrNumber, TicketStatus newStatus)
        {
            var ticket = GetTicketById(idOrNumber);
            if (ticket == null) return false;

            ticket.Status = newStatus;
            return UpdateTicket(ticket);
        }

        public bool DeleteTicket(string id)
        {
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM tickets WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public TicketItem? GetTicketById(string idOrNumber)
        {
            if (string.IsNullOrWhiteSpace(idOrNumber)) return null;

            string normalized = idOrNumber.Trim();
            int parsedNum = 0;
            if (normalized.StartsWith("KT-", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(normalized.Substring(3), out parsedNum);
            }
            else
            {
                int.TryParse(normalized, out parsedNum);
            }

            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, ticket_number, app, ticket_type, status, priority,
                           title_enc, description_enc, tags_enc, notes_enc,
                           created_at, updated_at, completed_at, killed_at
                    FROM tickets
                    WHERE id = @id OR (@num > 0 AND ticket_number = @num)
                    LIMIT 1;
                ";
                cmd.Parameters.AddWithValue("@id", normalized);
                cmd.Parameters.AddWithValue("@num", parsedNum);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return MapTicketFromReader(reader);
                }
            }

            return null;
        }

        public List<TicketItem> GetAllTickets()
        {
            lock (_dbLock)
            {
                var list = new List<TicketItem>();
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, ticket_number, app, ticket_type, status, priority,
                           title_enc, description_enc, tags_enc, notes_enc,
                           created_at, updated_at, completed_at, killed_at
                    FROM tickets
                    ORDER BY ticket_number DESC;
                ";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapTicketFromReader(reader));
                }

                return list;
            }
        }

        public List<TicketItem> GetFilteredTickets(string? app = null, string? type = null, TicketStatus? status = null, string? search = null)
        {
            var all = GetAllTickets();

            IEnumerable<TicketItem> query = all;

            if (!string.IsNullOrWhiteSpace(app) && !app.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => string.Equals(t.App, app.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(type) && !type.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(t => string.Equals(t.TicketType, type.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(t =>
                    t.FormattedId.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    t.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (t.Tags != null && t.Tags.Any(tag => tag.Contains(s, StringComparison.OrdinalIgnoreCase)))
                );
            }

            return query.ToList();
        }

        private static TicketItem MapTicketFromReader(SqliteDataReader reader)
        {
            var item = new TicketItem
            {
                Id = reader.GetString(0),
                TicketNumber = reader.GetInt32(1),
                App = reader.GetString(2),
                TicketType = reader.GetString(3),
                Status = TicketStatusExtensions.ParseStatus(reader.GetString(4)),
                Priority = TicketPriorityExtensions.ParsePriority(reader.GetString(5))
            };

            string titleEnc = reader.IsDBNull(6) ? "" : reader.GetString(6);
            string descEnc = reader.IsDBNull(7) ? "" : reader.GetString(7);
            string tagsEnc = reader.IsDBNull(8) ? "" : reader.GetString(8);
            string notesEnc = reader.IsDBNull(9) ? "" : reader.GetString(9);

            // Decrypt DPAPI protected fields
            item.Title = TicketCryptoService.DecryptString(titleEnc);
            item.Description = TicketCryptoService.DecryptString(descEnc);
            item.Notes = TicketCryptoService.DecryptString(notesEnc);

            string tagsJson = TicketCryptoService.DecryptString(tagsEnc);
            if (!string.IsNullOrWhiteSpace(tagsJson))
            {
                try
                {
                    item.Tags = JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
                }
                catch
                {
                    item.Tags = new List<string>();
                }
            }

            if (DateTime.TryParse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dtCreated))
            {
                item.CreatedAt = dtCreated;
            }

            if (DateTime.TryParse(reader.GetString(11), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dtUpdated))
            {
                item.UpdatedAt = dtUpdated;
            }

            if (!reader.IsDBNull(12) && DateTime.TryParse(reader.GetString(12), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dtCompleted))
            {
                item.CompletedAt = dtCompleted;
            }

            if (!reader.IsDBNull(13) && DateTime.TryParse(reader.GetString(13), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dtKilled))
            {
                item.KilledAt = dtKilled;
            }

            return item;
        }

        public List<AppCategory> GetAppCategories()
        {
            lock (_dbLock)
            {
                var list = new List<AppCategory>();
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT a.name, a.display_name, a.color_hex, a.description,
                           COALESCE(SUM(CASE WHEN t.status IN ('todo', 'doing') THEN 1 ELSE 0 END), 0) as open_count,
                           COUNT(t.id) as total_count
                    FROM app_categories a
                    LEFT JOIN tickets t ON lower(t.app) = lower(a.name)
                    GROUP BY a.name, a.display_name, a.color_hex, a.description
                    ORDER BY a.name ASC;
                ";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new AppCategory
                    {
                        Name = reader.GetString(0),
                        DisplayName = reader.GetString(1),
                        ColorHex = reader.IsDBNull(2) ? "#0078D7" : reader.GetString(2),
                        Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        OpenCount = Convert.ToInt32(reader.GetInt64(4)),
                        TotalCount = Convert.ToInt32(reader.GetInt64(5))
                    });
                }

                return list;
            }
        }

        public bool AddAppCategory(string name, string displayName, string colorHex, string description)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO app_categories (name, display_name, color_hex, description)
                    VALUES (@name, @display, @color, @desc);
                ";
                cmd.Parameters.AddWithValue("@name", name.Trim().ToLowerInvariant());
                cmd.Parameters.AddWithValue("@display", string.IsNullOrWhiteSpace(displayName) ? name.Trim() : displayName.Trim());
                cmd.Parameters.AddWithValue("@color", string.IsNullOrWhiteSpace(colorHex) ? "#0078D7" : colorHex.Trim());
                cmd.Parameters.AddWithValue("@desc", description ?? "");
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool DeleteAppCategory(string name)
        {
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM app_categories WHERE lower(name) = lower(@name);";
                cmd.Parameters.AddWithValue("@name", name.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public (int Total, int Todo, int Doing, int Done, int Killed) GetStatistics(string? appFilter = null)
        {
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();

                string sql = @"
                    SELECT
                        COUNT(*),
                        COALESCE(SUM(CASE WHEN status = 'todo' THEN 1 ELSE 0 END), 0),
                        COALESCE(SUM(CASE WHEN status = 'doing' THEN 1 ELSE 0 END), 0),
                        COALESCE(SUM(CASE WHEN status = 'done' THEN 1 ELSE 0 END), 0),
                        COALESCE(SUM(CASE WHEN status = 'killed' THEN 1 ELSE 0 END), 0)
                    FROM tickets
                ";

                if (!string.IsNullOrWhiteSpace(appFilter) && !appFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    sql += " WHERE lower(app) = lower(@app);";
                    cmd.Parameters.AddWithValue("@app", appFilter.Trim());
                }

                cmd.CommandText = sql;

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return (
                        Convert.ToInt32(reader.GetInt64(0)),
                        Convert.ToInt32(reader.GetInt64(1)),
                        Convert.ToInt32(reader.GetInt64(2)),
                        Convert.ToInt32(reader.GetInt64(3)),
                        Convert.ToInt32(reader.GetInt64(4))
                    );
                }

                return (0, 0, 0, 0, 0);
            }
        }

        public List<string> GetTicketTypes()
        {
            lock (_dbLock)
            {
                var list = new List<string>();
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT name FROM ticket_types ORDER BY name ASC;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(reader.GetString(0));
                }
                return list;
            }
        }

        public bool AddTicketType(string name, string colorHex = "#0078D7")
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO ticket_types (name, color_hex) VALUES (@name, @color);";
                cmd.Parameters.AddWithValue("@name", name.Trim());
                cmd.Parameters.AddWithValue("@color", string.IsNullOrWhiteSpace(colorHex) ? "#0078D7" : colorHex.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public List<TicketTypeCategory> GetDetailedTicketTypes()
        {
            lock (_dbLock)
            {
                var list = new List<TicketTypeCategory>();
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT y.name, y.color_hex, COUNT(t.id) as ticket_count
                    FROM ticket_types y
                    LEFT JOIN tickets t ON lower(t.ticket_type) = lower(y.name)
                    GROUP BY y.name, y.color_hex
                    ORDER BY y.name ASC;
                ";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new TicketTypeCategory
                    {
                        Name = reader.GetString(0),
                        ColorHex = reader.IsDBNull(1) ? "#0078D7" : reader.GetString(1),
                        TicketCount = Convert.ToInt32(reader.GetInt64(2))
                    });
                }
                return list;
            }
        }

        public bool DeleteTicketType(string name)
        {
            lock (_dbLock)
            {
                using var conn = CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM ticket_types WHERE lower(name) = lower(@name);";
                cmd.Parameters.AddWithValue("@name", name.Trim());
                return cmd.ExecuteNonQuery() > 0;
            }
        }
    }

    public class TicketTypeCategory
    {
        public string Name { get; set; } = "";
        public string ColorHex { get; set; } = "#0078D7";
        public int TicketCount { get; set; } = 0;
    }
}
