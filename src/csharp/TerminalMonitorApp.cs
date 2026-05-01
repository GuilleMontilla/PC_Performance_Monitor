/*
  Class Name  : TerminalMonitorApp.cs
  Project     : PC Performance Monitor 
  Author      : Guillermo A. Montilla Ramos
  Contact     : guillermomontilla111@outlook.com
  Created     : 2025-01-XX
  Updated     : 2025-01-XX

  Description:
    Terminal application for displaying real-time system performance metrics.
*/
using System;
using System.Threading;

namespace PCPerformanceMonitor
{
    /// <summary>
    /// Terminal application for monitoring system performance
    /// </summary>
    public class TerminalMonitorApp
    {
        private readonly WindowsNativeMonitor _hardwareMonitor;
        private readonly double _updateInterval;
        private bool _running;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public TerminalMonitorApp(double updateInterval = 1.0)
        {
            _hardwareMonitor = new WindowsNativeMonitor();
            _updateInterval = updateInterval;
            _running = false;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts hardware monitoring
        /// </summary>
        public void StartMonitoring()
        {
            _running = true;
            Console.WriteLine("Press Ctrl+C to stop");

            try
            {
                while (_running && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Get metrics
                    var metrics = _hardwareMonitor.GetAllMetrics();

                    // Display in console
                    PrintMetrics(metrics);

                    // Wait interval
                    Thread.Sleep((int)(_updateInterval * 1000));
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nStopping monitoring...");
            }
            finally
            {
                Stop();
            }
        }

        /// <summary>
        /// Prints metrics to console
        /// </summary>
        private void PrintMetrics(WindowsNativeMonitor.AllMetrics metrics)
        {
            Console.WriteLine($"\n=== System Metrics - {metrics.Timestamp} ===");
            Console.WriteLine($"CPU: Load: {metrics.CpuLoad:F1}% | Clock: {metrics.CpuClock:F0}MHz | Freq: {metrics.CpuFreq:F0}MHz | Temp: {metrics.CpuTemp:F1}°C");
            Console.WriteLine($"GPU: Load: {metrics.GpuLoad:F1}% | Clock: {metrics.GpuClock:F0}MHz | Freq: {metrics.GpuFreq:F0}MHz | Temp: {metrics.GpuTemp:F1}°C");
            Console.WriteLine($"VRAM: {metrics.VramUsed:F0}MB / {metrics.VramTotal:F0}MB");
            Console.WriteLine($"RAM: Load: {metrics.RamLoad:F1}% | Used: {metrics.RamUsed:F0}MB / {metrics.RamTotal:F0}MB");
            Console.WriteLine($"NET: Upload: {metrics.NetUpload:F1} KB/s | Download: {metrics.NetDownload:F1} KB/s");
        }

        /// <summary>
        /// Stops monitoring
        /// </summary>
        public void Stop()
        {
            _running = false;
            _cancellationTokenSource.Cancel();
            _hardwareMonitor.Dispose();
            Console.WriteLine("Monitoring stopped");
        }
    }
}
