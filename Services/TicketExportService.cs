using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using KerkenezTicket.Models;

namespace KerkenezTicket.Services
{
    public enum ExportFormat
    {
        Auto = 0,
        Zip = 1,
        Folder = 2,
        Markdown = 3
    }

    public static class TicketExportService
    {
        public static string ExportTicket(TicketItem ticket, string outputDir, ExportFormat format = ExportFormat.Auto)
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            int attCount = ticket.AttachmentCount;
            if (format == ExportFormat.Auto)
            {
                format = attCount > 0 ? ExportFormat.Zip : ExportFormat.Markdown;
            }

            string safeTitle = SanitizeForFileName(ticket.Title);
            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Ticket";
            string baseName = $"{ticket.FormattedId}_{safeTitle}";

            switch (format)
            {
                case ExportFormat.Markdown:
                    return ExportSingleMarkdownFile(ticket, outputDir, baseName);

                case ExportFormat.Folder:
                    return ExportTicketFolder(ticket, outputDir, baseName);

                case ExportFormat.Zip:
                default:
                    return ExportTicketZip(ticket, outputDir, baseName);
            }
        }

        private static string ExportSingleMarkdownFile(TicketItem ticket, string outputDir, string baseName)
        {
            string mdPath = Path.Combine(outputDir, $"{baseName}.md");
            string mdContent = GenerateTicketMarkdown(ticket, null);
            File.WriteAllText(mdPath, mdContent, Encoding.UTF8);
            return mdPath;
        }

        private static string ExportTicketFolder(TicketItem ticket, string parentDir, string folderName)
        {
            string targetFolder = Path.Combine(parentDir, folderName);
            if (Directory.Exists(targetFolder))
            {
                Directory.Delete(targetFolder, true);
            }
            Directory.CreateDirectory(targetFolder);

            string attSubfolderName = "attachments";
            string mdPath = Path.Combine(targetFolder, $"{folderName}.md");
            string mdContent = GenerateTicketMarkdown(ticket, ticket.AttachmentCount > 0 ? attSubfolderName : null);
            File.WriteAllText(mdPath, mdContent, Encoding.UTF8);

            if (ticket.Attachments != null && ticket.Attachments.Count > 0)
            {
                string attDir = Path.Combine(targetFolder, attSubfolderName);
                Directory.CreateDirectory(attDir);

                foreach (var att in ticket.Attachments)
                {
                    string sourcePath = AttachmentStorageService.GetFullPath(att.StoredRelativePath);
                    if (File.Exists(sourcePath))
                    {
                        string destFile = Path.Combine(attDir, att.FileName);
                        File.Copy(sourcePath, destFile, true);
                    }
                }
            }

            return targetFolder;
        }

        private static string ExportTicketZip(TicketItem ticket, string outputDir, string baseName)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"kt_export_{Guid.NewGuid():N}");
            try
            {
                string stageFolder = ExportTicketFolder(ticket, tempDir, baseName);

                string zipPath = Path.Combine(outputDir, $"{baseName}.zip");
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                ZipFile.CreateFromDirectory(stageFolder, zipPath, CompressionLevel.Optimal, false);
                return zipPath;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }

        public static string ExportBatchTickets(IEnumerable<TicketItem> tickets, string outputDir, ExportFormat format = ExportFormat.Zip, string? customBatchName = null)
        {
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string batchName = !string.IsNullOrWhiteSpace(customBatchName)
                ? SanitizeForFileName(customBatchName)
                : $"KerkenezTickets_Export_{DateTime.Now:yyyyMMdd_HHmmss}";

            string tempDir = Path.Combine(Path.GetTempPath(), $"kt_batch_{Guid.NewGuid():N}");
            string batchFolder = Path.Combine(tempDir, batchName);
            Directory.CreateDirectory(batchFolder);

            try
            {
                var ticketList = tickets.ToList();

                // 1. Generate Index
                var sbIndex = new StringBuilder();
                sbIndex.AppendLine("# 🎫 Kerkenez Tickets Export Index");
                sbIndex.AppendLine();
                sbIndex.AppendLine($"> Exported on **{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC** • Total Tickets: **{ticketList.Count}**");
                sbIndex.AppendLine();
                sbIndex.AppendLine("| ID | Priority | App | Type | Status | Title | Attachments | File |");
                sbIndex.AppendLine("|:---|:---|:---|:---|:---|:---|:---|:---|");

                foreach (var t in ticketList)
                {
                    string safeTitle = SanitizeForFileName(t.Title);
                    if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Ticket";
                    string itemBase = $"{t.FormattedId}_{safeTitle}";

                    string relativePath;
                    if (t.AttachmentCount > 0)
                    {
                        ExportTicketFolder(t, batchFolder, itemBase);
                        relativePath = $"{itemBase}/{itemBase}.md";
                    }
                    else
                    {
                        string mdFile = $"{itemBase}.md";
                        string mdPath = Path.Combine(batchFolder, mdFile);
                        File.WriteAllText(mdPath, GenerateTicketMarkdown(t, null), Encoding.UTF8);
                        relativePath = mdFile;
                    }

                    string attText = t.AttachmentCount > 0 ? $"📎 {t.AttachmentCount}" : "-";
                    sbIndex.AppendLine($"| **{t.FormattedId}** | {t.Priority.ToDisplayName()} | `{t.App}` | {t.TicketType} | {t.Status.ToDisplayName()} | {EscapeMarkdown(t.Title)} | {attText} | [{t.FormattedId}.md]({Uri.EscapeDataString(relativePath)}) |");
                }

                File.WriteAllText(Path.Combine(batchFolder, "INDEX.md"), sbIndex.ToString(), Encoding.UTF8);

                if (format == ExportFormat.Folder)
                {
                    string destFolder = Path.Combine(outputDir, batchName);
                    if (Directory.Exists(destFolder)) Directory.Delete(destFolder, true);
                    CopyDirectory(batchFolder, destFolder);
                    return destFolder;
                }
                else
                {
                    string zipPath = Path.Combine(outputDir, $"{batchName}.zip");
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(batchFolder, zipPath, CompressionLevel.Optimal, false);
                    return zipPath;
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch { }
            }
        }

        public static string GenerateTicketMarkdown(TicketItem ticket, string? attachmentsFolder = "attachments")
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# [{ticket.FormattedId}] {ticket.Title}");
            sb.AppendLine();

            sb.AppendLine("| Metadata | Value |");
            sb.AppendLine("|:---|:---|");
            sb.AppendLine($"| **ID** | `{ticket.FormattedId}` |");
            sb.AppendLine($"| **App** | `{ticket.App}` |");
            sb.AppendLine($"| **Type** | {ticket.TicketType} |");
            sb.AppendLine($"| **Priority** | **{ticket.Priority.ToDisplayName()}** |");
            sb.AppendLine($"| **Status** | **{ticket.Status.ToDisplayName()}** |");
            sb.AppendLine($"| **Created** | {ticket.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC |");
            sb.AppendLine($"| **Updated** | {ticket.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC |");
            if (ticket.CompletedAt.HasValue)
            {
                sb.AppendLine($"| **Completed** | {ticket.CompletedAt.Value:yyyy-MM-dd HH:mm:ss} UTC |");
            }
            if (ticket.KilledAt.HasValue)
            {
                sb.AppendLine($"| **Killed** | {ticket.KilledAt.Value:yyyy-MM-dd HH:mm:ss} UTC |");
            }
            if (ticket.Tags != null && ticket.Tags.Count > 0)
            {
                sb.AppendLine($"| **Tags** | {string.Join(", ", ticket.Tags.Select(t => $"`{t}`"))} |");
            }
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 📝 Description");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(ticket.Description))
            {
                sb.AppendLine(ticket.Description);
            }
            else
            {
                sb.AppendLine("*(No description provided)*");
            }
            sb.AppendLine();

            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## 📋 Work Notes & History");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(ticket.Notes))
            {
                sb.AppendLine("```text");
                sb.AppendLine(ticket.Notes);
                sb.AppendLine("```");
            }
            else
            {
                sb.AppendLine("*(No work notes logged)*");
            }
            sb.AppendLine();

            if (ticket.Attachments != null && ticket.Attachments.Count > 0)
            {
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"## 📎 Attachments ({ticket.Attachments.Count})");
                sb.AppendLine();

                foreach (var att in ticket.Attachments)
                {
                    string safeAttName = Uri.EscapeDataString(att.FileName);
                    string relLink = !string.IsNullOrEmpty(attachmentsFolder)
                        ? $"{attachmentsFolder}/{safeAttName}"
                        : safeAttName;

                    if (att.IsImage)
                    {
                        sb.AppendLine($"### 🖼️ {att.FileName} ({att.FormattedFileSize})");
                        sb.AppendLine($"![{att.FileName}]({relLink})");
                        sb.AppendLine();
                    }
                    else if (att.IsLogOrText)
                    {
                        sb.AppendLine($"- 📄 [{att.FileName}]({relLink}) — *{att.FormattedFileSize}* (Attached: {att.CreatedAt:yyyy-MM-dd HH:mm})");
                    }
                    else
                    {
                        sb.AppendLine($"- 📎 [{att.FileName}]({relLink}) — *{att.FormattedFileSize}* (Attached: {att.CreatedAt:yyyy-MM-dd HH:mm})");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine($"*Exported from Kerkenez Ticket at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");

            return sb.ToString();
        }

        public static void RevealInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", $"\"{path}\"");
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Export", $"Failed to reveal {path} in Explorer: {ex.Message}");
            }
        }

        public static string SanitizeForFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (!invalidChars.Contains(c) && c != '\"' && c != '\'' && c != ':' && c != '/')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }
            string clean = sb.ToString().Trim();
            if (clean.Length > 50) clean = clean.Substring(0, 50).TrimEnd();
            return clean;
        }

        private static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("|", "\\|").Replace("\r\n", " ").Replace("\n", " ");
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string targetSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, targetSubDir);
            }
        }
    }
}
