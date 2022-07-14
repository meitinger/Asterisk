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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aufbauwerk.Asterisk
{
    internal abstract class Service
    {
        public Service(string name) => Name = name;

        public static IEnumerable<Service> All
        {
            get
            {
                yield return Sms.SendService.Instance.Main;
                foreach (var server in Settings.Instance.Servers)
                {
                    yield return new Sms.MissedCallsService(server);
                    yield return new Blf.GetExtensionStateService(server);
                    yield return new Blf.SetDeviceStateService(server);
                }
            }
        }

        public string Name { get; }

        protected internal void LogEvent(EventLogEntryType type, string message) => Program.LogEvent(type, $"[{Name}] {message}");

        public abstract Task RunAsync(CancellationToken cancellationToken);
    }

    internal abstract class AstersikService : Service
    {
        public AstersikService(string name, Settings.Server server) : base($"{name}@{server}") => Server = server;

        protected virtual IEnumerable<string> EventFilter { get; } = Enumerable.Empty<string>();

        public Settings.Server Server { get; }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // login to Asterisk and run the actual service
                    using var client = new AsteriskClient(Server.Host, Server.Port, Server.Prefix) { Timeout = Server.Timeout };
                    await client.ExecuteNonQueryAsync(new("Login")
                    {
                        { "Username", Server.Username },
                        { "Secret", Server.Secret }
                    }, cancellationToken);
                    try
                    {
                        LogEvent(EventLogEntryType.Information, "Logged in to AMI.");
                        foreach (var filter in EventFilter) await client.ExecuteNonQueryAsync(new("Filter")
                        {
                            { "Operation", "Add" },
                            { "Filter", filter }
                        }, cancellationToken);
                        await RunAsync(client, cancellationToken);
                        LogEvent(EventLogEntryType.Warning, "Service ended unexpectedly.");
                    }
                    finally
                    {
                        // always try to logout, but ignore errors
                        try
                        {
                            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                            cancellationTokenSource.CancelAfter(Server.ShutdownTimeLimit);
                            await client.ExecuteNonQueryAsync(new("Logoff"), cancellationTokenSource.Token);
                            LogEvent(EventLogEntryType.Information, "Logged off from AMI.");
                        }
                        catch { }
                    }
                }
                catch (HttpRequestException e) { LogEvent(EventLogEntryType.Warning, $"HTTP error: {e}"); }
                catch (AsteriskException e) { LogEvent(EventLogEntryType.Warning, $"AMI error: {e}"); }
                await Task.Delay(Server.RetryInterval, cancellationToken);
            }
        }

        protected abstract Task RunAsync(AsteriskClient client, CancellationToken cancellationToken);
    }
}
