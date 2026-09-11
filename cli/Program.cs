using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KerkenezTicket.Models;
using KerkenezTicket.Services;

namespace KerkenezTicket.CLI
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args == null || args.Length == 0)
                {
                    PrintHelp();
                    return 0;
                }

                string command = args[0].ToLowerInvariant().TrimStart('-', '/');

                switch (command)
                {
                    case "add":
                    case "new":
                    case "create":
                        return HandleAdd(args.Skip(1).ToArray());

                    case "list":
                    case "ls":
                    case "all":
                        return HandleList(args.Skip(1).ToArray());

                    case "view":
                    case "show":
                    case "get":
                        return HandleView(args.Skip(1).ToArray());

                    case "status":
                    case "set-status":
                        return HandleStatus(args.Skip(1).ToArray());

                    case "done":
                    case "resolve":
                    case "close":
                        return HandleQuickStatus(args.Skip(1).ToArray(), TicketStatus.Done);

                    case "kill":
                    case "drop":
                    case "cancel":
                        return HandleQuickStatus(args.Skip(1).ToArray(), TicketStatus.Killed);

                    case "export":
                    case "backup":
                        return HandleExport(args.Skip(1).ToArray());

                    case "register":
                    case "install":
                        return HandleRegister();

                    case "uninstall":
                    case "--uninstall":
                    case "-uninstall":
                    case "/uninstall":
                        return HandleCliUninstall(args.Skip(1).ToArray());

                    case "help":
                    case "?":
                    case "h":
                        PrintHelp();
                        return 0;

                    default:
                        // If first argument didn't match a command, check if user ran `kticket "some title" -a mail...`
                        if (!args[0].StartsWith("-"))
                        {
                            return HandleAdd(args);
                        }
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Unknown command: {args[0]}");
                        Console.ResetColor();
                        PrintHelp();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();
                return 1;
            }
        }

        private static int HandleAdd(string[] args)
        {
            if (args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Usage: kticket add \"<title>\" [-a <app>] [-p <priority>] [-t <type>] [-d <desc>] [-s <status>]");
                Console.ResetColor();
                return 1;
            }

            var configService = new ConfigService();
            var db = new TicketDatabaseService();

            string title = "";
            string app = configService.Settings.DefaultApp;
            string priority = configService.Settings.DefaultPriority;
            string type = configService.Settings.DefaultType;
            string desc = "";
            string status = "todo";
            var tags = new List<string>();

            int i = 0;
            if (!args[0].StartsWith("-"))
            {
                title = args[0];
                i = 1;
            }

            while (i < args.Length)
            {
                string arg = args[i];
                string next = (i + 1 < args.Length) ? args[i + 1] : "";

                switch (arg.ToLowerInvariant())
                {
                    case "-a":
                    case "--app":
                        app = next;
                        i += 2;
                        break;
                    case "-p":
                    case "--priority":
                        priority = next;
                        i += 2;
                        break;
                    case "-t":
                    case "--type":
                        type = next;
                        i += 2;
                        break;
                    case "-d":
                    case "--desc":
                    case "--description":
                        desc = next;
                        i += 2;
                        break;
                    case "-s":
                    case "--status":
                        status = next;
                        i += 2;
                        break;
                    case "--tag":
                    case "--tags":
                        if (!string.IsNullOrWhiteSpace(next))
                        {
                            tags.AddRange(next.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));
                        }
                        i += 2;
                        break;
                    default:
                        if (string.IsNullOrEmpty(title))
                        {
                            title = arg;
                        }
                        i++;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: Ticket title is required.");
                Console.ResetColor();
                return 1;
            }

            var ticket = new TicketItem
            {
                Title = title.Trim(),
                App = string.IsNullOrWhiteSpace(app) ? "general" : app.Trim().ToLowerInvariant(),
                TicketType = TicketTypeHelper.NormalizeType(type),
                Priority = TicketPriorityExtensions.ParsePriority(priority),
                Status = TicketStatusExtensions.ParseStatus(status),
                Description = desc.Trim(),
                Tags = tags
            };

            var created = db.AddTicket(ticket);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[SUCCESS] ");
            Console.ResetColor();
            Console.WriteLine($"Created ticket {created.FormattedId}");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine($"  Title:    {created.Title}");
            Console.WriteLine($"  App:      {created.App}");
            Console.WriteLine($"  Type:     {created.TicketType}");
            Console.WriteLine($"  Priority: {created.Priority.ToDisplayName()}");
            Console.WriteLine($"  Status:   {created.Status.ToDisplayName()}");
            if (created.Tags != null && created.Tags.Count > 0)
            {
                Console.WriteLine($"  Tags:     {string.Join(", ", created.Tags)}");
            }
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Storage:  %APPDATA%\\Kerkenez\\ticket\\tickets.db (DPAPI Encrypted)");
            Console.ResetColor();

            return 0;
        }

        private static int HandleList(string[] args)
        {
            var db = new TicketDatabaseService();

            string? app = null;
            string? type = null;
            TicketStatus? status = null;
            string? search = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string next = (i + 1 < args.Length) ? args[i + 1] : "";

                switch (arg.ToLowerInvariant())
                {
                    case "-a":
                    case "--app":
                        app = next;
                        i++;
                        break;
                    case "-t":
                    case "--type":
                        type = next;
                        i++;
                        break;
                    case "-s":
                    case "--status":
                        status = TicketStatusExtensions.ParseStatus(next);
                        i++;
                        break;
                    case "--search":
                    case "-q":
                        search = next;
                        i++;
                        break;
                    case "--all":
                        status = null;
                        break;
                }
            }

            var tickets = db.GetFilteredTickets(app, type, status, search);

            if (tickets.Count == 0)
            {
                Console.WriteLine("No tickets found matching the criteria.");
                return 0;
            }

            Console.WriteLine();
            Console.WriteLine($"{"ID",-8} {"APP",-10} {"TYPE",-12} {"STATUS",-10} {"PRIORITY",-10} {"TITLE"}");
            Console.WriteLine(new string('-', 85));

            foreach (var t in tickets)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"{t.FormattedId,-8} ");

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{t.App,-10} ");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{t.TicketType,-12} ");

                switch (t.Status)
                {
                    case TicketStatus.Todo:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;
                    case TicketStatus.Doing:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        break;
                    case TicketStatus.Done:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case TicketStatus.Killed:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;
                }
                Console.Write($"{t.Status.ToDisplayName(),-10} ");

                switch (t.Priority)
                {
                    case TicketPriority.Urgent:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case TicketPriority.High:
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        break;
                    case TicketPriority.Medium:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                    case TicketPriority.Low:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                }
                Console.Write($"{t.Priority.ToDisplayName(),-10} ");

                Console.ResetColor();
                string displayTitle = t.Title.Length > 35 ? t.Title.Substring(0, 32) + "..." : t.Title;
                Console.WriteLine(displayTitle);
            }

            Console.ResetColor();
            Console.WriteLine(new string('-', 85));
            var stats = db.GetStatistics(app);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Total: {stats.Total} | Todo: {stats.Todo} | Doing: {stats.Doing} | Done: {stats.Done} | Killed: {stats.Killed}");
            Console.ResetColor();
            Console.WriteLine();

            return 0;
        }

        private static int HandleView(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: kticket view <id|number>");
                return 1;
            }

            var db = new TicketDatabaseService();
            var ticket = db.GetTicketById(args[0]);

            if (ticket == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ticket not found: {args[0]}");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"=== {ticket.FormattedId}: {ticket.Title} ===");
            Console.ResetColor();
            Console.WriteLine($"App:         {ticket.App}");
            Console.WriteLine($"Type:        {ticket.TicketType}");
            Console.WriteLine($"Status:      {ticket.Status.ToDisplayName()}");
            Console.WriteLine($"Priority:    {ticket.Priority.ToDisplayName()}");
            Console.WriteLine($"Created:     {ticket.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Updated:     {ticket.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            if (ticket.CompletedAt.HasValue) Console.WriteLine($"Completed:   {ticket.CompletedAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            if (ticket.KilledAt.HasValue) Console.WriteLine($"Killed:      {ticket.KilledAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
            if (ticket.Tags != null && ticket.Tags.Count > 0)
            {
                Console.WriteLine($"Tags:        {string.Join(", ", ticket.Tags)}");
            }
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Description:");
            Console.WriteLine(string.IsNullOrWhiteSpace(ticket.Description) ? "(No description)" : ticket.Description);
            if (!string.IsNullOrWhiteSpace(ticket.Notes))
            {
                Console.WriteLine(new string('-', 50));
                Console.WriteLine("Notes / History:");
                Console.WriteLine(ticket.Notes);
            }
            Console.WriteLine();

            return 0;
        }

        private static int HandleStatus(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: kticket status <id|number> <todo|doing|done|killed>");
                return 1;
            }

            var db = new TicketDatabaseService();
            var newStatus = TicketStatusExtensions.ParseStatus(args[1]);

            bool ok = db.UpdateTicketStatus(args[0], newStatus);
            if (ok)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUCCESS] Updated ticket {args[0]} status to {newStatus.ToDisplayName()}");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ticket not found: {args[0]}");
                Console.ResetColor();
                return 1;
            }
        }

        private static int HandleQuickStatus(string[] args, TicketStatus status)
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"Usage: kticket {status.ToKey()} <id|number>");
                return 1;
            }

            var db = new TicketDatabaseService();
            bool ok = db.UpdateTicketStatus(args[0], status);
            if (ok)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUCCESS] Marked ticket {args[0]} as {status.ToDisplayName()}");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ticket not found: {args[0]}");
                Console.ResetColor();
                return 1;
            }
        }

        private static int HandleExport(string[] args)
        {
            var db = new TicketDatabaseService();
            var config = new ConfigService();
            var backupService = new BackupService(db, config);

            string? destination = args.Length > 0 ? args[0] : null;
            string path = backupService.CreateBackup(destination);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Backup file created at:");
            Console.ResetColor();
            Console.WriteLine($"  {path}");

            return 0;
        }

        private static int HandleRegister()
        {
            bool ok = CliInstallerService.EnsureInstalledAndRegistered();
            if (ok)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[SUCCESS] kticket installed and registered in User PATH.");
                Console.ResetColor();
                Console.WriteLine($"Location: {CliInstallerService.CliExecutablePath}");
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] Could not fully register kticket.");
                Console.ResetColor();
                return 1;
            }
        }

        private static void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine(" Kerkenez Ticket CLI (kticket)");
            Console.WriteLine(" Local-first DPAPI-encrypted ticket tracking");
            Console.WriteLine("==================================================");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  kticket add \"<title>\" [options]");
            Console.WriteLine("  kticket list [options]");
            Console.WriteLine("  kticket view <id|number>");
            Console.WriteLine("  kticket status <id|number> <todo|doing|done|killed>");
            Console.WriteLine("  kticket done <id|number>");
            Console.WriteLine("  kticket kill <id|number>");
            Console.WriteLine("  kticket export [filepath]");
            Console.WriteLine("  kticket register");
            Console.WriteLine("  kticket uninstall [--yes]");
            Console.WriteLine("  kticket help");
            Console.WriteLine();
            Console.WriteLine("Add Options:");
            Console.WriteLine("  -a, --app <name>        Target app or project (e.g. web, api, client)");
            Console.WriteLine("  -p, --priority <level>  urgent | high | medium | low");
            Console.WriteLine("  -t, --type <type>       bug | feature | sync | task | improvement");
            Console.WriteLine("  -d, --desc <text>       Detailed description");
            Console.WriteLine("  -s, --status <status>   todo | doing | done | killed");
            Console.WriteLine("  --tag <tags>            Comma-separated tags");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  kticket add \"fix a refresh error sync on x situation\" -a myapp -p high -t sync");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static int HandleCliUninstall(string[] args)
        {
            bool isQuiet = args.Any(a => a.Equals("--yes", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("-y", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("--quiet", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("-q", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("-s", StringComparison.OrdinalIgnoreCase));

            if (!isQuiet)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("WARNING: This will completely uninstall Kerkenez Ticket, remove the CLI from PATH,");
                Console.WriteLine("and delete all local ticket databases and configuration in %APPDATA%\\Kerkenez\\ticket.");
                Console.ResetColor();
                Console.Write("Are you sure you want to proceed? [y/N]: ");

                var response = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (response != "y" && response != "yes")
                {
                    Console.WriteLine("Uninstall cancelled.");
                    return 0;
                }
            }

            bool success = ConfigService.Uninstall();
            if (success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[SUCCESS] Kerkenez Ticket and all associated databases and registrations were successfully removed.");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] Uninstall completed with some warnings. Please verify %APPDATA%\\Kerkenez\\ticket.");
                Console.ResetColor();
                return 1;
            }
        }
    }
}
