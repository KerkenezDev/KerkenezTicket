using System;
using System.Drawing;

namespace KerkenezTicket.Models
{
    public enum TicketPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }

    public static class TicketPriorityExtensions
    {
        public static string ToKey(this TicketPriority priority) => priority switch
        {
            TicketPriority.Low => "low",
            TicketPriority.Medium => "medium",
            TicketPriority.High => "high",
            TicketPriority.Urgent => "urgent",
            _ => "medium"
        };

        public static string ToDisplayName(this TicketPriority priority) => priority switch
        {
            TicketPriority.Low => "Low",
            TicketPriority.Medium => "Medium",
            TicketPriority.High => "High",
            TicketPriority.Urgent => "Urgent",
            _ => priority.ToString()
        };

        public static Color GetBadgeColor(this TicketPriority priority) => priority switch
        {
            TicketPriority.Low => Color.FromArgb(108, 117, 125),      // Slate
            TicketPriority.Medium => Color.FromArgb(0, 120, 215),     // Blue
            TicketPriority.High => Color.FromArgb(230, 100, 0),       // Orange
            TicketPriority.Urgent => Color.FromArgb(220, 53, 69),     // Crimson Red
            _ => Color.FromArgb(108, 117, 125)
        };

        public static Color GetBadgeBgColor(this TicketPriority priority) => priority switch
        {
            TicketPriority.Low => Color.FromArgb(240, 242, 245),
            TicketPriority.Medium => Color.FromArgb(235, 243, 250),
            TicketPriority.High => Color.FromArgb(255, 243, 230),
            TicketPriority.Urgent => Color.FromArgb(253, 237, 237),
            _ => Color.FromArgb(245, 245, 245)
        };

        public static TicketPriority ParsePriority(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return TicketPriority.Medium;
            string normalized = value.Trim().ToLowerInvariant().Replace("-", "").Replace(" ", "");

            return normalized switch
            {
                "urgent" or "critical" or "p0" or "0" or "emerg" or "emergency" => TicketPriority.Urgent,
                "high" or "p1" or "1" => TicketPriority.High,
                "medium" or "normal" or "mid" or "p2" or "2" => TicketPriority.Medium,
                "low" or "trivial" or "p3" or "3" => TicketPriority.Low,
                _ => TicketPriority.Medium
            };
        }
    }
}
