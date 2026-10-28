# Circuit: Arduino Uno + SSD1306 OLED

I2C wiring for the project target board (**Arduino Uno**, VID `0x2341`, PID `0x0043`).

```mermaid
flowchart LR
  subgraph uno ["Arduino Uno"]
    A4[A4 SDA]
    A5[A5 SCL]
    V5[5V]
    GND[GND]
  end

  subgraph oled ["SSD1306 128x64"]
    SDA[SDA]
    SCL[SCL]
    VCC[VCC]
    OGND[GND]
  end

  A4 --- SDA
  A5 --- SCL
  V5 --- VCC
  GND --- OGND
```

| OLED pin | Uno pin |
|----------|---------|
| SDA | A4 |
| SCL | A5 |
| VCC | 5V |
| GND | GND |

Libraries: Adafruit SSD1306 + Adafruit GFX.

See also [arduino-wiring.md](../arduino-wiring.md) and [hardware assets](hardware/).
