# Flow: display_metrics.ino

Firmware on Arduino Uno — parse frames and drive SSD1306.

```mermaid
flowchart TD
  setup([setup]) --> baud[Serial.begin 115200]
  baud --> oled[display.begin]
  oled --> splash[showSplash Waiting]

  loop([loop]) --> avail{Serial available >= 21?}
  avail -->|no| timeout{No packet for 5s?}
  timeout -->|yes| waiting[showWaiting]
  timeout -->|no| loop
  avail -->|yes| peek{peek == 0xAA?}
  peek -->|no| discard[Read discard]
  discard --> avail
  peek -->|yes| read[Read 21-byte buffer]
  read --> hdr{Header 0xAA 0x55?}
  hdr -->|no| avail
  hdr -->|yes| crc{CRC-8 Dallas OK?}
  crc -->|no| avail
  crc -->|yes| copy[Copy to pkt]
  copy --> draw[updateDisplay]
  draw --> loop
```

Source: [`src/firmware/display_metrics/display_metrics.ino`](../../../src/firmware/display_metrics/display_metrics.ino)
