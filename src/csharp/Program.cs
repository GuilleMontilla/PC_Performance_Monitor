/*
  Script Name : Program.cs
  Project     : PC Performance Monitor 
  Author      : Guillermo A. Montilla Ramos
  Contact     : guillermomontilla111@outlook.com
  Created     : 2025-01-XX
  Updated     : 2025-01-XX

  Description:
    Main entry point for the PC Performance Monitor application.
    Supports two modes:
      - Terminal mode (default): prints metrics to console.
      - Serial / Arduino mode (--serial or --arduino): streams packed
        binary frames to an Arduino over a COM port.

  Usage:
    PCPerformanceMonitor.exe                     (terminal mode)
    PCPerformanceMonitor.exe --serial            (auto-detect COM port)
    PCPerformanceMonitor.exe --serial COM3       (explicit port)
    PCPerformanceMonitor.exe --serial --verbose  (serial + terminal echo)
*/
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace PCPerformanceMonitor
{
    class Program
    {
        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsUserAnAdmin();

        static void Main(string[] args)
        {
            const double UPDATE_INTERVAL = 1.0;

            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("   PC Performance Monitor   ");
            Console.WriteLine("=".PadRight(60, '='));

            try
            {
                bool isAdmin = IsUserAnAdmin();
                string status = isAdmin ? "Administrator" : "User (limited sensors)";
                Console.WriteLine($"Privileges: {status}");
            }
            catch
            {
                Console.WriteLine("Privileges: Unknown");
            }

            try
            {
                Console.WriteLine($"System: {Environment.OSVersion}");
                Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
            }
            catch { }

            bool serialMode = args.Any(a => a.Equals("--serial", StringComparison.OrdinalIgnoreCase)
                                          || a.Equals("--arduino", StringComparison.OrdinalIgnoreCase));
            bool verbose = args.Any(a => a.Equals("--verbose", StringComparison.OrdinalIgnoreCase)
                                       || a.Equals("-v", StringComparison.OrdinalIgnoreCase));

            if (serialMode)
            {
                string? comPort = ResolveComPort(args);
                Console.WriteLine($"Mode: Serial (port: {comPort ?? "auto-detect"})");
                Console.WriteLine($"Interval: {UPDATE_INTERVAL}s");
                Console.WriteLine("-".PadRight(60, '-'));

                var monitor = new SerialMonitorApp(comPort, UPDATE_INTERVAL, verbose);

                Console.CancelKeyPress += (_, e) => { e.Cancel = true; monitor.Stop(); };

                try  { monitor.StartMonitoring(); }
                catch (Exception ex) { Console.WriteLine($"\nCritical error: {ex.Message}"); }
                finally { monitor.Stop(); Console.WriteLine("Program terminated."); }
            }
            else
            {
                Console.WriteLine("Mode: Terminal");
                Console.WriteLine($"Interval: {UPDATE_INTERVAL}s");
                Console.WriteLine("-".PadRight(60, '-'));

                var monitor = new TerminalMonitorApp(UPDATE_INTERVAL);

                Console.CancelKeyPress += (_, e) => { e.Cancel = true; monitor.Stop(); };

                try  { monitor.StartMonitoring(); }
                catch (Exception ex) { Console.WriteLine($"\nCritical error: {ex.Message}"); }
                finally { monitor.Stop(); Console.WriteLine("Program terminated."); }
            }
        }

        /// <summary>
        /// Looks for a COMx token right after --serial/--arduino in the arg list.
        /// Returns null when no explicit port is given (triggers auto-detect).
        /// </summary>
        private static string? ResolveComPort(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--serial", StringComparison.OrdinalIgnoreCase)
                 || args[i].Equals("--arduino", StringComparison.OrdinalIgnoreCase))
                {
                    string next = args[i + 1];
                    if (!next.StartsWith("-"))
                        return next;
                }
            }

            var ports = ArduinoSerialBridge.GetAvailablePorts();
            if (ports.Length > 0)
            {
                Console.WriteLine("Available COM ports:");
                for (int i = 0; i < ports.Length; i++)
                    Console.WriteLine($"  [{i}] {ports[i]}");
            }

            return null;
        }
    }
}
