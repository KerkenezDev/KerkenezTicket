using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using KerkenezTicket.Models;

namespace KerkenezTicket.Services
{
    public class BackupAttachmentPayload
    {
        public string AttachmentId { get; set; } = "";
        public string TicketId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string StoredRelativePath { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Base64Data { get; set; }
    }

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
        public List<BackupAttachmentPayload> AttachmentFiles { get; set; } = new List<BackupAttachmentPayload>();
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

                var allTickets = _db.GetAllTickets();
                var package = new BackupPackage
                {
                    Format = "KerkenezTicketBackup",
                    Version = "1.0",
                    ExportedAt = DateTime.UtcNow,
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    Apps = _db.GetAppCategories(),
                    Tickets = allTickets
                };
                package.TotalTickets = package.Tickets.Count;

                // Safely embed attachments up to 25MB each
                const long maxFileSize = 25 * 1024 * 1024;
                foreach (var t in allTickets)
                {
                    if (t.Attachments != null)
                    {
                        foreach (var att in t.Attachments)
                        {
                            try
                            {
                                string fullPath = AttachmentStorageService.GetFullPath(att.StoredRelativePath);
                                string? base64 = null;
                                if (File.Exists(fullPath) && new FileInfo(fullPath).Length <= maxFileSize)
                                {
                                    base64 = Convert.ToBase64String(File.ReadAllBytes(fullPath));
                                }

                                package.AttachmentFiles.Add(new BackupAttachmentPayload
                                {
                                    AttachmentId = att.Id,
                                    TicketId = att.TicketId,
                                    FileName = att.FileName,
                                    StoredRelativePath = att.StoredRelativePath,
                                    FileSizeBytes = att.FileSizeBytes,
                                    CreatedAt = att.CreatedAt,
                                    Base64Data = base64
                                });
                            }
                            catch { }
                        }
                    }
                }

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

                    // Restore attachment records
                    if (ticket.Attachments != null)
                    {
                        foreach (var att in ticket.Attachments)
                        {
                            _db.AddAttachment(att);
                        }
                    }
                }

                // Restore attachment files if present in backup
                if (package.AttachmentFiles != null)
                {
                    foreach (var attFile in package.AttachmentFiles)
                    {
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(attFile.Base64Data))
                            {
                                string fullPath = AttachmentStorageService.GetFullPath(attFile.StoredRelativePath);
                                string? dir = Path.GetDirectoryName(fullPath);
                                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                }

                                if (!File.Exists(fullPath) || overwriteExisting)
                                {
                                    byte[] bytes = Convert.FromBase64String(attFile.Base64Data);
                                    File.WriteAllBytes(fullPath, bytes);
                                }
                            }
                        }
                        catch { }
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
