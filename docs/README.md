# Documentation

Index for PC Performance Monitor.

## Guides

- [Serial protocol](serial-protocol.md) — 21-byte frame, CRC-8 Dallas, 115200 baud, AutoConnect VID/PID
- [Arduino wiring](arduino-wiring.md) — SSD1306 I2C pinout and libraries

## Diagrams

- [System overview](diagrams/overview.md)
- [Circuit (Uno + OLED)](diagrams/circuit.md)
- [Hardware assets (PDF / Fritzing)](diagrams/hardware/)

### Module flows

| Module | Diagram |
|--------|---------|
| Program | [flows/program.md](diagrams/flows/program.md) |
| WindowsNativeMonitor | [flows/windows-native-monitor.md](diagrams/flows/windows-native-monitor.md) |
| TerminalMonitorApp | [flows/terminal-monitor-app.md](diagrams/flows/terminal-monitor-app.md) |
| SerialMonitorApp | [flows/serial-monitor-app.md](diagrams/flows/serial-monitor-app.md) |
| ArduinoSerialBridge | [flows/arduino-serial-bridge.md](diagrams/flows/arduino-serial-bridge.md) |
| Firmware display_metrics | [flows/display-metrics-firmware.md](diagrams/flows/display-metrics-firmware.md) |

## Source

- Desktop app: [`src/desktop/`](../src/desktop/)
- Firmware: [`src/firmware/`](../src/firmware/)
