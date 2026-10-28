# Contributing

Thanks for helping improve PC Performance Monitor.

## Setup

1. Clone the repo on Windows 10/11 (x64).
2. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download).
3. Build and run the desktop app:

```bash
cd src/desktop
dotnet restore
dotnet build
dotnet run
```

Run as Administrator for full LibreHardwareMonitor sensors.

Firmware (optional OLED): see [`src/firmware/README.md`](src/firmware/README.md). After serial protocol changes, **re-upload** the sketch so baud and CRC match the PC app.

## Repository layout

| Path | Purpose |
|------|---------|
| `src/desktop/` | .NET 8 Windows app |
| `src/firmware/` | Arduino Uno sketch |
| `docs/` | Guides, protocol, diagrams (not the root) |
| `docs/diagrams/` | Mermaid flows + circuit + hardware PDFs |
| `.github/` | CI, issue/PR templates |

Keep the **root** minimal (README, LICENSE, CONTRIBUTING, config). Put long docs and diagrams under `docs/`.

## Workflow

- Branch from `main` with short-lived names: `feat/...`, `fix/...`, `docs/...`.
- Use [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `chore:`).
- Open a PR against `main`. CI must build (`.github/workflows/ci.yml`).

## Pull requests

Checklist:

- [ ] `dotnet build` succeeds in `src/desktop`
- [ ] Docs updated if behavior or protocol changed (`docs/`, including `docs/diagrams/flows/` when control flow changes)
- [ ] No `bin/`, `obj/`, or secrets committed
- [ ] Title follows Conventional Commits

## Code notes

- Target framework is **`net8.0-windows`** (not `net8.0`); build output is under `bin/.../net8.0-windows/`.
- Package READMEs: [`src/desktop/README.md`](src/desktop/README.md), [`src/firmware/README.md`](src/firmware/README.md).
- Doc hub: [`docs/README.md`](docs/README.md).

## Known pitfalls / debugging notes

When a metric shows `0` or serial misbehaves:

### LibreHardwareMonitor

Inspect `sensor.Name`, `sensor.SensorType`, and `sensor.Value` — type matters by GPU vendor.

- **Network at 0:** do not use only the first `HardwareType.Network` (often Bluetooth). Sum Upload/Download throughput across **all** adapters.
- **VRAM total at 0:** `GPU Memory Total` is often `SensorType.SmallData` (MB), not `Data`. Map Used/Total in `SmallData`; if `Data` (GB), convert with `× 1024`.

### Serial / Arduino

- AutoConnect filters WMI: Uno `VID_2341`+`PID_0043`, then other Arduino VIDs, then CH340 — never the first arbitrary COM.
- Override with `--serial COMx`.
- Checksum is **CRC-8 Dallas/Maxim**; baud **115200**. Update PC and firmware together.

See [docs/serial-protocol.md](docs/serial-protocol.md) and [docs/diagrams/](docs/diagrams/).
