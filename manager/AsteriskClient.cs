/* Copyright (C) 2013-2022, Manuel Meitinger
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
using System.Collections.Specialized;
using System.Net.Http;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aufbauwerk.Asterisk
{
    /// <summary>
    /// Represents an Asterisk Manager action request.
    /// </summary>
    public sealed class AsteriskAction : System.Collections.IEnumerable
    {
        private string? _cachedQuery;
        private readonly NameValueCollection _parameters = new(StringComparer.OrdinalIgnoreCase);
        private readonly StringBuilder _queryBuilder = new("rawman?action=");

        private class ReadOnlyNameValueCollection : NameValueCollection
        {
            internal ReadOnlyNameValueCollection(NameValueCollection col) : base(col) => IsReadOnly = true;
        }

        /// <summary>
        /// Creates a new action query definition.
        /// </summary>
        /// <param name="name">The name of the action.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        public AsteriskAction(string name)
        {
            // add the action parameter
            if (name.Length == 0) throw ExceptionBuilder.EmptyArgument(nameof(name));
            Name = name;
            _queryBuilder.Append(Uri.EscapeUriString(name));
        }

        /// <summary>
        /// Adds another parameter to the action.
        /// </summary>
        /// <param name="paramName">The parameter name.</param>
        /// <param name="paramValue">The value of the parameter.</param>
        /// <exception cref="ArgumentException"><paramref name="paramName"/> is empty.</exception>
        public void Add(string paramName, string paramValue)
        {
            // check, escape and add the parameter
            if (paramName.Length == 0) throw ExceptionBuilder.EmptyArgument(nameof(paramName));
            _parameters.Add(paramName, paramValue);
            _queryBuilder.Append('&').Append(Uri.EscapeUriString(paramName)).Append('=').Append(Uri.EscapeUriString(paramValue));
        }

        internal string ExpectedResponse => string.Equals(Name, "Logoff", StringComparison.OrdinalIgnoreCase) ? "Goodbye" : "Success";

        /// <summary>
        /// Gets the name of this action.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a read-only copy of the parameters.
        /// </summary>
        public NameValueCollection Parameters => new ReadOnlyNameValueCollection(_parameters);

        /// <summary>
        /// Returns the entire <c>rawman</c> action URL.
        /// </summary>
        /// <returns>A relative URL.</returns>
        public override string ToString() => _cachedQuery ??= _queryBuilder.ToString();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _parameters.GetEnumerator();
    }

    /// <summary>
    /// Represents an asynchronous client for the Asterisk Manager Interface via HTTP.
    /// </summary>
    public class AsteriskClient : HttpMessageInvoker
    {
        private readonly Uri _baseAddress;
        private bool _disposed = false;
        private readonly CancellationTokenSource _disposingCts = new();
        private DateTime _lastExecute = DateTime.Now;
        private bool _started = false;
        private TimeSpan _timeout = TimeSpan.FromTicks(TimeSpan.TicksPerMinute);

        /// <summary>
        /// Creates a new instance by building the base URI.
        /// </summary>
        /// <param name="host">The server name or IP.</param>
        /// <param name="port">The port on which the Asterisk Micro Web-Server listens.</param>
        /// <param name="prefix">The prefix to the manager endpoints.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is less than -1 or greater than 65,535.</exception>
        /// <exception cref="UriFormatException">The URI constructed by the parameters is invalid.</exception>
        public AsteriskClient(string host, int port = 8088, string prefix = "asterisk") : this(new UriBuilder("http", host, port, prefix).Uri) { }

        /// <summary>
        /// Creates a new client instance.
        /// </summary>
        /// <param name="baseAddress">The manager base address.</param>
        public AsteriskClient(Uri baseAddress) : this(baseAddress, new HttpClientHandler(), true) { }

        /// <summary>
        /// Creates a new client instance without a base address.
        /// </summary>
        /// <param name="baseAddress">The manager base address.</param>
        /// <param name="handler">The <see cref="HttpMessageHandler"/> responsible for processing the HTTP response messages.</param>
        /// <param name="disposeHandler"><c>true</c> if the inner handler should be disposed of by <see cref="HttpClient.Dispose(bool)"/>; <c>false</c> if you intend to reuse the inner handler.</param>
        protected AsteriskClient(Uri baseAddress, HttpMessageHandler handler, bool disposeHandler = true) : base(handler, disposeHandler) => _baseAddress = baseAddress;

        /// <summary>
        /// Gets or sets the <see cref="TimeSpan"/> to wait before the asynchronous execution times out.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The timeout specified is less than or equal to zero and is not <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
        /// <exception cref="InvalidOperationException">An operation has already been started on the current instance.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                CheckDisposedOrStarted();
                if (value <= TimeSpan.Zero && value != System.Threading.Timeout.InfiniteTimeSpan) throw new ArgumentOutOfRangeException(nameof(Timeout));
                _timeout = value;
            }
        }

        /// <summary>
        /// Checks whether the current instance has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        protected void CheckDisposed()
        {
            if (_disposed) throw new ObjectDisposedException($"{GetType().FullName}({_baseAddress})");
        }

        /// <summary>
        /// Checks whether the current instance has been disposed or an operation has been started.
        /// </summary>
        /// <exception cref="InvalidOperationException">An operation has already been started on the current instance.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        protected void CheckDisposedOrStarted()
        {
            CheckDisposed();
            if (_started) throw ExceptionBuilder.OperationAlreadyStarted();
        }

        /// <summary>
        /// Disposes the underlying <see cref="HttpMessageInvoker"/> and cancels all pending operations.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; </c>false</c> to releases only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _disposingCts.Cancel();
                _disposingCts.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Executes an asynchronous AMI action.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="parser">A <see cref="Parser{T}"/> that converts the raw AMI result into type <typeparamref name="T"/>.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">An error or timeout occurred while querying the server.</exception>
        /// <exception cref="AsteriskException">The server response contains an error.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        protected async Task<T> ExecuteAsync<T>(AsteriskAction action, Func<AsteriskAction, string, T> parser, CancellationToken cancellationToken)
        {
            // check/set the state and prepare the timeout cancellation source
            CheckDisposed();
            _started = true;
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposingCts.Token);
            if (_timeout != System.Threading.Timeout.InfiniteTimeSpan) cancellationTokenSource.CancelAfter(_timeout);

            // execute the query and turn timeouts into request exceptions
            try
            {
                try
                {
                    using var response = await SendAsync(new(HttpMethod.Get, new Uri(_baseAddress, action.ToString())), cancellationTokenSource.Token);
                    _lastExecute = DateTime.Now;
                    return parser(action, await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync());
                }
                catch (HttpRequestException) when (cancellationTokenSource.IsCancellationRequested) { throw new OperationCanceledException(cancellationTokenSource.Token); } // .NET 4 throws HttpRequestException on cancellation
            }
            catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                CheckDisposed();
                throw ExceptionBuilder.RequestTimedOut(_timeout);
            }
        }

        /// <summary>
        /// Executes an asynchronous enumeration operation.
        /// </summary>
        /// <param name="action">The Asterisk Manager action definition.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>An <see cref="AsteriskEnumeration"/> instance.</returns>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">An error or timeout occurred while querying the server.</exception>
        /// <exception cref="AsteriskException">The server response contains an error.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<AsteriskEnumeration> ExecuteEnumerationAsync(AsteriskAction action, CancellationToken cancellationToken) => ExecuteAsync(action, (action, s) => new AsteriskEnumeration(s, action.Name + "Complete"), cancellationToken);

        /// <summary>
        /// Executes an asynchronous non-query operation.
        /// </summary>
        /// <param name="action">The Asterisk Manager action definition.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">An error or timeout occurred while querying the server.</exception>
        /// <exception cref="AsteriskException">The server response contains an error.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task ExecuteNonQueryAsync(AsteriskAction action, CancellationToken cancellationToken) => ExecuteAsync(action, (action, s) => new AsteriskResponse(s, action.ExpectedResponse) is not null, cancellationToken);

        /// <summary>
        /// Executes an asynchronous query operation.
        /// </summary>
        /// <param name="action">The Asterisk Manager action definition.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">An error or timeout occurred while querying the server.</exception>
        /// <exception cref="AsteriskException">The server response contains an error.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<AsteriskResponse> ExecuteQueryAsync(AsteriskAction action, CancellationToken cancellationToken) => ExecuteAsync(action, (action, s) => new AsteriskResponse(s, action.ExpectedResponse), cancellationToken);

        /// <summary>
        /// Executes an asynchronous scalar operation.
        /// </summary>
        /// <param name="action">The Asterisk Manager action definition.</param>
        /// <param name="valueName">The name of the value to return.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException"><paramref name="valueName"/> is empty.</exception>
        /// <exception cref="HttpRequestException">An error or timeout occurred while querying the server.</exception>
        /// <exception cref="AsteriskException">The server response contains an error.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<string> ExecuteScalarAsync(AsteriskAction action, string valueName, CancellationToken cancellationToken)
        {
            if (valueName.Length == 0) throw ExceptionBuilder.EmptyArgument(nameof(valueName));
            return ExecuteAsync(action, (action, s) => new AsteriskResponse(s, action.ExpectedResponse).Get(valueName), cancellationToken);
        }

        private async Task KeepAliveAsync(CancellationToken cancellationToken)
        {
            // calculate how long we should wait
            var safeTimeout = Timeout - TimeSpan.FromTicks(10 * TimeSpan.TicksPerSecond);
            if (safeTimeout <= TimeSpan.Zero) safeTimeout = Timeout;

            // do not start pinging immediately if we just executed something else
            var timeoutFromLastExecute = safeTimeout - (DateTime.Now - _lastExecute);
            if (timeoutFromLastExecute > TimeSpan.Zero) await Task.Delay(timeoutFromLastExecute, cancellationToken);

            // continuously ping the server
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteNonQueryAsync(new("Ping"), cancellationToken);
                await Task.Delay(safeTimeout, cancellationToken);
            }
        }

        /// <summary>
        /// Waits for the result of given <paramref name="task"/> while keeping the connection alive with pings.
        /// </summary>
        /// <typeparam name="T">The result type of the given <paramref name="task"/>.</typeparam>
        /// <param name="task">The task to wait for.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">An error or timeout occurred while querying the server.</exception>
        /// <exception cref="AsteriskException">The server response contains an error.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        /// <remarks>This method also throws exceptions occurring in the <paramref name="task"/>.</remarks>
        public async Task<T> WaitAndKeepAliveAsync<T>(Task<T> task)
        {
            T result;
            CancellationTokenSource cts = new();

            // start pinging
            var keepAlive = KeepAliveAsync(cts.Token);

            // wait for the actual result
            try { result = await task; }
            catch
            {
                // stop pinging and re-throw the original exception
                await CancelKeepAliveAsync();
                throw;
            }

            // stop pinging
            try { await CancelKeepAliveAsync(); }
            catch
            {
                // make sure the result gets disposed on error
                (result as IDisposable)?.Dispose();
                throw;
            }

            // return the result
            return result;

            async Task CancelKeepAliveAsync()
            {
                cts.Cancel();
                try { await keepAlive; }
                catch (OperationCanceledException) { }
                finally { cts.Dispose(); }
            }
        }

        /// <summary>
        /// Waits for the given <paramref name="task"/> while keeping the connection alive with pings.
        /// </summary>
        /// <param name="task">The task to wait for.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="HttpRequestException">An error or timeout occurred while querying the server.</exception>
        /// <exception cref="AsteriskException">The server response contains an error.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has been disposed.</exception>
        /// <remarks>This method also throws exceptions occurring in the <paramref name="task"/>.</remarks>
        public Task WaitAndKeepAliveAsync(Task task)
        {
            return WrapTask(task);

            async static Task<bool> WrapTask(Task task)
            {
                await task;
                return true;
            }
        }
    }

    /// <summary>
    /// A collection of events and metadata.
    /// </summary>
    public class AsteriskEnumeration : IEnumerable<AsteriskEvent>
    {
        private static string[] ResultSetSeparator { get; } = { "\r\n\r\n" };

        private readonly AsteriskEvent[] _events;

        /// <summary>
        /// Creates a new enumeration from a manager response.
        /// </summary>
        /// <param name="input">The server response.</param>
        /// <param name="expectedCompleteEventName">The name of the event that ends the enumeration.</param>
        /// <param name="expectedResponseStatus">The expected status code, defaults to <c>"Success"</c>.</param>
        /// <exception cref="AsteriskException">The server response is invalid.</exception>
        public AsteriskEnumeration(string input, string expectedCompleteEventName, string expectedResponseStatus = "Success")
        {
            // split the events
            var items = input.Split(ResultSetSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (items.Length == 0) throw ExceptionBuilder.EnumerationResponseMissing();

            // get (and check) the response result set
            Response = new(items[0], expectedResponseStatus);

            // get the complete event
            if (items.Length == 1) throw ExceptionBuilder.EnumerationCompleteEventMissing();
            CompleteEvent = new(items[items.Length - 1]);
            if (!string.Equals(CompleteEvent.EventName, expectedCompleteEventName, StringComparison.OrdinalIgnoreCase)) throw ExceptionBuilder.EnumerationCompleteEventMissing();

            // get the rest
            _events = new AsteriskEvent[items.Length - 2];
            for (var i = 0; i < _events.Length; i++) _events[i] = new(items[i + 1]);
        }

        /// <summary>
        /// Gets the event that was sent after the enumeration was complete.
        /// </summary>
        public AsteriskEvent CompleteEvent { get; }

        /// <summary>
        /// Gets the number of events that were returned by the Asterisk Manager, excluding <see cref="CompleteEvent"/>.
        /// </summary>
        public int Count => _events.Length;

        /// <summary>
        /// Gets the response that was sent before any event.
        /// </summary>
        public AsteriskResponse Response { get; }

        /// <summary>
        /// Gets the event at a certain position within the enumeration.
        /// </summary>
        /// <param name="index">The offset within the enumeration.</param>
        /// <returns>The event description.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero or equal to or greater than <see cref="Count"/>.</exception>
        public AsteriskEvent this[int index] => _events[index];

        /// <summary>
        /// Returns an enumerator that iterates through the retrieved events.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the returned events.</returns>
        public IEnumerator<AsteriskEvent> GetEnumerator() => ((IEnumerable<AsteriskEvent>)_events).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Represents an Asterisk Manager event.
    /// </summary>
    public class AsteriskEvent : AsteriskResultSet
    {
        /// <summary>
        /// Creates a new event description.
        /// </summary>
        /// <param name="input">The server response.</param>
        /// <exception cref="AsteriskException">The server response is invalid.</exception>
        public AsteriskEvent(string input) : base(input) => EventName = Get("Event");

        /// <summary>
        /// Gets the name of the current event.
        /// </summary>
        public string EventName { get; }
    }

    /// <summary>
    /// Represents an Asterisk Manager error.
    /// </summary>
    [Serializable]
    public class AsteriskException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsteriskException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public AsteriskException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsteriskException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception.</param>
        public AsteriskException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsteriskException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
        protected AsteriskException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Extension methods to convert <see cref="DeviceState"/> to <see cref="ExtensionState"/> and vice versa.
    /// </summary>
    public static class AsteriskExtensions
    {
        /// <summary>
        /// Converts an <see cref="ExtensionState"/> into a <see cref="DeviceState"/>.
        /// </summary>
        /// <param name="value">The <see cref="ExtensionState"/> to convert.</param>
        /// <returns>The corresponding <see cref="DeviceState"/>.</returns>
        public static DeviceState ToDeviceState(this ExtensionState value) => value switch
        {
            ExtensionState.NOT_INUSE => DeviceState.NOT_INUSE,
            ExtensionState.INUSE => DeviceState.INUSE,
            ExtensionState.BUSY => DeviceState.BUSY,
            ExtensionState.UNAVAILABLE => DeviceState.UNAVAILABLE,
            ExtensionState.RINGING => DeviceState.RINGING,
            ExtensionState.INUSE | ExtensionState.RINGING => DeviceState.RINGINUSE,
            ExtensionState.ONHOLD => DeviceState.ONHOLD,
            ExtensionState.INUSE | ExtensionState.ONHOLD => DeviceState.ONHOLD,
            _ => DeviceState.UNKNOWN,
        };

        /// <summary>
        /// Converts a <see cref="DeviceState"/> into an <see cref="ExtensionState"/>, equivalent to <c>ast_devstate_to_extenstate</c>.
        /// </summary>
        /// <param name="value">The <see cref="DeviceState"/> to convert.</param>
        /// <returns>The corresponding <see cref="ExtensionState"/>.</returns>
        public static ExtensionState ToExtensionState(this DeviceState value) => value switch
        {
            DeviceState.UNKNOWN => ExtensionState.NOT_INUSE,
            DeviceState.NOT_INUSE => ExtensionState.NOT_INUSE,
            DeviceState.INUSE => ExtensionState.INUSE,
            DeviceState.BUSY => ExtensionState.BUSY,
            DeviceState.INVALID => ExtensionState.UNAVAILABLE,
            DeviceState.UNAVAILABLE => ExtensionState.UNAVAILABLE,
            DeviceState.RINGING => ExtensionState.RINGING,
            DeviceState.RINGINUSE => ExtensionState.INUSE | ExtensionState.RINGING,
            DeviceState.ONHOLD => ExtensionState.ONHOLD,
            _ => ExtensionState.NOT_INUSE,
        };
    }

    /// <summary>
    /// A result set with additional response metadata.
    /// </summary>
    public class AsteriskResponse : AsteriskResultSet
    {
        /// <summary>
        /// Creates a response result set.
        /// </summary>
        /// <param name="input">The server response.</param>
        /// <exception cref="AsteriskException">The server response is invalid.</exception>
        public AsteriskResponse(string input) : base(input)
        {
            // set the status and message
            Status = Get("Response");
            Message = string.Join(Environment.NewLine, GetValues("Message") ?? new string[0]);
        }

        /// <summary>
        /// Creates a new response result set and ensures that the status is as expected.
        /// </summary>
        /// <param name="input">The server response.</param>
        /// <param name="expectedResponseStatus">The expected status code.</param>
        /// <exception cref="AsteriskException">The server response is invalid.</exception>
        public AsteriskResponse(string input, string expectedResponseStatus) : this(input)
        {
            // check the response status
            if (!string.Equals(Status, expectedResponseStatus, StringComparison.OrdinalIgnoreCase)) throw ExceptionBuilder.ResponseUnexpected(Status, expectedResponseStatus, Message);
        }

        /// <summary>
        /// Gets the value of the response field, usually <c>Success</c> or <c>Error</c>.
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Gets the optional status message.
        /// </summary>
        public string Message { get; }
    }

    /// <summary>
    /// A <see cref="NameValueCollection"/> that behaves more like a <see cref="System.Collections.IDictionary"/> and is read-only.
    /// </summary>
    public class AsteriskResultSet : NameValueCollection
    {
        private static readonly string[] LineSeparator = { "\r\n" };
        private static readonly char[] PartSeparator = { ':' };

        /// <summary>
        /// Creates a result set.
        /// </summary>
        /// <param name="input">The server response.</param>
        /// <exception cref="AsteriskException">The server response is invalid.</exception>
        public AsteriskResultSet(string input) : base(StringComparer.OrdinalIgnoreCase)
        {
            // split the lines
            var lines = input.Split(LineSeparator, StringSplitOptions.None);

            // add each name-value pair
            var endOfSet = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0) endOfSet = true;
                else if (endOfSet) throw ExceptionBuilder.ResultSetMultipleEncountered();
                var parts = line.Split(PartSeparator, 2);
                if (parts.Length == 2) Add(parts[0].Trim(), parts[1].Trim());
                else Add(string.Empty, parts[0].Trim());
            }

            // don't allow further modifications
            IsReadOnly = true;
        }

        /// <summary>
        /// Gets the value associated with the given key.
        /// </summary>
        /// <param name="name">The key of the entry that contains the value.</param>
        /// <returns>A <see cref="string"/> that contains the value.</returns>
        /// <exception cref="AsteriskException">There are either no or multiple associated values.</exception>
        public override string Get(string name)
        {
            // ensure that there is one and only one value
            var values = GetValues(name);
            if (values is null || values.Length == 0) throw ExceptionBuilder.ResultSetKeyNotFound(name);
            if (values.Length > 1) throw ExceptionBuilder.ResultSetKeyNotUnique(name);
            return values[0];
        }
    }

    /// <summary>
    /// Device States. (Taken from <c>devicestate.h</c>.)
    /// </summary>
    /// <remarks>
    /// The order of these states may not change because they are included
    /// in Asterisk events which may be transmitted across the network to
    /// other servers.
    /// </remarks>
    public enum DeviceState
    {
        /// <summary>
        /// Device is valid but channel didn't know state.
        /// </summary>
        UNKNOWN,

        /// <summary>
        /// Device is not used.
        /// </summary>
        NOT_INUSE,

        /// <summary>
        /// Device is in use.
        /// </summary>
        INUSE,

        /// <summary>
        /// Device is busy.
        /// </summary>
        BUSY,

        /// <summary>
        /// Device is invalid.
        /// </summary>
        INVALID,

        /// <summary>
        /// Device is unavailable.
        /// </summary>
        UNAVAILABLE,

        /// <summary>
        /// Device is ringing.
        /// </summary>
        RINGING,

        /// <summary>
        /// Device is ringing *and* in use.
        /// </summary>
        RINGINUSE,

        /// <summary>
        /// Device is on hold.
        /// </summary>
        ONHOLD,
    }

    internal static class ExceptionBuilder
    {
        private static readonly ResourceManager Resources = new(typeof(AsteriskClient));

        private static string GetString([CallerMemberName] string? functionName = null) => Resources.GetString(functionName);

        internal static ArgumentException EmptyArgument(string paramName) => new(paramName, string.Format(GetString(), paramName));
        internal static AsteriskException EnumerationCompleteEventMissing() => new(GetString());
        internal static AsteriskException EnumerationResponseMissing() => new(GetString());
        internal static InvalidOperationException OperationAlreadyStarted() => new(GetString());
        internal static HttpRequestException RequestTimedOut(TimeSpan timeout) => new(string.Format(GetString(), timeout));
        internal static AsteriskException ResponseUnexpected(string response, string expectedResponse, string message) => new(string.Format(GetString(), response, expectedResponse, message));
        internal static AsteriskException ResultSetKeyNotFound(string name) => new(string.Format(GetString(), name));
        internal static AsteriskException ResultSetKeyNotUnique(string name) => new(string.Format(GetString(), name));
        internal static AsteriskException ResultSetMultipleEncountered() => new(GetString());
    }

    /// <summary>
    /// Extension states. (Taken from <c>pbx.h</c>.)
    /// </summary>
    /// <remarks>States can be combined.</remarks>
    [Flags]
    public enum ExtensionState
    {
        /// <summary>
        /// Extension removed.
        /// </summary>
        REMOVED = -2,

        /// <summary>
        /// Extension hint removed.
        /// </summary>
        DEACTIVATED = -1,

        /// <summary>
        /// No device INUSE or BUSY.
        /// </summary>
        NOT_INUSE = 0,

        /// <summary>
        /// One or more devices INUSE.
        /// </summary>
        INUSE = 1 << 0,

        /// <summary>
        /// All devices BUSY.
        /// </summary>
        BUSY = 1 << 1,

        /// <summary>
        /// All devices UNAVAILABLE/UNREGISTERED.
        /// </summary>
        UNAVAILABLE = 1 << 2,

        /// <summary>
        /// All devices RINGING.
        /// </summary>
        RINGING = 1 << 3,

        /// <summary>
        /// All devices ONHOLD.
        /// </summary>
        ONHOLD = 1 << 4,
    }
}
