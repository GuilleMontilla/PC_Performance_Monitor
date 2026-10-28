# System overview

End-to-end data path: Windows metrics → optional serial → Arduino OLED.

```mermaid
flowchart LR
  subgraph pc ["Windows PC"]
    Program["Program"]
    TerminalApp["TerminalMonitorApp"]
    SerialApp["SerialMonitorApp"]
    Monitor["WindowsNativeMonitor"]
    Bridge["ArduinoSerialBridge"]

    Program -->|"default"| TerminalApp
    Program -->|"--serial"| SerialApp
    TerminalApp --> Monitor
    SerialApp --> Monitor
    SerialApp --> Bridge
  end

  subgraph hw ["Arduino Uno"]
    Sketch["display_metrics.ino"]
    OLED["SSD1306 128x64"]

    Bridge -->|"115200 8N1\n21-byte CRC-8"| Sketch
    Sketch --> OLED
  end
```

## Module flows

- [Program](flows/program.md)
- [WindowsNativeMonitor](flows/windows-native-monitor.md)
- [TerminalMonitorApp](flows/terminal-monitor-app.md)
- [SerialMonitorApp](flows/serial-monitor-app.md)
- [ArduinoSerialBridge](flows/arduino-serial-bridge.md)
- [Firmware display_metrics](flows/display-metrics-firmware.md)
- [Circuit wiring](circuit.md)
