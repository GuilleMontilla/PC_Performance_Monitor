# Serial Protocol

The PC sends a **21-byte binary packet** (little-endian) to the Arduino at **115200 baud**, 8N1.

| Offset | Size | Field | Type | Description |
|--------|------|-------|------|-------------|
| 0 | 1 | `Header1` | `uint8` | `0xAA` — sync byte 1 |
| 1 | 1 | `Header2` | `uint8` | `0x55` — sync byte 2 |
| 2 | 1 | `CpuLoad` | `uint8` | CPU load (0–100 %) |
| 3 | 1 | `CpuTemp` | `uint8` | CPU temperature (°C) |
| 4 | 1 | `GpuLoad` | `uint8` | GPU load (0–100 %) |
| 5 | 1 | `GpuTemp` | `uint8` | GPU temperature (°C) |
| 6 | 2 | `RamUsed` | `uint16` | RAM used (MB) |
| 8 | 2 | `RamFree` | `uint16` | RAM free (MB) |
| 10 | 2 | `RamTotal` | `uint16` | RAM total (MB) |
| 12 | 4 | `NetUpload` | `uint32` | Upload speed (KB/s) |
| 16 | 4 | `NetDownload` | `uint32` | Download speed (KB/s) |
| 20 | 1 | `Checksum` | `uint8` | CRC-8 Dallas/Maxim of bytes 2–19 |

## Checksum

**CRC-8 Dallas/Maxim** (poly `0x31` reflected, init `0x00`). Same lookup table on PC and Arduino (`PROGMEM`). This catches single-byte corruption better than XOR; it is not a cryptographic integrity check.

PC and firmware must use the same algorithm and baud rate (breaking change vs older XOR / 9600 builds).

## Auto-detect (PC)

`--serial` without a port enumerates COM devices via WMI and only opens Arduino-like ports, in order:

1. Arduino Uno: `VID_2341` + `PID_0043`
2. Other Arduino USB: `VID_2341` or `VID_2A03`
3. Fallback: CH340 / USB-SERIAL (`VID_1A86`, description match)

Use `--serial COMx` to force any port (no filter). Auto-detect never opens the first arbitrary COM that happens to open (e.g. legacy COM1).

## Arduino behavior

The sketch validates the header pair and CRC-8 before updating the display. If no valid packet arrives within 5 seconds, the OLED shows a "Waiting for PC..." message.

Framing: `src/desktop/ArduinoSerialBridge.cs`. Sketch: `src/firmware/display_metrics/display_metrics.ino`.
