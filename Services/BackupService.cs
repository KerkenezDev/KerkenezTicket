using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using KerkenezTicket.Models;

namespace KerkenezTicket.Services
{
    public class BackupPackage
    {
        public string Format { get; set; } = "KerkenezTicketBackup";
        public string Version { get; set; } = "1.0";
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
        public string MachineName { get; set; } = Environment.MachineName;
        public string UserName { get; set; } = Environment.UserName;
        public int TotalTickets { get; set; }
        public List<AppCategory> Apps { get; set; } = new List<AppCategory>();
        public List<TicketItem> Tickets { get; set; } = new List<TicketItem>();
    }

    public class BackupService
    {
        private readonly TicketDatabaseService _db;
        private readonly ConfigService _config;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public BackupService(TicketDatabaseService db, ConfigService config)
        {
            _db = db;
            _config = config;
        }

        public string CreateBackup(string? destinationFilePath = null)
        {
            try
            {
                ConfigService.EnsureDirectories();

                string? backupPath = destinationFilePath;
                if (string.IsNullOrWhiteSpace(backupPath))
                {
                    string fileName = $"kticket_backup_{DateTime.Now:yyyyMMdd_HHmmss}.ktbackup";
                    string dir = !string.IsNullOrWhiteSpace(_config.Settings.BackupDirectory)
                        ? _config.Settings.BackupDirectory
                        : ConfigService.BackupsFolder;

                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    backupPath = Path.Combine(dir, fileName);
                }

                var package = new BackupPackage
                {
                    Format = "KerkenezTicketBackup",
                    Version = "1.0",
                    ExportedAt = DateTime.UtcNow,
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    Apps = _db.GetAppCategories(),
                    Tickets = _db.GetAllTickets()
                };
                package.TotalTickets = package.Tickets.Count;

                string json = JsonSerializer.Serialize(package, JsonOptions);

                string tempFile = backupPath + ".tmp";
                File.WriteAllText(tempFile, json);

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Move(tempFile, backupPath);

                return backupPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupService] Export failed: {ex.Message}");
                throw;
            }
        }

        public (int Imported, int Updated, string Message) RestoreBackup(string backupFilePath, bool overwriteExisting = false)
        {
            if (!File.Exists(backupFilePath))
            {
                return (0, 0, "Backup file does not exist.");
            }

            try
            {
                string json = File.ReadAllText(backupFilePath);
                var package = JsonSerializer.Deserialize<BackupPackage>(json, JsonOptions);

                if (package == null || package.Tickets == null)
                {
                    return (0, 0, "Invalid or corrupted backup file format.");
                }

                // Restore apps
                if (package.Apps != null)
                {
                    foreach (var app in package.Apps)
                    {
                        _db.AddAppCategory(app.Name, app.DisplayName, app.ColorHex, app.Description);
                    }
                }

                int imported = 0;
                int updated = 0;

                var existingTickets = _db.GetAllTickets().ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

                foreach (var ticket in package.Tickets)
                {
                    if (existingTickets.TryGetValue(ticket.Id, out var existing))
                    {
                        if (overwriteExisting)
                        {
                            _db.UpdateTicket(ticket);
                            updated++;
                        }
                    }
                    else
                    {
                        _db.AddTicket(ticket);
                        imported++;
                    }
                }

                return (imported, updated, $"Restored {imported} new tickets, updated {updated} existing tickets.");
            }
            catch (Exception ex)
            {
                return (0, 0, $"Restore failed: {ex.Message}");
            }
        }
    }
}
