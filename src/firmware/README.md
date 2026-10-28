# Firmware (Arduino Uno)

OLED sketch for PC Performance Monitor.

## Board

- **Arduino Uno** — VID `0x2341`, PID `0x0043`
- Sketch: [`display_metrics/display_metrics.ino`](display_metrics/display_metrics.ino)

## Upload

1. Open the sketch in Arduino IDE.
2. Select board **Arduino Uno**.
3. Install **Adafruit SSD1306** and **Adafruit GFX**.
4. Upload to the board.

Serial settings expected by the PC app: **115200** baud, **CRC-8 Dallas** on the 21-byte frame. Re-upload whenever the desktop protocol changes.

## Docs

- [Wiring](../../docs/arduino-wiring.md)
- [Circuit diagram](../../docs/diagrams/circuit.md)
- [Serial protocol](../../docs/serial-protocol.md)
- [Firmware flow](../../docs/diagrams/flows/display-metrics-firmware.md)
