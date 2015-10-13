/* Copyright (C) 2015, Manuel Meitinger
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
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Aufbauwerk.Net.Asterisk;
using BLF.Properties;

namespace BLF
{
    [Flags]
    internal enum ExtensionStates
    {
        Removed = -2,	/*!< Extension removed */
        Deactivated = -1,	/*!< Extension hint removed */
        NotInUse = 0,	/*!< No device INUSE or BUSY  */
        InUse = 1 << 0,	/*!< One or more devices INUSE */
        Busy = 1 << 1,	/*!< All devices BUSY */
        Unavailable = 1 << 2, /*!< All devices UNAVAILABLE/UNREGISTERED */
        Ringing = 1 << 3,	/*!< All devices RINGING */
        OnHold = 1 << 4,	/*!< All devices ONHOLD */
    }

    internal class Extension : INotifyPropertyChanged
    {
        private static readonly Dictionary<Extension, List<object>> registrations = new Dictionary<Extension, List<object>>();
        private static readonly BindingList<Extension> all = new BindingList<Extension>();

        public static ExtensionStates ParseStatus(string status)
        {
            // parses an extensions status
            int i;
            if (!int.TryParse(status, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
                throw new AsteriskException("Extension status is not a valid number.");
            return (ExtensionStates)i;
        }

        public static string FormatState(ExtensionStates state)
        {
            // convert the extension state to a device state string
            switch (state)
            {
                case ExtensionStates.Removed: return "INVALID";
                case ExtensionStates.Deactivated: return "UNKNOWN";
                case ExtensionStates.OnHold: return "ONHOLD";
                case ExtensionStates.Busy: return "BUSY";
                case ExtensionStates.Unavailable: return "UNAVAILABLE";
                case ExtensionStates.Ringing | ExtensionStates.InUse: return "RINGINUSE";
                case ExtensionStates.Ringing: return "RINGING";
                case ExtensionStates.InUse: return "INUSE";
                case ExtensionStates.NotInUse: return "NOT_INUSE";
                default:
                    return "UNKNOWN";
            }
        }

        public static IEnumerable<Extension> All { get { return all; } }

        public static event Action<Extension> StateChanged;

        public static Extension FromNumber(string number, object registrant)
        {
            if (number == null)
                throw new ArgumentNullException("number");
            if (registrant == null)
                throw new ArgumentNullException("registrant");

            // find the extension with the given number or create one and add the registrant
            var extension = all.SingleOrDefault(e => e.Number == number);
            if (extension == null)
            {
                extension = new Extension(number);
                registrations.Add(extension, new List<object>() { registrant });
                all.Add(extension);
            }
            else
            {
                var registrants = registrations[extension];
                if (!registrants.Contains(registrant))
                    registrants.Add(registrant);
            }
            return extension;
        }

        public static void FreeAll(object registrant)
        {
            if (registrant == null)
                throw new ArgumentNullException("registrant");

            // remove all registrations
            var obsoleteExtensions = new List<Extension>();
            foreach (var registration in registrations)
            {
                if (registration.Value.Remove(registrant) && registration.Value.Count == 0)
                    obsoleteExtensions.Add(registration.Key);
            }
            foreach (var extension in obsoleteExtensions)
            {
                registrations.Remove(extension);
                all.Remove(extension);
            }
        }

        private ExtensionStates state = ExtensionStates.Removed;

        private Extension(string number)
        {
            Number = number;
        }

        public string Number { get; private set; }

        public ExtensionStates State
        {
            get
            {
                return state;
            }
            set
            {
                // set the new state and notify the listeners
                if (state != value)
                {
                    state = value;
                    var e = PropertyChanged;
                    if (e != null)
                        e(this, new PropertyChangedEventArgs("State"));
                    if (StateChanged != null)
                        StateChanged(this);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    internal class ExtensionSyncClient : AsteriskClient
    {
        private readonly Queue<Extension> pendingExtensions = new Queue<Extension>();
        private bool loggedIn = false;
        private string prefix = null;
        private string[] dialPlan = null;
        private IAsyncResult login = null;
        private IAsyncResult getPrefix = null;
        private IAsyncResult showDialPlan = null;
        private IAsyncResult extensionState = null;
        private IAsyncResult waitEvent = null;
        private IAsyncResult setDeviceState = null;
        private Timer start = null;

        private void SafeCancelExecute(ref IAsyncResult asyncResult)
        {
            // cancel and reset an existing async operation
            if (asyncResult != null)
            {
                CancelExecute(asyncResult);
                asyncResult = null;
            }
        }

        private bool SafeEndExecute<T>(ref IAsyncResult currentAsyncResult, IAsyncResult completedAsyncResult, Func<IAsyncResult, T> endMethod, out T result)
        {
            // safely end a no longer needed async operation
            result = default(T);
            if (currentAsyncResult != completedAsyncResult)
            {
                try { endMethod(completedAsyncResult); }
                catch (OperationCanceledException) { }
                catch (WebException) { }
                catch (AsteriskException) { }
                return false;
            }

            // reset the current operation and handle the result
            currentAsyncResult = null;
            try { result = endMethod(completedAsyncResult); }
            catch (WebException e)
            {
                // stop, show a warning and start again later
                Stop();
                Program.ShowMessage(BaseUri.ToString(), e.Message, MessageType.Warning);
                StartAsync(Settings.Default.RetryNetwork);
                return false;
            }
            catch (AsteriskException e)
            {
                // stop, show an error and start again later
                Stop();
                Program.ShowMessage(BaseUri.ToString(), e.Message, MessageType.Error);
                StartAsync(Settings.Default.RetryManager);
                return false;
            }
            return true;
        }

        private bool SafeEndExecute(ref IAsyncResult currentAsyncResult, IAsyncResult completedAsyncResult, Action<IAsyncResult> endMethod)
        {
            // slightly inefficient but better than code duplication
            bool dummy;
            return SafeEndExecute(ref currentAsyncResult, completedAsyncResult, r => { endMethod(r); return true; }, out dummy) && dummy;
        }

        private void StartAsync(TimeSpan waitTime)
        {
            start = new Timer(StartComplete);
            start.Change(waitTime, TimeSpan.Zero);
        }

        private void StartComplete(object state)
        {
            Program.Synced(() =>
            {
                // continue with start if not cancelled
                if (start == (Timer)state)
                {
                    start.Dispose();
                    start = null;
                    Start();
                }
            });
        }

        internal void Start()
        {
            // checks due to internal
            if (loggedIn || start != null || login != null)
                throw new InvalidOperationException();

            // start with login
            LoginAsync();
        }

        internal void Stop()
        {
            // cancel a pending start
            if (start != null)
            {
                start.Dispose();
                start = null;
            }

            // cancel all other async operations
            SafeCancelExecute(ref login);
            SafeCancelExecute(ref getPrefix);
            SafeCancelExecute(ref showDialPlan);
            SafeCancelExecute(ref extensionState);
            SafeCancelExecute(ref waitEvent);
            SafeCancelExecute(ref setDeviceState);

            // remove all known states
            loggedIn = false;
            prefix = null;
            dialPlan = null;

            // clear all registered extensions
            Extension.FreeAll(this);
        }

        private void LoginAsync()
        {
            login = BeginExecuteNonQuery(new AsteriskAction("Login") { { "Username", Settings.Default.Username }, { "Secret", Settings.Default.Secret } }, LoginComplete, null);
        }

        private void LoginComplete(IAsyncResult asyncResult)
        {
            Program.Synced(() =>
            {
                if (!(loggedIn = SafeEndExecute(ref login, asyncResult, EndExecuteNonQuery)))
                    return;

                // query the extensions and prefix
                ShowDialPlanAsync();
                GetPrefixAsync();

                // start waiting for events
                WaitEventAsync();

                // enqueue all existing extensions and start syncing
                foreach (var exten in Extension.All)
                    if (!pendingExtensions.Contains(exten))
                        pendingExtensions.Enqueue(exten);
                if (pendingExtensions.Count > 0)
                    SetDeviceStateAsync(pendingExtensions.Dequeue());
            });
        }

        private void ExtensionStateAsync(int index)
        {
            extensionState = BeginExecuteScalar(new AsteriskAction("ExtensionState") { { "Context", Settings.Default.ExtensionsContext }, { "Exten", dialPlan[index] } }, "Status", ExtensionStateComplete, index);
        }

        private void ExtensionStateComplete(IAsyncResult asyncResult)
        {
            Program.Synced(() =>
            {
                ExtensionStates state;
                if (!SafeEndExecute(ref extensionState, asyncResult, ar => Extension.ParseStatus(EndExecuteScalar(ar)), out state))
                    return;

                // set the state
                var index = (int)asyncResult.AsyncState;
                Extension.FromNumber(prefix + dialPlan[index], this).State = state;

                // move to the next extension
                if (++index < dialPlan.Length)
                    ExtensionStateAsync(index);
            });
        }

        private void WaitEventAsync()
        {
            waitEvent = BeginExecuteEnumeration(new AsteriskAction("WaitEvent") { }, WaitEventComplete, null);
        }

        private void WaitEventComplete(IAsyncResult asyncResult)
        {
            Program.Synced(() =>
            {
                KeyValuePair<string, ExtensionStates>[] result;
                if (!SafeEndExecute(ref waitEvent, asyncResult, ar =>
                    EndExecuteEnumeration(ar)
                        .Where(r =>
                            string.Equals(r["Event"], "ExtensionStatus", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(r["Context"], Settings.Default.ExtensionsContext) &&
                            Regex.IsMatch(r["Exten"], Settings.Default.ExtensionsPattern))
                        .Select(r => new KeyValuePair<string, ExtensionStates>(r["Exten"], Extension.ParseStatus(r["Status"])))
                        .ToArray(), out result))
                    return;

                // handle the events and wait again
                if (prefix != null)
                    foreach (var exten in result)
                        Extension.FromNumber(prefix + exten.Key, this).State = exten.Value;
                WaitEventAsync();
            });
        }

        private void GetPrefixAsync()
        {
            getPrefix = BeginExecuteScalar(new AsteriskAction("GetVar") { { "Variable", Settings.Default.PrefixVariable } }, "Value", GetPrefixComplete, null);
        }

        private void GetPrefixComplete(IAsyncResult asyncResult)
        {
            Program.Synced(() =>
            {
                if (!SafeEndExecute(ref getPrefix, asyncResult, EndExecuteScalar, out prefix))
                    return;

                // set the extensions
                if (dialPlan != null && dialPlan.Length > 0)
                    ExtensionStateAsync(0);
            });
        }

        private void ShowDialPlanAsync()
        {
            showDialPlan = BeginExecuteEnumeration(new AsteriskAction("ShowDialPlan") { { "Context", Settings.Default.ExtensionsContext } }, ShowDialPlanComplete, null);
        }

        private void ShowDialPlanComplete(IAsyncResult asyncResult)
        {
            Program.Synced(() =>
            {
                if (!SafeEndExecute(ref showDialPlan, asyncResult, ar => EndExecuteEnumeration(ar).Where(e => string.Equals(e["Priority"], "hint", StringComparison.OrdinalIgnoreCase)).Select(e => e["Extension"]).Where(e => Regex.IsMatch(e, Settings.Default.ExtensionsPattern)).ToArray(), out dialPlan))
                    return;

                // set the extensions
                if (dialPlan.Length > 0 && prefix != null)
                    ExtensionStateAsync(0);
            });
        }

        private void SetDeviceStateAsync(Extension extension)
        {
            setDeviceState = BeginExecuteNonQuery(new AsteriskAction("SetVar") { { "Variable", string.Format("DEVICE_STATE(Custom:{0})", extension.Number) }, { "Value", Extension.FormatState(extension.State) } }, SetDeviceStateComplete, null);
        }

        private void SetDeviceStateComplete(IAsyncResult asyncResult)
        {
            Program.Synced(() =>
            {
                if (!SafeEndExecute(ref setDeviceState, asyncResult, EndExecuteNonQuery))
                    return;

                // continue with the next
                if (pendingExtensions.Count > 0)
                    SetDeviceStateAsync(pendingExtensions.Dequeue());
            });
        }

        public ExtensionSyncClient(Uri serverUri)
            : base(serverUri)
        {
            // attach the event listener
            Extension.StateChanged += UpdateExtension;
        }

        private void UpdateExtension(Extension extension)
        {
            // enqueue the extension
            if (!pendingExtensions.Contains(extension))
            {
                pendingExtensions.Enqueue(extension);

                // sync it if not doing already so
                if (loggedIn && pendingExtensions.Count > 0 && setDeviceState == null)
                    SetDeviceStateAsync(pendingExtensions.Dequeue());
            }
        }
    }
}
