/*
  Class Name  : ArduinoSerialBridge.cs
  Project     : PC Performance Monitor
  Author      : Guillermo A. Montilla Ramos
  Contact     : guillermomontilla111@outlook.com

  Description:
    Handles serial communication with an Arduino Uno. Packs system metrics into
    a 21-byte binary struct (header + payload + CRC-8 Dallas) and sends it over a
    COM port at 115200 baud. AutoConnect prefers VID/PID of Arduino Uno
    (0x2341:0x0043), then other Arduino VIDs, then CH340/USB-SERIAL devices.
*/
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PCPerformanceMonitor
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MetricsPacket
    {
        public byte Header1;       // 0xAA
        public byte Header2;       // 0x55
        public byte CpuLoad;       // 0-100 %
        public byte CpuTemp;       // °C
        public byte GpuLoad;       // 0-100 %
        public byte GpuTemp;       // °C
        public ushort RamUsed;     // MB, little-endian
        public ushort RamFree;     // MB, little-endian
        public ushort RamTotal;    // MB, little-endian
        public uint NetUpload;     // KB/s, little-endian
        public uint NetDownload;   // KB/s, little-endian
        public byte Checksum;      // CRC-8 Dallas/Maxim of bytes [2..19]
    }

    /// <summary>
    /// Bridges the metrics monitor to an Arduino via serial port.
    /// </summary>
    public class ArduinoSerialBridge : IDisposable
    {
        // Target board: Arduino UNO (Board Info)
        public const string ArduinoUnoVid = "2341";
        public const string ArduinoUnoPid = "0043";
        public const string ArduinoOrgVid = "2A03";

        private SerialPort? _serialPort;
        private bool _disposed;

        public bool IsConnected => _serialPort?.IsOpen == true;
        public string PortName => _serialPort?.PortName ?? "None";

        public static readonly int PacketSize = Marshal.SizeOf<MetricsPacket>();
        public const int BaudRate = 115200;

        private static readonly Regex ComPortInName = new(
            @"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Lists Arduino-like COM ports ordered by match priority
        /// (Uno VID+PID, other Arduino VIDs, then CH340/USB-SERIAL).
        /// </summary>
        public static string[] GetAvailablePorts()
        {
            return GetArduinoCandidatePorts().Select(c => c.PortName).ToArray();
        }

        /// <summary>
        /// Opens a connection on the specified COM port (no VID filter).
        /// </summary>
        public bool Connect(string portName)
        {
            try
            {
                _serialPort?.Close();
                _serialPort?.Dispose();

                _serialPort = new SerialPort(portName, BaudRate)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 2000,
                    WriteTimeout = 2000,
                    DtrEnable = true
                };

                _serialPort.Open();
                System.Threading.Thread.Sleep(2000);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to {portName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Connects to the first Arduino-like COM port (VID/PID / CH340 filter).
        /// Does not open arbitrary serial devices.
        /// </summary>
        public bool AutoConnect()
        {
            var candidates = GetArduinoCandidatePorts();
            if (candidates.Count == 0)
            {
                Console.WriteLine("No Arduino-like COM port found (VID 2341/2A03 or CH340/USB-SERIAL).");
                Console.WriteLine("Use --serial COMx to force a port.");
                return false;
            }

            Console.WriteLine("Arduino-like COM candidates:");
            foreach (var c in candidates)
                Console.WriteLine($"  [{c.Priority}] {c.PortName} — {c.Description}");

            foreach (var c in candidates)
            {
                Console.WriteLine($"Trying {c.PortName}...");
                if (Connect(c.PortName))
                {
                    Console.WriteLine($"Connected on {c.PortName}");
                    return true;
                }
            }

            Console.WriteLine("Could not connect to any Arduino-like COM port.");
            return false;
        }

        /// <summary>
        /// Converts AllMetrics into a packed MetricsPacket with CRC-8 Dallas checksum.
        /// </summary>
        public static MetricsPacket PackMetrics(WindowsNativeMonitor.AllMetrics m)
        {
            var pkt = new MetricsPacket
            {
                Header1 = 0xAA,
                Header2 = 0x55,
                CpuLoad = (byte)Math.Clamp(m.CpuLoad, 0, 100),
                CpuTemp = (byte)Math.Clamp(m.CpuTemp, 0, 255),
                GpuLoad = (byte)Math.Clamp(m.GpuLoad, 0, 100),
                GpuTemp = (byte)Math.Clamp(m.GpuTemp, 0, 255),
                RamUsed = (ushort)Math.Clamp(m.RamUsed, 0, ushort.MaxValue),
                RamFree = (ushort)Math.Clamp(m.RamTotal - m.RamUsed, 0, ushort.MaxValue),
                RamTotal = (ushort)Math.Clamp(m.RamTotal, 0, ushort.MaxValue),
                NetUpload = (uint)Math.Clamp(m.NetUpload, 0, uint.MaxValue),
                NetDownload = (uint)Math.Clamp(m.NetDownload, 0, uint.MaxValue),
                Checksum = 0
            };

            byte[] raw = StructToBytes(pkt);
            pkt.Checksum = ComputeCrc8Dallas(raw, 2, raw.Length - 3);
            return pkt;
        }

        /// <summary>
        /// CRC-8 Dallas/Maxim (poly 0x31 reflected). Same table as Arduino PROGMEM.
        /// </summary>
        private static readonly byte[] Crc8Table =
        {
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

        public static byte ComputeCrc8Dallas(byte[] data, int offset, int length)
        {
            byte crc = 0x00;
            for (int i = 0; i < length; i++)
                crc = Crc8Table[crc ^ data[offset + i]];
            return crc;
        }

        /// <summary>
        /// Marshals the struct into a byte array (little-endian, packed).
        /// </summary>
        public static byte[] StructToBytes(MetricsPacket pkt)
        {
            int size = PacketSize;
            byte[] buffer = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(pkt, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return buffer;
        }

        /// <summary>
        /// Packs and sends a single metrics frame to the Arduino.
        /// Returns true on success.
        /// </summary>
        public bool SendMetrics(WindowsNativeMonitor.AllMetrics metrics)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                return false;

            try
            {
                var pkt = PackMetrics(metrics);
                byte[] data = StructToBytes(pkt);
                _serialPort.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Serial write error: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        #region Port discovery (WMI)

        private sealed class PortCandidate
        {
            public required string PortName { get; init; }
            public required string Description { get; init; }
            public int Priority { get; init; } // lower = better
        }

        private static List<PortCandidate> GetArduinoCandidatePorts()
        {
            var results = new List<PortCandidate>();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "";
                    string pnp = obj["PNPDeviceID"]?.ToString() ?? "";

                    var match = ComPortInName.Match(name);
                    if (!match.Success)
                        continue;

                    string port = match.Groups[1].Value.ToUpperInvariant();
                    int? priority = ClassifyPort(pnp, name);
                    if (priority == null)
                        continue;

                    results.Add(new PortCandidate
                    {
                        PortName = port,
                        Description = name,
                        Priority = priority.Value
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WMI port enumeration failed: {ex.Message}");
            }

            return results
                .GroupBy(c => c.PortName)
                .Select(g => g.OrderBy(c => c.Priority).First())
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.PortName)
                .ToList();
        }

        /// <summary>
        /// Returns match priority or null if the device is not Arduino-like.
        /// 0 = Uno VID+PID, 1 = other Arduino VID, 2 = CH340 / USB-SERIAL.
        /// </summary>
        private static int? ClassifyPort(string pnpDeviceId, string name)
        {
            string pnp = pnpDeviceId.ToUpperInvariant();
            string desc = name.ToUpperInvariant();

            bool vid2341 = pnp.Contains("VID_2341");
            bool vid2a03 = pnp.Contains("VID_2A03");
            bool pid0043 = pnp.Contains("PID_0043");

            if (vid2341 && pid0043)
                return 0;

            if (vid2341 || vid2a03)
                return 1;

            if (desc.Contains("CH340") || desc.Contains("USB-SERIAL") || desc.Contains("USB SERIAL")
                || pnp.Contains("CH340") || pnp.Contains("VID_1A86")) // WCH CH340
                return 2;

            return null;
        }

        #endregion
    }
}
