using System;
using System.IO;

namespace KerkenezTicket.Models
{
    public class TicketAttachment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string TicketId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string StoredRelativePath { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string FileExtension => !string.IsNullOrEmpty(FileName) ? Path.GetExtension(FileName).ToLowerInvariant() : "";

        public bool IsImage
        {
            get
            {
                string ext = FileExtension;
                return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".ico" or ".svg";
            }
        }

        public bool IsLogOrText
        {
            get
            {
                string ext = FileExtension;
                return ext is ".log" or ".txt" or ".json" or ".xml" or ".csv" or ".md" or ".cs" or ".sql" or ".yaml" or ".yml" or ".ini" or ".cfg" or ".env";
            }
        }

        public string FormattedFileSize
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{(FileSizeBytes / 1024.0):F1} KB";
                if (FileSizeBytes < 1024 * 1024 * 1024) return $"{(FileSizeBytes / (1024.0 * 1024.0)):F1} MB";
                return $"{(FileSizeBytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
            }
        }

        public TicketAttachment Clone()
        {
            return new TicketAttachment
            {
                Id = this.Id,
                TicketId = this.TicketId,
                FileName = this.FileName,
                StoredRelativePath = this.StoredRelativePath,
                FileSizeBytes = this.FileSizeBytes,
                CreatedAt = this.CreatedAt
            };
        }
    }
}
