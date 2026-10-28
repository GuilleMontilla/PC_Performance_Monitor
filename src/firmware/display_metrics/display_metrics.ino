/*
  Sketch Name : display_metrics.ino
  Project     : PC Performance Monitor
  Author      : Guillermo A. Montilla Ramos
  Contact     : guillermomontilla111@outlook.com

  Description:
    Receives a 21-byte packed binary struct from a PC over serial (115200 baud),
    validates the header and CRC-8 Dallas/Maxim checksum, then renders CPU, GPU,
    RAM and Network telemetry on an SSD1306 128x64 OLED via I2C.

  Required libraries (Arduino IDE Library Manager):
    - Adafruit SSD1306
    - Adafruit GFX Library

  Wiring (I2C):
    SDA -> A4
    SCL -> A5
    VCC -> 5V
    GND -> GND
*/

#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <avr/pgmspace.h>

#define SCREEN_WIDTH  128
#define SCREEN_HEIGHT 64
#define OLED_ADDR     0x3C
#define OLED_RESET    -1

#define HEADER1       0xAA
#define HEADER2       0x55
#define PACKET_SIZE   21
#define TIMEOUT_MS    5000
#define SERIAL_BAUD   115200

Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

struct __attribute__((packed)) MetricsPacket {
  uint8_t  header1;
  uint8_t  header2;
  uint8_t  cpuLoad;
  uint8_t  cpuTemp;
  uint8_t  gpuLoad;
  uint8_t  gpuTemp;
  uint16_t ramUsed;
  uint16_t ramFree;
  uint16_t ramTotal;
  uint32_t netUpload;
  uint32_t netDownload;
  uint8_t  checksum;
};

MetricsPacket pkt;
unsigned long lastPacketTime = 0;
bool hasData = false;

/* CRC-8 Dallas/Maxim lookup (poly 0x31 reflected → 0x8C), stored in flash */
const uint8_t CRC8_TABLE[256] PROGMEM = {
  0x00, 0x5E, 0xBC, 0xE2, 0x61, 0x3F, 0xDD, 0x83, 0xC2, 0x9C, 0x7E, 0x20, 0xA3, 0xFD, 0x1F, 0x41,
  0x9D, 0xC3, 0x21, 0x7F, 0xFC, 0xA2, 0x40, 0x1E, 0x5F, 0x01, 0xE3, 0xBD, 0x3E, 0x60, 0x82, 0xDC,
  0x23, 0x7D, 0x9F, 0xC1, 0x42, 0x1C, 0xFE, 0xA0, 0xE1, 0xBF, 0x5D, 0x03, 0x80, 0xDE, 0x3C, 0x62,
  0xBE, 0xE0, 0x02, 0x5C, 0xDF, 0x81, 0x63, 0x3D, 0x7C, 0x22, 0xC0, 0x9E, 0x1D, 0x43, 0xA1, 0xFF,
  0x46, 0x18, 0xFA, 0xA4, 0x27, 0x79, 0x9B, 0xC5, 0x84, 0xDA, 0x38, 0x66, 0xE5, 0xBB, 0x59, 0x07,
  0xDB, 0x85, 0x67, 0x39, 0xBA, 0xE4, 0x06, 0x58, 0x19, 0x47, 0xA5, 0xFB, 0x78, 0x26, 0xC4, 0x9A,
  0x65, 0x3B, 0xD9, 0x87, 0x04, 0x5A, 0xB8, 0xE6, 0xA7, 0xF9, 0x1B, 0x45, 0xC6, 0x98, 0x7A, 0x24,
  0xF8, 0xA6, 0x44, 0x1A, 0x99, 0xC7, 0x25, 0x7B, 0x3A, 0x64, 0x86, 0xD8, 0x5B, 0x05, 0xE7, 0xB9,
  0x8C, 0xD2, 0x30, 0x6E, 0xED, 0xB3, 0x51, 0x0F, 0x4E, 0x10, 0xF2, 0xAC, 0x2F, 0x71, 0x93, 0xCD,
  0x11, 0x4F, 0xAD, 0xF3, 0x70, 0x2E, 0xCC, 0x92, 0xD3, 0x8D, 0x6F, 0x31, 0xB2, 0xEC, 0x0E, 0x50,
  0xAF, 0xF1, 0x13, 0x4D, 0xCE, 0x90, 0x72, 0x2C, 0x6D, 0x33, 0xD1, 0x8F, 0x0C, 0x52, 0xB0, 0xEE,
  0x32, 0x6C, 0x8E, 0xD0, 0x53, 0x0D, 0xEF, 0xB1, 0xF0, 0xAE, 0x4C, 0x12, 0x91, 0xCF, 0x2D, 0x73,
  0xCA, 0x94, 0x76, 0x28, 0xAB, 0xF5, 0x17, 0x49, 0x08, 0x56, 0xB4, 0xEA, 0x69, 0x37, 0xD5, 0x8B,
  0x57, 0x09, 0xEB, 0xB5, 0x36, 0x68, 0x8A, 0xD4, 0x95, 0xCB, 0x29, 0x77, 0xF4, 0xAA, 0x48, 0x16,
  0xE9, 0xB7, 0x55, 0x0B, 0x88, 0xD6, 0x34, 0x6A, 0x2B, 0x75, 0x97, 0xC9, 0x4A, 0x14, 0xF6, 0xA8,
  0x74, 0x2A, 0xC8, 0x96, 0x15, 0x4B, 0xA9, 0xF7, 0xB6, 0xE8, 0x0A, 0x54, 0xD7, 0x89, 0x6B, 0x35
};

/* ── helpers ─────────────────────────────────────── */

uint8_t crc8Dallas(const uint8_t *data, uint8_t len) {
  uint8_t crc = 0x00;
  for (uint8_t i = 0; i < len; i++) {
    crc = pgm_read_byte(&CRC8_TABLE[crc ^ data[i]]);
  }
  return crc;
}

bool validateChecksum(const uint8_t *buf, uint8_t len) {
  /* CRC over payload bytes [2 .. len-2]; compare to last byte */
  uint8_t crc = crc8Dallas(buf + 2, len - 3);
  return crc == buf[len - 1];
}

/*
 * Format a KB/s value into a human-friendly string.
 * < 1024 KB/s  -> "xxx KB/s"
 * >= 1024      -> "x.x MB/s"
 */
void formatSpeed(uint32_t kbps, char *out, uint8_t outLen) {
  if (kbps < 1024)
    snprintf(out, outLen, "%lu KB/s", (unsigned long)kbps);
  else
    snprintf(out, outLen, "%lu.%lu MB/s",
             (unsigned long)(kbps / 1024),
             (unsigned long)((kbps % 1024) * 10 / 1024));
}

/* ── display routines ────────────────────────────── */

void showSplash() {
  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);

  display.setCursor(16, 8);
  display.print(F("PC  PERFORMANCE"));
  display.setCursor(34, 20);
  display.print(F("MONITOR"));

  display.setCursor(16, 40);
  display.print(F("Waiting for PC..."));

  display.display();
}

void showWaiting() {
  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);

  display.setCursor(10, 24);
  display.print(F("Waiting for PC..."));

  display.display();
}

void updateDisplay() {
  display.clearDisplay();
  display.setTextSize(1);
  display.setTextColor(SSD1306_WHITE);

  /* Row 0 – title */
  display.setCursor(16, 0);
  display.print(F("== PC MONITOR =="));

  /* Row 1 – CPU */
  display.setCursor(0, 10);
  display.print(F("CPU:"));
  if (pkt.cpuLoad < 10)  display.print(' ');
  if (pkt.cpuLoad < 100) display.print(' ');
  display.print(pkt.cpuLoad);
  display.print(F("%"));

  display.setCursor(76, 10);
  if (pkt.cpuTemp < 10)  display.print(' ');
  if (pkt.cpuTemp < 100) display.print(' ');
  display.print(pkt.cpuTemp);
  display.print(F(" C"));

  /* Row 2 – GPU */
  display.setCursor(0, 20);
  display.print(F("GPU:"));
  if (pkt.gpuLoad < 10)  display.print(' ');
  if (pkt.gpuLoad < 100) display.print(' ');
  display.print(pkt.gpuLoad);
  display.print(F("%"));

  display.setCursor(76, 20);
  if (pkt.gpuTemp < 10)  display.print(' ');
  if (pkt.gpuTemp < 100) display.print(' ');
  display.print(pkt.gpuTemp);
  display.print(F(" C"));

  /* Row 3 – separator */
  display.drawFastHLine(0, 29, SCREEN_WIDTH, SSD1306_WHITE);

  /* Row 4 – RAM used / total */
  display.setCursor(0, 32);
  display.print(F("RAM:"));
  display.print(pkt.ramUsed);
  display.print('/');
  display.print(pkt.ramTotal);
  display.print(F(" MB"));

  /* Row 5 – RAM free */
  display.setCursor(0, 42);
  display.print(F("Free: "));
  display.print(pkt.ramFree);
  display.print(F(" MB"));

  /* Row 6 – Network upload */
  char speedBuf[16];

  display.setCursor(0, 52);
  display.print(F("U:"));
  formatSpeed(pkt.netUpload, speedBuf, sizeof(speedBuf));
  display.print(speedBuf);

  /* Row 7 (same y-line, right half) – Network download */
  display.setCursor(68, 52);
  display.print(F("D:"));
  formatSpeed(pkt.netDownload, speedBuf, sizeof(speedBuf));
  display.print(speedBuf);

  display.display();
}

/* ── setup & loop ────────────────────────────────── */

void setup() {
  Serial.begin(SERIAL_BAUD);

  if (!display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDR)) {
    for (;;);  // halt if OLED not found
  }

  showSplash();
  lastPacketTime = millis();
}

void loop() {
  /* Look for the 0xAA 0x55 header byte-by-byte */
  while (Serial.available() >= PACKET_SIZE) {
    if (Serial.peek() != HEADER1) {
      Serial.read();  // discard non-header byte
      continue;
    }

    uint8_t buf[PACKET_SIZE];
    buf[0] = Serial.read();  // consume HEADER1

    if (Serial.available() < (PACKET_SIZE - 1)) break;

    if (Serial.peek() != HEADER2) continue;  // false start

    for (uint8_t i = 1; i < PACKET_SIZE; i++)
      buf[i] = Serial.read();

    if (!validateChecksum(buf, PACKET_SIZE)) continue;

    memcpy(&pkt, buf, PACKET_SIZE);
    lastPacketTime = millis();
    hasData = true;
    updateDisplay();
  }

  if (millis() - lastPacketTime > TIMEOUT_MS) {
    if (hasData) {
      hasData = false;
      showWaiting();
    }
  }
}
