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
using System.Threading;
using System.Threading.Tasks;

namespace Aufbauwerk.Asterisk.Blf
{
    internal static class DeviceStates
    {
        public class Entry
        {
            private static readonly Dictionary<string, Entry> _map = new();

            public static Entry? First { get; private set; }

            public static Entry? Last { get; private set; }

            public static void Register(string device, DeviceState state)
            {
                var entry = new Entry(device, state);
                if (First is null && Last is null)
                {
                    // set the first update
                    Debug.Assert(_map.Count == 0);
                    First = entry;
                    Last = entry;
                }
                else if (First is not null && Last is not null)
                {
                    // add the update to the end
                    Debug.Assert(Last.Next is null);
                    Last.Next = entry;
                    entry.Prev = Last;
                    Last = entry;
                    if (_map.TryGetValue(device, out var existingEntry))
                    {
                        // don't enumerate the existing entry anymore
                        if (existingEntry.Prev is null)
                        {
                            Debug.Assert(First == existingEntry);
                            First = existingEntry.Next;
                        }
                        else existingEntry.Prev.Next = existingEntry.Next;

                        // replace the existing entry
                        _map[device] = entry;
                    }
                }
                else Debug.Assert(false);
            }

            private Entry(string device, DeviceState state)
            {
                Device = device;
                State = state;
            }

            public string Device { get; }

            public Entry? Next { get; private set; }

            public Entry? Prev { get; private set; }

            public DeviceState State { get; }
        }

        private static readonly SemaphoreSlim _lock = new(1, 1);
        private static readonly Queue<TaskCompletionSource<bool>> _notifiers = new();

        public static async Task<Entry> GetChanged(Entry? lastUpdate, IDictionary<string, DeviceState> states, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TaskCompletionSource<bool> taskCompletionSource;

                // acquire the lock
                await _lock.WaitAsync(cancellationToken);
                try
                {
                    // try find the next applicable update
                    var nextUpdate = lastUpdate is null ? Entry.First : lastUpdate.Next;
                    while (nextUpdate is not null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!states.TryGetValue(nextUpdate.Device, out var currentState) || currentState != nextUpdate.State) return nextUpdate;
                        nextUpdate = nextUpdate.Next;
                    }

                    // prepare to wait for more updates
                    taskCompletionSource = new();
                    cancellationToken.Register(() => taskCompletionSource.TrySetCanceled(cancellationToken), useSynchronizationContext: false);
                    _notifiers.Enqueue(taskCompletionSource);
                }
                finally { _lock.Release(); }

                // wait for next updates to arrive
                await taskCompletionSource.Task;
            }
        }

        public static async Task Update(IDictionary<string, DeviceState> updates, CancellationToken cancellationToken)
        {
            // acquire the lock
            await _lock.WaitAsync(cancellationToken);
            try
            {
                // register updates
                foreach (var entry in updates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Entry.Register(entry.Key, entry.Value);
                }

                // signal success to all non-canceled waiters
                while (_notifiers.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _notifiers.Dequeue().TrySetResult(true);
                }
            }
            finally { _lock.Release(); }
        }
    }
}
