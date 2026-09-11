using System;
using System.Drawing;

namespace KerkenezTicket.Models
{
    public static class TicketTypeHelper
    {
        public static readonly string[] DefaultTypes = new[]
        {
            "Bug",
            "Feature",
            "Sync",
            "Task",
            "Improvement"
        };

        public static string NormalizeType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Bug";
            string v = value.Trim();
            string lower = v.ToLowerInvariant();

            return lower switch
            {
                "bug" or "fix" or "defect" or "error" => "Bug",
                "feature" or "feat" or "new" => "Feature",
                "sync" or "synchronize" or "refresh" => "Sync",
                "task" or "chore" or "todo" => "Task",
                "improvement" or "enhancement" or "perf" or "optimize" => "Improvement",
                _ => char.ToUpperInvariant(v[0]) + (v.Length > 1 ? v.Substring(1) : "")
            };
        }

        public static Color GetTypeColor(string type)
        {
            string lower = (type ?? "").ToLowerInvariant();
            return lower switch
            {
                "bug" => Color.FromArgb(217, 83, 79),         // Coral Red
                "feature" => Color.FromArgb(92, 184, 92),     // Emerald
                "sync" => Color.FromArgb(23, 162, 184),       // Cyan / Teal
                "task" => Color.FromArgb(102, 16, 242),       // Purple
                "improvement" => Color.FromArgb(255, 153, 0), // Orange / Gold
                _ => Color.FromArgb(108, 117, 125)
            };
        }

        public static string GetTypeGlyph(string type)
        {
            string lower = (type ?? "").ToLowerInvariant();
            return lower switch
            {
                "bug" => "\uEBE8",        // Bug glyph / Warning
                "feature" => "\uE735",    // Star / Feature
                "sync" => "\uE895",       // Refresh / Sync
                "task" => "\uE71D",       // Task / Checkbox
                "improvement" => "\uE8A1",// Speed / Upgrade
                _ => "\uE71D"
            };
        }
    }
}
