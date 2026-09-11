using System;
using System.IO;
using System.Runtime.InteropServices;

namespace KerkenezTicket.Services
{
    /// <summary>
    /// Manages Desktop and Start Menu shortcuts for Kerkenez Ticket.
    /// Uses COM WScript.Shell interop (no extra dependencies).
    /// </summary>
    public static class ShortcutService
    {
        private const string ShortcutName = "Kerkenez Ticket.lnk";

        public static string DesktopShortcutPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName);

        public static string StartMenuShortcutPath
        {
            get
            {
                string startFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs", "Kerkenez");
                return Path.Combine(startFolder, ShortcutName);
            }
        }

        /// <summary>
        /// Gets the current exe path reliably.
        /// </summary>
        private static string? GetExePath()
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KerkenezTicket.exe");
            }
            return File.Exists(exePath) ? exePath : null;
        }

        /// <summary>
        /// Creates both Desktop and Start Menu shortcuts.
        /// Returns true if at least one was created successfully.
        /// </summary>
        public static bool CreateShortcuts()
        {
            string? exePath = GetExePath();
            if (exePath == null) return false;

            bool any = false;
            any |= CreateShortcut(DesktopShortcutPath, exePath);
            any |= CreateShortcut(StartMenuShortcutPath, exePath);
            return any;
        }

        /// <summary>
        /// Health-checks existing shortcuts. If a shortcut file exists but points to a
        /// wrong/old target path, updates it to point to the current exe location.
        /// Does NOT recreate deleted shortcuts (respects user choice to remove them).
        /// </summary>
        public static void HealShortcuts()
        {
            try
            {
                string? exePath = GetExePath();
                if (exePath == null) return;

                HealSingleShortcut(DesktopShortcutPath, exePath);
                HealSingleShortcut(StartMenuShortcutPath, exePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShortcutService] Heal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes both Desktop and Start Menu shortcuts if they exist.
        /// Also cleans up the Start Menu folder if empty.
        /// </summary>
        public static void RemoveShortcuts()
        {
            try
            {
                if (File.Exists(DesktopShortcutPath))
                    File.Delete(DesktopShortcutPath);
            }
            catch { }

            try
            {
                if (File.Exists(StartMenuShortcutPath))
                    File.Delete(StartMenuShortcutPath);

                // Clean up the Kerkenez start menu folder if now empty
                string? startDir = Path.GetDirectoryName(StartMenuShortcutPath);
                if (startDir != null && Directory.Exists(startDir) &&
                    !Directory.EnumerateFileSystemEntries(startDir).GetEnumerator().MoveNext())
                {
                    Directory.Delete(startDir, false);
                }
            }
            catch { }
        }

        /// <summary>
        /// Checks if at least one shortcut currently exists.
        /// </summary>
        public static bool AnyShortcutExists()
        {
            return File.Exists(DesktopShortcutPath) || File.Exists(StartMenuShortcutPath);
        }

        private static bool CreateShortcut(string lnkPath, string targetExe)
        {
            try
            {
                string? dir = Path.GetDirectoryName(lnkPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Use COM interop with WScript.Shell to create a proper .lnk
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                dynamic shell = Activator.CreateInstance(shellType)!;
                try
                {
                    dynamic shortcut = shell.CreateShortcut(lnkPath);
                    try
                    {
                        shortcut.TargetPath = targetExe;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe) ?? "";
                        shortcut.Description = "Kerkenez Ticket - Local-first encrypted ticket tracking";
                        shortcut.IconLocation = $"{targetExe},0";
                        shortcut.Save();
                        return true;
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(shortcut);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShortcutService] Error creating shortcut '{lnkPath}': {ex.Message}");
                return false;
            }
        }

        private static void HealSingleShortcut(string lnkPath, string correctTarget)
        {
            // Only heal if the shortcut file already exists (don't recreate deleted ones)
            if (!File.Exists(lnkPath)) return;

            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                dynamic shell = Activator.CreateInstance(shellType)!;
                try
                {
                    dynamic shortcut = shell.CreateShortcut(lnkPath);
                    try
                    {
                        string currentTarget = shortcut.TargetPath ?? "";
                        string currentWorkDir = shortcut.WorkingDirectory ?? "";
                        string expectedWorkDir = Path.GetDirectoryName(correctTarget) ?? "";

                        bool needsHeal =
                            !string.Equals(currentTarget, correctTarget, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(currentWorkDir, expectedWorkDir, StringComparison.OrdinalIgnoreCase);

                        if (needsHeal)
                        {
                            shortcut.TargetPath = correctTarget;
                            shortcut.WorkingDirectory = expectedWorkDir;
                            shortcut.IconLocation = $"{correctTarget},0";
                            shortcut.Save();

                            System.Diagnostics.Debug.WriteLine($"[ShortcutService] Healed shortcut: {lnkPath}");
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(shortcut);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShortcutService] Error healing shortcut '{lnkPath}': {ex.Message}");
            }
        }
    }
}
