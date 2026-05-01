# PC-Performance-Monitor
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![Windows](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows&logoColor=white)
![LibreHardwareMonitor](https://img.shields.io/badge/LibreHardwareMonitor-0.9.3-green)
![Arduino](https://img.shields.io/badge/Arduino-Optional-00878F?logo=arduino&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-yellow)

A low-power real-time system performance monitor for Windows built with C# and .NET 8. Collects CPU, GPU, RAM, and network metrics using native Windows APIs (P/Invoke) and [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). 

Supports two operating modes:

 - ***Terminal*** for CLI output
 - ***Serial*** for streaming telemetry to an Arduino with an SSD1306 OLED display.

---

# Table of contents

  - [Features](#features)
    - [Monitored Metrics](#monitored-metrics)
    - [Serial Protocol](#serial-protocol)
  - [Wiring](#wiring)
  - [💾 Installation](#-installation)
  - [📝 How to Use it](#-how-to-use-it)
  - [📸 Example Output/Screenshots](#-example-outputscreenshots)
  - [🌐 Applications](#-applications)
  - [🚀 Future Improvements](#-future-improvements)
  - [📩 Contact](#-contact)
---
## Features

### Monitored Metrics

LibreHardwareMonitor exposes a wide range of hardware sensors — including
voltage, power draw, fan speed, storage health, battery level, and more.
This project collects the following subset, chosen for the most relevant
real-time performance data:

| Category | Metrics |
|----------|---------|
| **CPU**  | Load %, clock speed, frequency, temperature |
| **GPU**  | Load %, clock speed, frequency, temperature, VRAM usage |
| **RAM**  | Load %, used / total (MB) |
| **Network** | Upload speed (KB/s), download speed (KB/s) |

<details>
<summary>All available sensor types in LibreHardwareMonitor</summary>

 | Category | Sensor Types |
|----------|-------------|
| **CPU** | Load, temperature, clock, voltage, power, frequency |
| **GPU** | Load, temperature, clock, voltage, power, fan speed, VRAM usage |
| **Motherboard** | Temperature, voltage, fan speed, fan control |
| **Storage** | Temperature, load, data read/written, throughput, health (SMART) |
| **RAM** | Load, used/available, data throughput |
| **Network** | Upload/download throughput |
| **Battery** | Level, voltage, charge rate, remaining time |
| **PSU** | Voltage, power, temperature |
</details>


### Serial Protocol

The PC sends a **21-byte binary packet** (little-endian) to the Arduino at **9600 baud**:

| Offset | Size | Field | Type | Description |
|--------|------|-------|------|-------------|
| 0 | 1 | `Header1` | `uint8` | `0xAA` — sync byte 1 |
| 1 | 1 | `Header2` | `uint8` | `0x55` — sync byte 2 |
| 2 | 1 | `CpuLoad` | `uint8` | CPU load (0–100 %) |
| 3 | 1 | `CpuTemp` | `uint8` | CPU temperature (C) |
| 4 | 1 | `GpuLoad` | `uint8` | GPU load (0–100 %) |
| 5 | 1 | `GpuTemp` | `uint8` | GPU temperature (C) |
| 6 | 2 | `RamUsed` | `uint16` | RAM used (MB) |
| 8 | 2 | `RamFree` | `uint16` | RAM free (MB) |
| 10 | 2 | `RamTotal` | `uint16` | RAM total (MB) |
| 12 | 4 | `NetUpload` | `uint32` | Upload speed (KB/s) |
| 16 | 4 | `NetDownload` | `uint32` | Download speed (KB/s) |
| 20 | 1 | `Checksum` | `uint8` | XOR of bytes 2–19 |

The Arduino validates the header pair and checksum before updating the display. If no valid packet arrives within 5 seconds, the OLED shows a "Waiting for PC..." message.

---
## Wiring
