/*
  Class Name  : WindowsNativeMonitor.cs
  Project     : PC Performance Monitor 
  Author      : Guillermo A. Montilla Ramos
  Contact     : guillermomontilla111@outlook.com
  Created     : 2025-01-XX
  Updated     : 2025-01-XX

  Description:
    This C# class collects real-time system performance data from a Windows system using native Windows APIs.
    Uses P/Invoke to retrieve CPU usage and RAM using kernel32.dll. Also uses the LibreHardwareMonitor 
    NuGet package to retrieve CPU clock speed, CPU temperature, CPU frequency, GPU usage, GPU temperature, 
    GPU clock speeds, GPU frequency and VRAM.

  History:
    2025-01-XX – Initial C# version converted from Python implementation.
*/
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;

namespace PCPerformanceMonitor
{
    /// <summary>
    /// Monitors Windows system performance using native APIs and LibreHardwareMonitor
    /// </summary>
    public class WindowsNativeMonitor : IDisposable
    {
        #region P/Invoke Declarations

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemTimes(
            out FILETIME lpIdleTime,
            out FILETIME lpKernelTime,
            out FILETIME lpUserTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
        }

        #endregion

        #region Private Fields

        private long _prevIdleTime;
        private long _prevKernelTime;
        private long _prevUserTime;
        private readonly object _cpuLock = new object();

        private CpuFrequencyCache? _cpuFreqCache;
        private readonly int _cacheDurationSeconds = 30;

        private Computer? _computer;
        private IHardware? _cpuHardware;
        private IHardware? _gpuHardware;
        private IHardware? _networkHardware;

        #endregion

        #region Constructor

        public WindowsNativeMonitor()
        {
            InitializeLibreHardwareMonitor();
        }

        private void InitializeLibreHardwareMonitor()
        {
            try
            {
                _computer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsNetworkEnabled = true
                };
                _computer.Open();

                // Find CPU hardware
                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        _cpuHardware = hardware;
                    }
                    else if (hardware.HardwareType == HardwareType.GpuNvidia || 
                             hardware.HardwareType == HardwareType.GpuAmd ||
                             hardware.HardwareType == HardwareType.GpuIntel)
                    {
                        _gpuHardware = hardware;
                    }
                    else if (hardware.HardwareType == HardwareType.Network)
                    {
                        _networkHardware ??= hardware;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize LibreHardwareMonitor: {ex.Message}");
            }
        }

        #endregion

        #region CPU Methods

        /// <summary>
        /// Gets CPU usage using GetSystemTimes - Thread Safe
        /// </summary>
        public double GetCpuUsageNative()
        {
            lock (_cpuLock)
            {
                try
                {
                    if (!GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
                    {
                        return 0.0;
                    }

                    long currentIdle = ((long)idleTime.dwHighDateTime << 32) | idleTime.dwLowDateTime;
                    long currentKernel = ((long)kernelTime.dwHighDateTime << 32) | kernelTime.dwLowDateTime;
                    long currentUser = ((long)userTime.dwHighDateTime << 32) | userTime.dwLowDateTime;

                    // First call - store values and return 0
                    if (_prevIdleTime == 0 && _prevKernelTime == 0 && _prevUserTime == 0)
                    {
                        _prevIdleTime = currentIdle;
                        _prevKernelTime = currentKernel;
                        _prevUserTime = currentUser;
                        return 0.0;
                    }

                    // Calculate differences
                    long idleDiff = currentIdle - _prevIdleTime;
                    long kernelDiff = currentKernel - _prevKernelTime;
                    long userDiff = currentUser - _prevUserTime;

                    // Update previous values
                    _prevIdleTime = currentIdle;
                    _prevKernelTime = currentKernel;
                    _prevUserTime = currentUser;

                    // Calculate CPU usage
                    long totalSystemTime = kernelDiff + userDiff;

                    if (totalSystemTime == 0)
                    {
                        return 0.0;
                    }

                    double cpuUsage = ((totalSystemTime - idleDiff) / (double)totalSystemTime) * 100.0;

                    // Ensure it's in valid range
                    return Math.Max(0.0, Math.Min(100.0, cpuUsage));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting CPU usage with GetSystemTimes: {ex.Message}");
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// Gets CPU frequency with cache to avoid frequent registry access
        /// </summary>
        private CpuFrequencyCache GetCpuFrequencyCached()
        {
            var currentTime = DateTime.UtcNow;

            // Use cache if available and not expired
            if (_cpuFreqCache != null && 
                (currentTime - _cpuFreqCache.CacheTime).TotalSeconds < _cacheDurationSeconds)
            {
                return _cpuFreqCache;
            }

            var freqData = new CpuFrequencyCache { CpuFreq = 0.0, CpuClock = 0.0 };

            try
            {
                string[] regPaths = {
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                    @"HARDWARE\DESCRIPTION\System\CentralProcessor\1"
                };

                foreach (string regPath in regPaths)
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(regPath))
                        {
                            if (key == null) continue;

                            // Try to get ~MHz value
                            var mhzValue = key.GetValue("~MHz");
                            if (mhzValue != null)
                            {
                                freqData.CpuFreq = Convert.ToDouble(mhzValue);
                                freqData.CpuClock = freqData.CpuFreq;
                                break;
                            }

                            // Fallback: parse ProcessorNameString
                            var nameValue = key.GetValue("ProcessorNameString");
                            if (nameValue != null)
                            {
                                string name = nameValue.ToString() ?? "";
                                var match = Regex.Match(name, @"(\d+\.?\d*)\s*GHz", RegexOptions.IgnoreCase);
                                if (match.Success)
                                {
                                    double ghz = Convert.ToDouble(match.Groups[1].Value);
                                    freqData.CpuFreq = ghz * 1000.0;
                                    freqData.CpuClock = freqData.CpuFreq;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting CPU frequency: {ex.Message}");
            }

            // Update cache
            freqData.CacheTime = currentTime;
            _cpuFreqCache = freqData;

            return freqData;
        }

        private class CpuFrequencyCache
        {
            public double CpuFreq { get; set; }
            public double CpuClock { get; set; }
            public DateTime CacheTime { get; set; }
        }

        #endregion

        #region Memory Methods

        /// <summary>
        /// Gets physical memory information using native API
        /// </summary>
        public MemoryMetrics GetMemoryInfoNative()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));

                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    double totalMb = memStatus.ullTotalPhys / (1024.0 * 1024.0);
                    double availableMb = memStatus.ullAvailPhys / (1024.0 * 1024.0);
                    double usedMb = totalMb - availableMb;

                    return new MemoryMetrics
                    {
                        RamLoad = memStatus.dwMemoryLoad,
                        RamUsed = usedMb,
                        RamTotal = totalMb
                    };
                }
                else
                {
                    Console.WriteLine("GlobalMemoryStatusEx failed, returning default values...");
                    return new MemoryMetrics();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in get_memory_info_native: {ex.Message}");
                return new MemoryMetrics();
            }
        }

        public class MemoryMetrics
        {
            public double RamLoad { get; set; }
            public double RamUsed { get; set; }
            public double RamTotal { get; set; }
        }

        #endregion

        #region System Info Methods

        /// <summary>
        /// Gets additional system information
        /// </summary>
        public SystemInfo GetSystemInfoNative()
        {
            try
            {
                var sysInfo = new SYSTEM_INFO();
                GetSystemInfo(ref sysInfo);

                return new SystemInfo
                {
                    ProcessorCount = sysInfo.dwNumberOfProcessors
                };
            }
            catch
            {
                return new SystemInfo { ProcessorCount = 1 };
            }
        }

        public class SystemInfo
        {
            public uint ProcessorCount { get; set; }
        }

        #endregion

        #region LibreHardwareMonitor Methods

        /// <summary>
        /// Gets CPU metrics including temperature from LibreHardwareMonitor
        /// </summary>
        public CpuMetrics GetCpuMetrics()
        {
            var cpuData = new CpuMetrics
            {
                CpuLoad = GetCpuUsageNative(),
                CpuTemp = 0.0,
                CpuClock = 0.0,
                CpuFreq = 0.0
            };

            // Get frequency from registry cache
            var freqCache = GetCpuFrequencyCached();
            cpuData.CpuFreq = freqCache.CpuFreq;
            cpuData.CpuClock = freqCache.CpuClock;

            // Get temperature and clock from LibreHardwareMonitor
            if (_cpuHardware != null)
            {
                try
                {
                    _cpuHardware.Update();

                    foreach (var sensor in _cpuHardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                        {
                            cpuData.CpuTemp = sensor.Value.Value;
                        }
                        else if (sensor.SensorType == SensorType.Clock && sensor.Name.Contains("CPU"))
                        {
                            if (sensor.Value.HasValue)
                            {
                                cpuData.CpuClock = sensor.Value.Value;
                                cpuData.CpuFreq = sensor.Value.Value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting CPU metrics from LibreHardwareMonitor: {ex.Message}");
                }
            }

            return cpuData;
        }

        public class CpuMetrics
        {
            public double CpuLoad { get; set; }
            public double CpuClock { get; set; }
            public double CpuFreq { get; set; }
            public double CpuTemp { get; set; }
        }

        /// <summary>
        /// Gets GPU metrics from LibreHardwareMonitor
        /// </summary>
        public GpuMetrics GetGpuMetrics()
        {
            var gpuData = new GpuMetrics
            {
                GpuLoad = 0.0,
                GpuClock = 0.0,
                GpuFreq = 0.0,
                GpuTemp = 0.0,
                VramUsed = 0.0,
                VramTotal = 0.0
            };

            if (_gpuHardware != null)
            {
                try
                {
                    _gpuHardware.Update();

                    foreach (var sensor in _gpuHardware.Sensors)
                    {
                        if (!sensor.Value.HasValue) continue;

                        double value = sensor.Value.Value;

                        switch (sensor.SensorType)
                        {
                            case SensorType.Load when sensor.Name.Contains("GPU"):
                                gpuData.GpuLoad = value;
                                break;

                            case SensorType.Temperature when sensor.Name.Contains("GPU"):
                                gpuData.GpuTemp = value;
                                break;

                            case SensorType.Clock when sensor.Name.Contains("GPU") || sensor.Name.Contains("Core"):
                                gpuData.GpuClock = value;
                                gpuData.GpuFreq = value;
                                break;

                            case SensorType.SmallData when sensor.Name.Contains("VRAM") || sensor.Name.Contains("Memory Used"):
                                gpuData.VramUsed = value;
                                break;

                            case SensorType.Data when sensor.Name.Contains("VRAM") || sensor.Name.Contains("Memory Total"):
                                gpuData.VramTotal = value;
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting GPU metrics from LibreHardwareMonitor: {ex.Message}");
                }
            }

            return gpuData;
        }

        public class GpuMetrics
        {
            public double GpuLoad { get; set; }
            public double GpuClock { get; set; }
            public double GpuFreq { get; set; }
            public double GpuTemp { get; set; }
            public double VramUsed { get; set; }
            public double VramTotal { get; set; }
        }

        #endregion

        #region Network Methods

        /// <summary>
        /// Gets network throughput from LibreHardwareMonitor (bytes/s converted to KB/s)
        /// </summary>
        public NetworkMetrics GetNetworkMetrics()
        {
            var netData = new NetworkMetrics();

            if (_networkHardware != null)
            {
                try
                {
                    _networkHardware.Update();

                    foreach (var sensor in _networkHardware.Sensors)
                    {
                        if (!sensor.Value.HasValue) continue;
                        if (sensor.SensorType != SensorType.Throughput) continue;

                        double kbPerSec = sensor.Value.Value / 1024.0;

                        if (sensor.Name.Contains("Upload"))
                            netData.NetUpload = kbPerSec;
                        else if (sensor.Name.Contains("Download"))
                            netData.NetDownload = kbPerSec;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error getting network metrics from LibreHardwareMonitor: {ex.Message}");
                }
            }

            return netData;
        }

        public class NetworkMetrics
        {
            public double NetUpload { get; set; }
            public double NetDownload { get; set; }
        }

        #endregion

        #region All Metrics

        /// <summary>
        /// Gets all system metrics
        /// </summary>
        public AllMetrics GetAllMetrics()
        {
            var metrics = new AllMetrics();

            var cpuMetrics = GetCpuMetrics();
            metrics.CpuLoad = cpuMetrics.CpuLoad;
            metrics.CpuClock = cpuMetrics.CpuClock;
            metrics.CpuFreq = cpuMetrics.CpuFreq;
            metrics.CpuTemp = cpuMetrics.CpuTemp;

            var gpuMetrics = GetGpuMetrics();
            metrics.GpuLoad = gpuMetrics.GpuLoad;
            metrics.GpuClock = gpuMetrics.GpuClock;
            metrics.GpuFreq = gpuMetrics.GpuFreq;
            metrics.GpuTemp = gpuMetrics.GpuTemp;
            metrics.VramUsed = gpuMetrics.VramUsed;
            metrics.VramTotal = gpuMetrics.VramTotal;

            var memMetrics = GetMemoryInfoNative();
            metrics.RamLoad = memMetrics.RamLoad;
            metrics.RamUsed = memMetrics.RamUsed;
            metrics.RamTotal = memMetrics.RamTotal;

            var netMetrics = GetNetworkMetrics();
            metrics.NetUpload = netMetrics.NetUpload;
            metrics.NetDownload = netMetrics.NetDownload;

            metrics.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return metrics;
        }

        public class AllMetrics
        {
            public double CpuLoad { get; set; }
            public double CpuClock { get; set; }
            public double CpuFreq { get; set; }
            public double CpuTemp { get; set; }
            public double GpuLoad { get; set; }
            public double GpuClock { get; set; }
            public double GpuFreq { get; set; }
            public double GpuTemp { get; set; }
            public double VramUsed { get; set; }
            public double VramTotal { get; set; }
            public double RamLoad { get; set; }
            public double RamUsed { get; set; }
            public double RamTotal { get; set; }
            public double NetUpload { get; set; }
            public double NetDownload { get; set; }
            public string Timestamp { get; set; } = "";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _computer?.Close();
        }

        #endregion
    }
}
