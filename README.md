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

# 5. Export single-file unencrypted JSON backup
kticket export my_tickets.json

# 6. Register CLI in user PATH
kticket register
```

---

## Building from Source

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) on Windows 10/11.

### Build Application & CLI
```bash
# Build desktop app
dotnet build KerkenezTicket.csproj -c Release

# Build CLI tool
dotnet build cli/KTicketCli.csproj -c Release
```

### Publish Binaries
```bash
dotnet publish KerkenezTicket.csproj -c Release -o publish
dotnet publish cli/KTicketCli.csproj -c Release -o publish
```

---

## License

This project is licensed under the [MIT License](LICENSE).
Third-party component attributions and licenses are documented in [NOTICES.md](NOTICES.md).
