using System;
using System.Diagnostics;
using System.IO;
using KerkenezTicket.Models;

namespace KerkenezTicket.Services
{
    public static class AttachmentStorageService
    {
        public static string BaseFolder => ConfigService.AttachmentsFolder;

        public static string GetTicketFolder(string ticketId)
        {
            string safeId = SanitizeFileName(ticketId);
            string folder = Path.Combine(BaseFolder, safeId);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return folder;
        }

        public static string GetFullPath(string relativePath)
        {
            if (Path.IsPathRooted(relativePath)) return relativePath;
            return Path.Combine(BaseFolder, relativePath);
        }

        public static string GetAttachmentFullPath(TicketAttachment attachment)
        {
            return GetFullPath(attachment.StoredRelativePath);
        }

        public static TicketAttachment SaveAttachmentFile(string ticketId, string sourceFilePath, string? customFileName = null)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Source attachment file not found.", sourceFilePath);
            }

            string id = Guid.NewGuid().ToString("N");
            string origName = !string.IsNullOrWhiteSpace(customFileName) ? customFileName : Path.GetFileName(sourceFilePath);
            string safeFileName = SanitizeFileName(origName);
            string storedFileName = $"{id}_{safeFileName}";

            string ticketFolder = GetTicketFolder(ticketId);
            string destFullPath = Path.Combine(ticketFolder, storedFileName);

            File.Copy(sourceFilePath, destFullPath, true);

            var fi = new FileInfo(destFullPath);
            string relPath = Path.Combine(SanitizeFileName(ticketId), storedFileName);

            return new TicketAttachment
            {
                Id = id,
                TicketId = ticketId,
                FileName = origName,
                StoredRelativePath = relPath,
                FileSizeBytes = fi.Length,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static TicketAttachment SaveAttachmentBytes(string ticketId, byte[] data, string fileName)
        {
            string id = Guid.NewGuid().ToString("N");
            string safeFileName = SanitizeFileName(fileName);
            string storedFileName = $"{id}_{safeFileName}";

            string ticketFolder = GetTicketFolder(ticketId);
            string destFullPath = Path.Combine(ticketFolder, storedFileName);

            File.WriteAllBytes(destFullPath, data);

            string relPath = Path.Combine(SanitizeFileName(ticketId), storedFileName);

            return new TicketAttachment
            {
                Id = id,
                TicketId = ticketId,
                FileName = fileName,
                StoredRelativePath = relPath,
                FileSizeBytes = data.Length,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static TicketAttachment SaveImageBytes(string ticketId, byte[] pngBytes, string? preferredFileName = null)
        {
            string fileName = !string.IsNullOrWhiteSpace(preferredFileName)
                ? preferredFileName
                : $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            return SaveAttachmentBytes(ticketId, pngBytes, fileName);
        }

        public static bool DeleteAttachmentFile(string relativePath)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("Attachments", $"Failed to delete physical file {relativePath}: {ex.Message}");
            }
            return false;
        }

        public static void DeleteTicketAttachmentsFolder(string ticketId)
        {
            try
            {
                string safeId = SanitizeFileName(ticketId);
                string folder = Path.Combine(BaseFolder, safeId);
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("Attachments", $"Failed to delete ticket attachments folder for {ticketId}: {ex.Message}");
            }
        }

        public static bool OpenAttachment(TicketAttachment attachment)
        {
            try
            {
                string fullPath = GetFullPath(attachment.StoredRelativePath);
                if (!File.Exists(fullPath))
                {
                    return false;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                LogService.Error("Attachments", $"Error opening attachment {attachment.FileName}: {ex.Message}");
                return false;
            }
        }

        public static bool ShowInExplorer(TicketAttachment attachment)
        {
            try
            {
                string fullPath = GetFullPath(attachment.StoredRelativePath);
                if (File.Exists(fullPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                    return true;
                }
                else
                {
                    string folder = GetTicketFolder(attachment.TicketId);
                    if (Directory.Exists(folder))
                    {
                        Process.Start("explorer.exe", $"\"{folder}\"");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Attachments", $"Error revealing attachment in Explorer: {ex.Message}");
            }
            return false;
        }

        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "file";
            var invalidChars = Path.GetInvalidFileNameChars();
            var clean = string.Concat(name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(clean) ? "file" : clean.Trim();
        }
    }
}
