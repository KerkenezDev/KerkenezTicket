using System;
using System.Drawing;

namespace KerkenezTicket.Models
{
    public class AppCategory
    {
        public string Name { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string ColorHex { get; set; } = "#0078D7";
        public string Description { get; set; } = "";
        public int OpenCount { get; set; }
        public int TotalCount { get; set; }

        public Color GetColor()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ColorHex))
                {
                    return ColorTranslator.FromHtml(ColorHex);
                }
            }
            catch { }
            return Color.FromArgb(0, 120, 215);
        }
    }
}
