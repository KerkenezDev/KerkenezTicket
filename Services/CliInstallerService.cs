using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace KerkenezTicket.Services
{
    public static class CliInstallerService
    {
        public static string CliInstallDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Kerkenez", "ticket");

        public static string CliExecutablePath => Path.Combine(CliInstallDirectory, "kticket.exe");
        public static string CliCmdPath => Path.Combine(CliInstallDirectory, "kticket.cmd");

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            UIntPtr wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out UIntPtr lpdwResult);

        private const int HWND_BROADCAST = 0xffff;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        public static bool IsCliInstalled()
        {
            return File.Exists(CliExecutablePath);
        }

        public static bool IsPathRegistered()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                string targetDir = CliInstallDirectory.TrimEnd('\\');

                return path.Split(';')
                    .Select(p => p.Trim().TrimEnd('\\'))
                    .Any(p => string.Equals(p, targetDir, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public static bool EnsureInstalledAndRegistered()
        {
            bool installed = EnsureInstalled();
            bool pathOk = EnsurePathRegistered();
            return installed && pathOk;
        }

        public static bool EnsureInstalled()
        {
            try
            {
                if (!Directory.Exists(CliInstallDirectory))
                {
                    Directory.CreateDirectory(CliInstallDirectory);
                }

                string? sourceCli = FindSourceCliExecutable();
                if (string.IsNullOrEmpty(sourceCli) || !File.Exists(sourceCli))
                {
                    System.Diagnostics.Debug.WriteLine("[CliInstallerService] Source kticket.exe not found.");
                    return File.Exists(CliExecutablePath);
                }

                // Copy kticket.exe and accompanying files if needed
                string sourceDir = Path.GetDirectoryName(sourceCli)!;
                bool needCopy = !File.Exists(CliExecutablePath) ||
                                File.GetLastWriteTimeUtc(sourceCli) > File.GetLastWriteTimeUtc(CliExecutablePath);

                if (needCopy)
                {
                    foreach (var file in Directory.GetFiles(sourceDir, "kticket.*"))
                    {
                        string destFile = Path.Combine(CliInstallDirectory, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }

                    // Also copy necessary dependencies if any (e.g. Microsoft.Data.Sqlite, runtimes)
                    foreach (var file in Directory.GetFiles(sourceDir, "*.dll"))
                    {
                        string destFile = Path.Combine(CliInstallDirectory, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }

                    string runtimesDir = Path.Combine(sourceDir, "runtimes");
                    if (Directory.Exists(runtimesDir))
                    {
                        CopyDirectory(runtimesDir, Path.Combine(CliInstallDirectory, "runtimes"));
                    }
                }

                // Write batch helper kticket.cmd
                if (!File.Exists(CliCmdPath))
                {
                    File.WriteAllText(CliCmdPath, "@echo off\r\n\"%~dp0kticket.exe\" %*\r\n");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CliInstallerService] Install error: {ex.Message}");
                return false;
            }
        }

        public static bool EnsurePathRegistered()
        {
            try
            {
                string targetDir = CliInstallDirectory.TrimEnd('\\');
                string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";

                bool alreadyInPath = currentPath.Split(';')
                    .Select(p => p.Trim().TrimEnd('\\'))
                    .Any(p => string.Equals(p, targetDir, StringComparison.OrdinalIgnoreCase));

                if (!alreadyInPath)
                {
                    string newPath = string.IsNullOrWhiteSpace(currentPath)
                        ? targetDir
                        : currentPath.TrimEnd(';') + ";" + targetDir;

                    Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
                    BroadcastEnvironmentChange();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CliInstallerService] PATH registration error: {ex.Message}");
                return false;
            }
        }

        private static void BroadcastEnvironmentChange()
        {
            try
            {
                SendMessageTimeout(
                    (IntPtr)HWND_BROADCAST,
                    WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "Environment",
                    SMTO_ABORTIFHUNG,
                    1500,
                    out _);
            }
            catch { }
        }

        private static string? FindSourceCliExecutable()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Check current base directory
            string p1 = Path.Combine(baseDir, "kticket.exe");
            if (File.Exists(p1)) return p1;

            // 2. Check cli build outputs relative to base directory (development mode)
            string[] relativeCandidates = new[]
            {
                Path.Combine(baseDir, "cli", "kticket.exe"),
                Path.Combine(baseDir, "..", "..", "..", "cli", "bin", "Release", "net10.0-windows10.0.19041.0", "kticket.exe"),
                Path.Combine(baseDir, "..", "..", "..", "cli", "bin", "Debug", "net10.0-windows10.0.19041.0", "kticket.exe"),
                Path.Combine(baseDir, "..", "KTicketCli", "bin", "Release", "net10.0-windows10.0.19041.0", "kticket.exe"),
                Path.Combine(baseDir, "..", "KTicketCli", "bin", "Debug", "net10.0-windows10.0.19041.0", "kticket.exe")
            };

            foreach (var cand in relativeCandidates)
            {
                try
                {
                    string full = Path.GetFullPath(cand);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }

            return null;
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }
            foreach (var sub in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(sub, Path.Combine(targetDir, Path.GetFileName(sub)));
            }
        }

        public static bool RemovePathRegistration()
        {
            try
            {
                string targetDir = CliInstallDirectory.TrimEnd('\\');
                string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";

                var parts = currentPath.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.Equals(p.TrimEnd('\\'), targetDir, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                string newPath = string.Join(";", parts);
                Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.User);
                BroadcastEnvironmentChange();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CliInstallerService] Remove PATH error: {ex.Message}");
                return false;
            }
        }

        public static bool UninstallCli()
        {
            try
            {
                RemovePathRegistration();

                if (Directory.Exists(CliInstallDirectory))
                {
                    Directory.Delete(CliInstallDirectory, true);
                }

                // If parent directory (%LOCALAPPDATA%\Programs\Kerkenez) is now empty, delete it
                string? parent = Path.GetDirectoryName(CliInstallDirectory);
                if (parent != null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    try { Directory.Delete(parent, false); } catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CliInstallerService] Uninstall CLI error: {ex.Message}");
                return false;
            }
        }
    }
}
