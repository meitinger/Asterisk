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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aufbauwerk.Asterisk.Blf
{
    internal class GetExtensionStateService : AstersikService
    {
        public GetExtensionStateService(Settings.Server server) : base("BLF-in", server) { }

        protected override IEnumerable<string> EventFilter { get; } = new string[] { "Event: ExtensionStatus" };

        private async Task<Dictionary<string, DeviceState>> GetUpdatesAsync(AsteriskClient client, string name, CancellationToken cancellationToken) => (await client.ExecuteEnumerationAsync(new(name), cancellationToken))
            .Where(r => Regex.IsMatch(r["Exten"], Server.ExtensionPattern))
            .ToLookup(
                r => Regex.Replace(r["Exten"], Server.ExtensionPattern, Server.DeviceFormat),
                r => (Enum.TryParse<ExtensionState>(r["Status"], out var state) ? state : ExtensionState.NOT_INUSE).ToDeviceState())
            .ToDictionary(l => l.Key, l => l.Last());

        protected override async Task RunAsync(AsteriskClient client, CancellationToken cancellationToken)
        {
            // forward the initial extension states and any changes to the global DeviceStates class
            await DeviceStates.Update(await GetUpdatesAsync(client, "ExtensionStateList", cancellationToken), cancellationToken);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DeviceStates.Update(await GetUpdatesAsync(client, "WaitEvent", cancellationToken), cancellationToken);
            }
        }
    }

    internal class SetDeviceStateService : AstersikService
    {
        public SetDeviceStateService(Settings.Server server) : base("BLF-out", server) { }

        protected override async Task RunAsync(AsteriskClient client, CancellationToken cancellationToken)
        {
            // get the initial device states and continuously forward updates to the Asterisk
            DeviceStates.Entry? update = null;
            var states = (await client.ExecuteEnumerationAsync(new("DeviceStateList"), cancellationToken))
                .ToLookup(r => r["Device"], r => Enum.TryParse<DeviceState>(r["State"], out var state) ? state : DeviceState.UNKNOWN)
                .ToDictionary(l => l.Key, l => l.Last());
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // wait for new updates while keeping the connection open
                update = await client.WaitAndKeepAliveAsync(DeviceStates.GetChanged(update, states, cancellationToken));

                // update the state on Asterisk and the local cache
                await client.ExecuteNonQueryAsync(new("Setvar")
                {
                    { "Variable", string.Format("DEVICE_STATE({0})", update.Device) },
                    { "Value", update.State.ToString() }
                }, cancellationToken);
                states[update.Device] = update.State;
            }
        }
    }
}
