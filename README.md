# Kerkenez Ticket

A local-first, zero-telemetry, DPAPI-encrypted ticket and issue tracking desktop application and CLI tailored specifically for solo developers and independent software engineers.

---

## Highlights

- **Local-First & Private**: Stores all data on your machine in SQLite (`%APPDATA%\Kerkenez\ticket\tickets.db`). Sensitive fields (titles, descriptions, tags, notes) are hardware-encrypted with Windows DPAPI.
- **Custom Projects & Apps**: Track issues across any number of projects without rigid presets. Add, customize, and badge projects with your own colors.
- **Full CLI (`kticket`)**: Create, list, inspect, and transition tickets directly from your command line or scripts.
- **Flexible UI**: Dual-pane layout with persistent splitter memory, live real-time system logs, automated single-file JSON backups, and multi-monitor DPI support.
- **Single Executable Deployment**: Runs cleanly on Windows without external services, databases, or cloud accounts.

---

## Command Line Interface (`kticket`)

The companion CLI provides instant ticket management from PowerShell, Windows Terminal, or Command Prompt.

```bash
# 1. Create a ticket
kticket add "Fix authentication token refresh timeout" -a auth -p high -t bug

# 2. List tickets (with optional filters)
kticket list
kticket list --app auth --status doing
kticket list --type bug --status todo

# 3. View ticket details & work notes
kticket view KT-1

# 4. Update status
kticket status KT-1 doing
kticket done KT-1
kticket kill KT-1

# 5. Append quick notes (timestamped) without the GUI
kticket note KT-1 "NullReferenceException at Foo.Bar() line 42"
kticket note 1 "repro: open dashboard -> click refresh -> crash"

# 6. Open notes in $EDITOR or notepad for freeform editing
kticket edit KT-1

# 7. Export single-file unencrypted JSON backup
kticket export my_tickets.json

# 8. Register CLI in user PATH
kticket register

# 9. Complete uninstallation (removes registry, PATH, CLI, and data)
kticket uninstall [--yes]
```

---

## Uninstallation & Windows Settings Integration

Kerkenez Ticket registers with Windows Settings (`HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\KerkenezTicket`). It automatically health-checks and heals its registry entry and executable path on every startup.

You can uninstall Kerkenez Ticket at any time via:
- **Windows Settings**: Go to **Settings -> Apps -> Installed Apps**, find **Kerkenez Ticket**, and click **Uninstall**.
- **Desktop Application Command**: `KerkenezTicket.exe --uninstall` (or `--uninstall --quiet` for silent mode).
- **CLI Command**: `kticket uninstall` (or `kticket uninstall --yes`).

A complete uninstall cleans:
1. Windows Settings Installed Apps registration
2. User `PATH` environment variable registration
3. Local CLI executable files in `%LOCALAPPDATA%\Programs\Kerkenez\ticket\`
4. All tickets database, configuration, and backups in `%APPDATA%\Kerkenez\ticket\`

---

## Building from Source

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) on Windows 10/11.

### Standalone Build
To compile single-file standalone binaries for both the Desktop GUI and CLI:
```powershell
dotnet-standalone .\publish\
```

---

## License

This project is licensed under the [MIT License](LICENSE).
Third-party component attributions and licenses are documented in [NOTICES.md](NOTICES.md).
