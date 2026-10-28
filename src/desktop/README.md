# Desktop app (.NET 8)

Windows host for PC Performance Monitor.

## Build and run

```bash
cd src/desktop
dotnet restore
dotnet build
dotnet run
```

Run as **Administrator** for full LibreHardwareMonitor sensors.

```bash
dotnet run -- --serial
dotnet run -- --serial COM3 --verbose
```

## Docs

- [Documentation index](../../docs/README.md)
- [Serial protocol](../../docs/serial-protocol.md)
- [Module flows](../../docs/diagrams/flows/)
