using System;
using System.Collections.Generic;

namespace KerkenezTicket.Models
{
    public class TicketItem
    {
        public string Id { get; set; } = "";
        public int TicketNumber { get; set; }
        public string App { get; set; } = "general";
        public string TicketType { get; set; } = "Bug";
        public TicketStatus Status { get; set; } = TicketStatus.Todo;
        public TicketPriority Priority { get; set; } = TicketPriority.Medium;
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Tags { get; set; } = new List<string>();
        public string Notes { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public DateTime? KilledAt { get; set; }

        public string TagsSummary => Tags != null && Tags.Count > 0 ? string.Join(", ", Tags) : "";

        public string FormattedId => !string.IsNullOrEmpty(Id) ? Id : $"KT-{TicketNumber}";

        public List<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
        public int AttachmentCount => Attachments?.Count ?? 0;

        public TicketItem Clone()
        {
            var clone = new TicketItem
            {
                Id = this.Id,
                TicketNumber = this.TicketNumber,
                App = this.App,
                TicketType = this.TicketType,
                Status = this.Status,
                Priority = this.Priority,
                Title = this.Title,
                Description = this.Description,
                Tags = new List<string>(this.Tags ?? new List<string>()),
                Notes = this.Notes,
                CreatedAt = this.CreatedAt,
                UpdatedAt = this.UpdatedAt,
                CompletedAt = this.CompletedAt,
                KilledAt = this.KilledAt,
                Attachments = new List<TicketAttachment>()
            };

            if (this.Attachments != null)
            {
                foreach (var att in this.Attachments)
                {
                    clone.Attachments.Add(att.Clone());
                }
            }

            return clone;
        }
    }
}
