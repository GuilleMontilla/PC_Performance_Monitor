# Arduino Wiring (I2C)

Connect an SSD1306 128×64 OLED to an Arduino Uno over I2C:

| SSD1306 Pin | Arduino Uno Pin |
|-------------|-----------------|
| SDA | A4 |
| SCL | A5 |
| VCC | 5V |
| GND | GND |

## Required libraries

Install via the Arduino IDE Library Manager:

- **Adafruit SSD1306**
- **Adafruit GFX Library**

## Sketch

Upload `src/firmware/display_metrics/display_metrics.ino` to the board (115200 baud, CRC-8 Dallas), then run the PC app in serial mode (see the [README](../README.md#usage)). Target board: **Arduino Uno** (`VID 0x2341`, `PID 0x0043`).

Circuit diagram: [diagrams/circuit.md](diagrams/circuit.md). Fritzing / PDF assets: [diagrams/hardware/](diagrams/hardware/).
