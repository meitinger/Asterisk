/* Copyright (C) 2015-2022, Manuel Meitinger
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace Aufbauwerk.Asterisk
{
    internal class Program : ServiceBase
    {
        private static readonly Program _instance = new();

        private static void Exit(Exception? e, int defaultExitCode)
        {
            // exit with an error code that is not zero
            var exitCode = e is null ? 0 : Marshal.GetHRForException(e);
            Environment.Exit(exitCode == 0 ? defaultExitCode : exitCode);
        }

        private static void HandleServiceEnded(Service service, Task task, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (task.IsFaulted) service.LogEvent(EventLogEntryType.Error, $"Service failed on shutdown: {task.Exception}");
            }
            else
            {
                service.LogEvent(EventLogEntryType.Error, task.Status switch
                {
                    TaskStatus.Faulted => $"Service failed: {task.Exception}",
                    TaskStatus.Canceled => $"Service got canceled unexpectedly.",
                    TaskStatus.RanToCompletion => $"Service ended unexpectedly.",
                    _ => $"Service ended in invalid state: {task.Status}"
                });
                Exit(task.Exception, -2147483641 /* E_ABORT */);
            }
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogEvent(EventLogEntryType.Error, $"Unhandled exception: {e.ExceptionObject}");
            if (e.IsTerminating) Exit(e.ExceptionObject as Exception, -2147418113 /* E_UNEXPECTED */);
        }

        private static void HandleUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogEvent(EventLogEntryType.Error, $"Unobserved task exception: {e.Exception}");
            if (!e.Observed) Exit(e.Exception, -2147483640 /* E_FAIL */);
        }

        internal static void LogEvent(EventLogEntryType type, string message) =>
#if DEBUG
            Console.WriteLine($"[{type}] {message}");
#else
            _instance.EventLog.WriteEntry(message, type);
#endif


        public static void Main(string[] args)
        {
            // handle all uncaught exceptions
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

#if DEBUG
            do
            {
                Console.WriteLine("Starting...");
                _instance.OnStart(args);
                Console.WriteLine("Started. Press ENTER to stop.");
                if (Console.ReadLine() is null) break;
                Console.WriteLine("Stopping...");
                _instance.OnStop();
                Console.WriteLine("Stopped. Press ENTER to start.");
            }
            while (Console.ReadLine() is not null);
#else
            ServiceBase.Run(_instance);
#endif
        }

        private (CancellationTokenSource, Task)? _runState;

        private Program()
        {
            // specify the service name and inform SCM that the service can be stopped
            ServiceName = Settings.Instance.ServiceName;
            CanStop = true;
        }

        protected override void OnStart(string[] args)
        {
            // set a non-zero exit code to indicate failure in case something goes wrong during start
            ExitCode = ~0;
            if (_runState.HasValue) throw new InvalidOperationException("Service already started.");
            CancellationTokenSource cancellationTokenSource = new();
            _runState = (cancellationTokenSource, Task.WhenAll(Service.All.Select(service => service.RunAsync(cancellationTokenSource.Token).ContinueWith(task => HandleServiceEnded(service, task, cancellationTokenSource.Token)))));
        }

        protected override void OnStop()
        {
            // stop and reset the exit code to indicate success
            if (!_runState.HasValue) throw new InvalidOperationException("Service already stopped.");
            var (cancellationTokenSource, task) = _runState.Value;
            cancellationTokenSource.Cancel();
            task.Wait();
            cancellationTokenSource.Dispose();
            _runState = null;
            ExitCode = 0;
        }
    }
}
