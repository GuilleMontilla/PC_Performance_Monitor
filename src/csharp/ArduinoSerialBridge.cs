/*
  Class Name  : ArduinoSerialBridge.cs
  Project     : PC Performance Monitor 
  Author      : Guillermo A. Montilla Ramos
  Contact     : guillermomontilla111@outlook.com
  Created     : 2025-01-XX
  Updated     : 2025-01-XX

  Description:
    Handles serial communication with an Arduino Uno. Packs system metrics into
    a 21-byte binary struct (header + payload + checksum) and sends it over a
    COM port at 9600 baud.
*/
using System;
using System.IO.Ports;
using System.Runtime.InteropServices;

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
        public byte Checksum;      // XOR of bytes [2..19]
    }

    /// <summary>
    /// Bridges the metrics monitor to an Arduino via serial port.
    /// </summary>
    public class ArduinoSerialBridge : IDisposable
    {
        private SerialPort? _serialPort;
        private bool _disposed;

        public bool IsConnected => _serialPort?.IsOpen == true;
        public string PortName => _serialPort?.PortName ?? "None";

        public static readonly int PacketSize = Marshal.SizeOf<MetricsPacket>();
        private const int BaudRate = 9600;

        /// <summary>
        /// Lists all available COM port names on the system.
        /// </summary>
        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// Opens a connection on the specified COM port.
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
        /// Tries each available COM port until one opens successfully.
        /// </summary>
        public bool AutoConnect()
        {
            var ports = GetAvailablePorts();
            if (ports.Length == 0)
            {
                Console.WriteLine("No COM ports found.");
                return false;
            }

            foreach (var port in ports)
            {
                Console.WriteLine($"Trying {port}...");
                if (Connect(port))
                {
                    Console.WriteLine($"Connected on {port}");
                    return true;
                }
            }

            Console.WriteLine("Could not connect to any COM port.");
            return false;
        }

        /// <summary>
        /// Converts AllMetrics into a packed MetricsPacket.
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
            byte xor = 0;
            for (int i = 2; i < raw.Length - 1; i++)
                xor ^= raw[i];
            pkt.Checksum = xor;

            return pkt;
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
    }
}
