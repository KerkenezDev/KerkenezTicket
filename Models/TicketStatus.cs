using System;
using System.Drawing;

namespace KerkenezTicket.Models
{
    public enum TicketStatus
    {
        Todo,
        Doing,
        Done,
        Killed
    }

    public static class TicketStatusExtensions
    {
        public static string ToKey(this TicketStatus status) => status switch
        {
            TicketStatus.Todo => "todo",
            TicketStatus.Doing => "doing",
            TicketStatus.Done => "done",
            TicketStatus.Killed => "killed",
            _ => "todo"
        };

        public static string ToDisplayName(this TicketStatus status) => status switch
        {
            TicketStatus.Todo => "To Do",
            TicketStatus.Doing => "Doing",
            TicketStatus.Done => "Done",
            TicketStatus.Killed => "Killed",
            _ => status.ToString()
        };

        public static Color GetBadgeColor(this TicketStatus status) => status switch
        {
            TicketStatus.Todo => Color.FromArgb(70, 130, 180),       // Steel Blue
            TicketStatus.Doing => Color.FromArgb(220, 130, 20),      // Amber / Orange
            TicketStatus.Done => Color.FromArgb(40, 167, 69),        // Green
            TicketStatus.Killed => Color.FromArgb(108, 117, 125),    // Gray / Dark slate
            _ => Color.FromArgb(100, 100, 100)
        };

        public static Color GetBadgeBgColor(this TicketStatus status) => status switch
        {
            TicketStatus.Todo => Color.FromArgb(235, 243, 250),
            TicketStatus.Doing => Color.FromArgb(254, 244, 230),
            TicketStatus.Done => Color.FromArgb(232, 245, 233),
            TicketStatus.Killed => Color.FromArgb(240, 242, 245),
            _ => Color.FromArgb(245, 245, 245)
        };

        public static string GetGlyph(this TicketStatus status) => status switch
        {
            TicketStatus.Todo => "\uE71D",   // Circle / task
            TicketStatus.Doing => "\uE768",  // Play / in-progress
            TicketStatus.Done => "\uE73E",   // Checkmark
            TicketStatus.Killed => "\uE711", // Dismiss / Cross
            _ => "\uE71D"
        };

        public static TicketStatus ParseStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return TicketStatus.Todo;
            string normalized = value.Trim().ToLowerInvariant().Replace("-", "").Replace(" ", "").Replace("_", "");

            return normalized switch
            {
                "todo" or "backlog" or "open" or "pending" => TicketStatus.Todo,
                "doing" or "inprogress" or "progress" or "active" or "working" or "wip" => TicketStatus.Doing,
                "done" or "completed" or "resolved" or "closed" or "finish" or "finished" => TicketStatus.Done,
                "killed" or "kill" or "cancelled" or "canceled" or "dropped" or "wontfix" or "dead" => TicketStatus.Killed,
                _ => TicketStatus.Todo
            };
        }
    }
}
