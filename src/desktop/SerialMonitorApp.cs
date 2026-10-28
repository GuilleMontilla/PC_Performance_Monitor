/*
  Class Name  : SerialMonitorApp.cs
  Project     : PC Performance Monitor 
  Author      : Guillermo A. Montilla Ramos
  Contact     : guillermomontilla111@outlook.com
  Created     : 2025-01-XX
  Updated     : 2025-01-XX

  Description:
    Application loop that collects system metrics and streams them to an Arduino
    over serial. Optionally prints metrics to the terminal for debugging.
*/
using System;
using System.Threading;

namespace PCPerformanceMonitor
{
    /// <summary>
    /// Monitor loop that sends packed metrics to an Arduino via serial.
    /// </summary>
    public class SerialMonitorApp
    {
        private readonly WindowsNativeMonitor _hardwareMonitor;
        private readonly ArduinoSerialBridge _bridge;
        private readonly double _updateInterval;
        private readonly bool _verbose;
        private bool _running;
        private readonly CancellationTokenSource _cts;

        public SerialMonitorApp(string? comPort = null, double updateInterval = 1.0, bool verbose = false)
        {
            _hardwareMonitor = new WindowsNativeMonitor();
            _bridge = new ArduinoSerialBridge();
            _updateInterval = updateInterval;
            _verbose = verbose;
            _cts = new CancellationTokenSource();

            if (comPort != null)
                _bridge.Connect(comPort);
            else
                _bridge.AutoConnect();
        }

        /// <summary>
        /// Main monitoring loop. Blocks until Stop() is called or Ctrl+C fires.
        /// </summary>
        public void StartMonitoring()
        {
            if (!_bridge.IsConnected)
            {
                Console.WriteLine("ERROR: No Arduino connected. Exiting.");
                return;
            }

            _running = true;
            Console.WriteLine($"Streaming to {_bridge.PortName} at 1 s interval. Press Ctrl+C to stop.");
            int failures = 0;

            try
            {
                while (_running && !_cts.Token.IsCancellationRequested)
                {
                    var metrics = _hardwareMonitor.GetAllMetrics();

                    bool ok = _bridge.SendMetrics(metrics);

                    if (ok)
                    {
                        failures = 0;
                        if (_verbose)
                            PrintMetrics(metrics);
                    }
                    else
                    {
                        failures++;
                        Console.WriteLine($"Send failed ({failures})");

                        if (failures >= 5)
                        {
                            Console.WriteLine("Too many failures – attempting reconnect...");
                            if (_bridge.AutoConnect())
                                failures = 0;
                            else
                                break;
                        }
                    }

                    Thread.Sleep((int)(_updateInterval * 1000));
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                Stop();
            }
        }

        private void PrintMetrics(WindowsNativeMonitor.AllMetrics m)
        {
            Console.WriteLine(
                $"[{m.Timestamp}] CPU:{m.CpuLoad:F0}% {m.CpuTemp:F0}C | " +
                $"GPU:{m.GpuLoad:F0}% {m.GpuTemp:F0}C | " +
                $"RAM:{m.RamUsed:F0}/{m.RamTotal:F0}MB | " +
                $"NET U:{m.NetUpload:F1} D:{m.NetDownload:F1} KB/s");
        }

        public void Stop()
        {
            _running = false;
            _cts.Cancel();
            _bridge.Dispose();
            _hardwareMonitor.Dispose();
            Console.WriteLine("Serial monitoring stopped.");
        }
    }
}
