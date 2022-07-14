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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aufbauwerk.Asterisk.Sms
{
    internal class MissedCallsService : AstersikService
    {
        private static readonly Regex MissedCallRegex = new(@"^(?<shortcode>\d+)\|(?<caller>\+\d+)\|(?<time>\d+)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

        private static async Task<long> GetNumberAsync(AsteriskClient client, string key, CancellationToken cancellationToken) => long.TryParse(await GetValueAsync(client, key, cancellationToken), out var value) ? value : throw new AsteriskException($"Key '{key}' does not contain a numeric value.");

        private static async Task<string> GetValueAsync(AsteriskClient client, string key, CancellationToken cancellationToken) => (await client.ExecuteEnumerationAsync(new("DBGet")
        {
            { "Family", "MissedCalls" },
            { "Key", key },
        }, cancellationToken)).First().Get("Val");

        private static Task SetValueAsync(AsteriskClient client, string key, string value, CancellationToken cancellationToken) => client.ExecuteNonQueryAsync(new("DBPut")
        {
            { "Family", "MissedCalls" },
            { "Key", key },
            { "Val", value },
        }, cancellationToken);

        public MissedCallsService(Settings.Server server) : base("SMS-missed-calls", server) { }

        protected override IEnumerable<string> EventFilter { get; } = new string[] { "Event: UserEvent", "UserEvent: MissedCall" };

        protected override async Task RunAsync(AsteriskClient client, CancellationToken cancellationToken)
        {
            // get the last sent SMS index
            var first = await GetNumberAsync(client, "First", cancellationToken);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // get the most recent missed call index
                var last = await GetNumberAsync(client, "Last", cancellationToken);
                if (first < last)
                {
                    // parse the next missed call to send
                    first++;
                    var missedCall = MissedCallRegex.Match(await GetValueAsync(client, first.ToString(CultureInfo.InvariantCulture), cancellationToken));
                    if (missedCall.Success && long.TryParse(missedCall.Groups["time"].Value, out var unixTime))
                    {
                        var shortcode = missedCall.Groups["shortcode"].Value;
                        var caller = missedCall.Groups["caller"].Value;
                        var time = new DateTime(621355968000000000 + unixTime * TimeSpan.TicksPerSecond, DateTimeKind.Utc).ToLocalTime();

                        // try to send the missed call notification with multiple attempts
                        for (int attempt = 0; attempt <= Settings.Instance.Sms.Retries; attempt++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // add a one second delay between every notification and attempt
                            await Task.Delay(1000, cancellationToken);

                            // ignore the missed call after some time
                            if (DateTime.Now - time > Settings.Instance.Sms.MaximumAge)
                            {
                                LogEvent(EventLogEntryType.Information, $"Ignore missed call #{first} from {time}.");
                                break;
                            }

                            // get a hold of the instance and send the text
                            using var instance = await client.WaitAndKeepAliveAsync(SendService.Instance.CreateAsync(cancellationToken));
                            if (await instance.SendTextAsync(shortcode, string.Format(Settings.Instance.Sms.TextTemplate, caller, time), cancellationToken)) break;
                        }
                    }
                    else LogEvent(EventLogEntryType.Warning, $"Missed call #{first} has an invalid format.");

                    // update the counter
                    await SetValueAsync(client, "First", first.ToString(CultureInfo.InvariantCulture), cancellationToken);
                }

                // wait for new missed calls
                else await client.ExecuteEnumerationAsync(new("WaitEvent"), cancellationToken);
            }
        }
    }

    internal class SendService : Service
    {
        public sealed class Instance : IDisposable
        {
            public static Service Main => _instance;

            public static async Task<Instance> CreateAsync(CancellationToken cancellationToken)
            {
                // acquire the lock to the client
                Instance instance = new();
                await _lock.WaitAsync(cancellationToken);
                return instance;
            }

            private bool _disposed = false;

            private Instance() { }

            public async Task<bool> SendTextAsync(string shortcode, string text, CancellationToken cancellationToken)
            {
                // lookup the recipient
                if (_disposed) throw new ObjectDisposedException(GetType().FullName);
                if (!_suggestionItems.TryGetValue(shortcode, out var suggestedItem))
                {
                    _instance.LogEvent(EventLogEntryType.Information, $"Short code '{shortcode}' not found.");
                    return true;
                }

                // send the text
                try
                {
                    using var response = await _client.PostAsync($"https://businessportal.magenta.at/services/messaging/sendSms?vpnId={Uri.EscapeDataString(Settings.Instance.Sms.VpnId)}", new SendSmsRequest(text, suggestedItem).Serialize(), cancellationToken);
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) _waitAccessTokenRefreshCts.Cancel();
                    var responseContent = await response.EnsureSuccessStatusCode().Content.Deserialize<SendSmsResponse>();
                    if (responseContent.NumberOfReceivers == 0) _instance.LogEvent(EventLogEntryType.Warning, $"Cannot send to recipient '{suggestedItem.DisplayName}'.");
                    else _instance.LogEvent(EventLogEntryType.Information, $"Sent text to '{suggestedItem.DisplayName}' from '{responseContent.SenderName}'.");
                    return true;
                }
                catch (HttpRequestException e)
                {
                    _instance.LogEvent(EventLogEntryType.Warning, $"Sending text to '{suggestedItem.DisplayName}' failed: {e}");
                    return false;
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _lock.Release();
                }
            }
        }

        private static readonly HttpClient _client = new();
        private static readonly SendService _instance = new();
        private static readonly SemaphoreSlim _lock = new(0, 1); // the service has first dibs
        private static readonly Dictionary<string, SuggestionItem> _suggestionItems = new();
        private static CancellationTokenSource _waitAccessTokenRefreshCts = new();

        private SendService() : base("SMS-send") { }

        private async Task RefreshShortcodeDirectoryAsync(CancellationToken cancellationToken)
        {
            // rebuild the phone directory
            using var response = await _client.GetAsync($"https://businessportal.magenta.at/services/messaging/uiData?custCode={Uri.EscapeDataString(Settings.Instance.Sms.CustomerCode)}&lang=en&vpnId={Uri.EscapeDataString(Settings.Instance.Sms.VpnId)}", cancellationToken);
            var uiData = await response.EnsureSuccessStatusCode().Content.Deserialize<UIData>();
            _suggestionItems.Clear();
            foreach (var item in uiData.SuggestionItems.Where(item => item.Type == SuggestionItemType.SHORTCODE))
            {
                if (_suggestionItems.TryGetValue(item.SearchTerm, out var existingItem)) LogEvent(EventLogEntryType.Warning, $"Duplicate short-code '{item.SearchTerm}', kept '{existingItem.DisplayName}' and ignored '{item.DisplayName}'.");
                else _suggestionItems.Add(item.SearchTerm, item);
            }
            LogEvent(EventLogEntryType.Information, $"Retrieved {_suggestionItems.Count} phone numbers and sender name '{uiData.SenderName}'.");
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // upon entering we do have the lock
                AccessToken token;
                try
                {
                    // login and prepare all HTTP related parts
                    var code = await _client.GrantAuthorizationCodeAsync(cancellationToken);
                    token = await _client.IssueAccessTokenAsync(code, cancellationToken);
                    await RefreshShortcodeDirectoryAsync(cancellationToken);
                }
                catch (HttpRequestException e)
                {
                    CheckCancellationAndLogException(e);

                    // wait for a bit and retry login
                    await Task.Delay(Settings.Instance.Sms.LoginRetryInterval, cancellationToken);
                    continue;
                }

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // recreate the cancellation source
                    _waitAccessTokenRefreshCts.Dispose();
                    _waitAccessTokenRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    // release the lock to allow sending
                    _lock.Release();
                    try
                    {
                        // either wait for 9/10th of the expiration time or until a sender encounters a 401
                        try { await Task.Delay(token.ExpiresIn.HasValue ? (token.ExpiresIn.Value * 900) : Timeout.Infinite, _waitAccessTokenRefreshCts.Token); }
                        catch (OperationCanceledException) when (_waitAccessTokenRefreshCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested) { }
                    }
                    finally
                    {
                        // re-acquire the lock to prevent sending, not allowing cancellation
                        await _lock.WaitAsync();
                    }

                    // if there is no refresh token then break the loop and re-login
                    if (token.RefreshTokenValue is null) { break; }

                    // refresh the access token and phone directory
                    try
                    {
                        token = await _client.RefreshAccessTokenAsync(token.RefreshTokenValue, cancellationToken);
                        await RefreshShortcodeDirectoryAsync(cancellationToken);
                    }
                    catch (HttpRequestException e)
                    {
                        CheckCancellationAndLogException(e);

                        // break the refresh loop and re-login
                        break;
                    }
                }
            }

            void CheckCancellationAndLogException(HttpRequestException e)
            {
                // .NET 4 throws HttpRequestException on cancellation, so check for that
                cancellationToken.ThrowIfCancellationRequested();
                LogEvent(EventLogEntryType.Warning, e.ToString());
            }
        }
    }
}
