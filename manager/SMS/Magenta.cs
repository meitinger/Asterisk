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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aufbauwerk.Asterisk.Sms
{
    internal record AccessToken : IJsonResponse
    {
        [JsonConstructor]
        public AccessToken(string value, string type, string? scope, int? expiresIn, string? refreshTokenValue)
        {
            if (value.Length == 0) throw new JsonException("Access token is empty.");
            if (type.Length == 0) throw new JsonException("Access token type is empty.");
            if (expiresIn < 0) throw new JsonException($"Access token expiration ({expiresIn}) is invalid.");
            Value = value;
            Type = type;
            Scope = scope;
            ExpiresIn = expiresIn;
            RefreshTokenValue = refreshTokenValue;
        }

        [JsonProperty(PropertyName = "access_token", Required = Required.Always)]
        public string Value { get; }

        [JsonProperty(PropertyName = "token_type", Required = Required.Always)]
        public string Type { get; }

        [JsonProperty(PropertyName = "scope")]
        public string? Scope { get; }

        [JsonProperty(PropertyName = "expires_in")]
        public int? ExpiresIn { get; }

        [JsonProperty(PropertyName = "refresh_token")]
        public string? RefreshTokenValue { get; }
    }

    internal record AccessTokenIssueRequest : IJsonRequest
    {
        // usually this would be in form-data instead of JSON and also require grant_type and client_id

        public AccessTokenIssueRequest(string code, string redirectUri)
        {
            Code = code;
            RedirectUri = redirectUri;
        }

        [JsonProperty(PropertyName = "code")]
        public string Code { get; set; }

        [JsonProperty(PropertyName = "redirect_uri")]
        public string RedirectUri { get; set; }
    }

    internal record AccessTokenRefreshRequest : IJsonRequest
    {
        // usually this would be in form-data instead of JSON and also require grant_type

        public AccessTokenRefreshRequest(string refreshToken) => RefreshToken = refreshToken;

        [JsonProperty(PropertyName = "refresh_token")]
        public string RefreshToken { get; set; }
    }

    internal static class BusinessPortal
    {
        private static readonly Regex CsrfInputRegex = new(@"name=""_csrf"" value=""(?<value>[^""]*)""", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private const string RedirectUri = "https://businessportal.magenta.at/";

        public static async Task<string> GrantAuthorizationCodeAsync(this HttpClient client, CancellationToken cancellationToken)
        {
            // initiate OAuth code authorize
            using var authorizeResponse = await client.GetAsync($"https://tgate.magenta.at/oauth/authorize?response_type=code&client_id=businessPortal&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope=openid", cancellationToken);
            var redirectUri = authorizeResponse.EnsureSuccessStatusCode().RequestMessage.RequestUri;
            if (IsRedirectedTo("https://tgate.magenta.at/oauth/login"))
            {
                // fetch the CSRF protection token and perform the Spring security check
                var csrfInput = CsrfInputRegex.Match(await authorizeResponse.Content.ReadAsStringAsync());
                if (!csrfInput.Success) throw new HttpRequestException("Failed to retrieve CSRF protection token from TGate login page.");
                using var loginResponse = await client.PostAsync("https://tgate.magenta.at/j_spring_security_check", new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    {"_csrf", csrfInput.Groups["value"].Value },
                    {"j_username", Settings.Instance.Sms.Username },
                    {"j_password", Settings.Instance.Sms.Password },
                }), cancellationToken);
                redirectUri = loginResponse.EnsureSuccessStatusCode().RequestMessage.RequestUri;
            }

            // retrieve the code from the redirect URI
            if (!IsRedirectedTo(RedirectUri)) throw new HttpRequestException($"TGate redirected to unknown URI '{redirectUri}'.");
            var code = System.Web.HttpUtility.ParseQueryString(redirectUri.Query)["code"];
            if (string.IsNullOrEmpty(code)) throw new HttpRequestException($"TGate didn't include OAuth code in the redirect URI '{redirectUri}'.");
            return code;

            bool IsRedirectedTo(string path) => redirectUri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.UriEscaped) == path;
        }

        public static Task<AccessToken> IssueAccessTokenAsync(this HttpClient client, string code, CancellationToken cancellationToken) => TokenRequestAsync(client, new AccessTokenIssueRequest(code, RedirectUri), cancellationToken);

        public static Task<AccessToken> RefreshAccessTokenAsync(this HttpClient client, string refreshToken, CancellationToken cancellationToken) => TokenRequestAsync(client, new AccessTokenRefreshRequest(refreshToken), cancellationToken);

        private static async Task<AccessToken> TokenRequestAsync(HttpClient client, IJsonRequest content, CancellationToken cancellationToken)
        {
            // perform either a token issue or refresh, and store the access token value in future authorization headers
            const string bearer = "Bearer"; /* case-sensitive */
            client.DefaultRequestHeaders.Authorization = null;
            using var response = await client.PostAsync("https://businessportal.magenta.at/jaxrs/oauth/token", content.Serialize(), cancellationToken);
            var token = await response.EnsureSuccessStatusCode().Content.Deserialize<AccessToken>();
            if (!token.Type.Equals(bearer, StringComparison.OrdinalIgnoreCase)) throw new HttpRequestException($"Token type '{token.Type}' is unsupported.");
            client.DefaultRequestHeaders.Authorization = new(bearer, token.Value);
            return token;
        }
    }

    internal record SendSmsRequest : IJsonRequest
    {
        public SendSmsRequest(string content, params SuggestionItem[] recipients)
        {
            Recipients = recipients;
            Content = content;
        }

        [JsonProperty(PropertyName = "suggestionItems")]
        public IList<SuggestionItem> Recipients { get; set; }

        [JsonProperty(PropertyName = "content")]
        public string Content { get; set; }

        [JsonProperty(PropertyName = "custCode")]
        public string CustomerCode { get; set; } = Settings.Instance.Sms.CustomerCode;

        [JsonProperty(PropertyName = "lang")]
        public string Language { get; set; } = "en";

        [JsonProperty(PropertyName = "encoding")]
        public SmsEncoding Encoding { get; set; } = SmsEncoding.DEFAULT;
    }

    internal record SendSmsResponse : IJsonResponse
    {
        [JsonConstructor]
        public SendSmsResponse(int numberOfReceivers, string senderName)
        {
            if (numberOfReceivers < 0) throw new JsonException($"Number of receivers ({numberOfReceivers}) is invalid.");
            NumberOfReceivers = numberOfReceivers;
            SenderName = senderName;
        }

        [JsonProperty(PropertyName = "numberOfReceivers", Required = Required.Always)]
        public int NumberOfReceivers { get; }

        [JsonProperty(PropertyName = "senderPhoneNumber", Required = Required.Always)]
        public string SenderName { get; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum SmsEncoding
    {
        DEFAULT,
        UNICODE,
    }

    internal record SuggestionItem
    {
        [JsonConstructor]
        public SuggestionItem(SuggestionItemType type, string searchTerm, string displayName, string value, string customerCode)
        {
            Type = type;
            SearchTerm = searchTerm;
            DisplayName = displayName;
            Value = value;
            CustomerCode = customerCode;
        }

        [JsonProperty(PropertyName = "type", Required = Required.Always)]
        public SuggestionItemType Type { get; }

        [JsonProperty(PropertyName = "searchTerm", Required = Required.Always)]
        public string SearchTerm { get; }

        [JsonProperty(PropertyName = "displayedName", Required = Required.Always)] // sic
        public string DisplayName { get; }

        [JsonProperty(PropertyName = "value", Required = Required.Always)]
        public string Value { get; }

        [JsonProperty(PropertyName = "custCode", Required = Required.Always)]
        public string CustomerCode { get; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum SuggestionItemType
    {
        COMPANY_NAME = 1,
        CUSTCODE = 2,
        MSISDN = 3,
        PERSON_NAME = 4,
        SHORTCODE = 5,
        VPN_NUMBER = 6,
    }

    internal record UIData : IJsonResponse
    {
        [JsonConstructor]
        public UIData(SuggestionItem[] suggestionItems, string senderName)
        {
            SuggestionItems = Array.AsReadOnly(suggestionItems);
            SenderName = senderName;
        }

        [JsonProperty(PropertyName = "suggestionItems", Required = Required.Always)]
        public IReadOnlyCollection<SuggestionItem> SuggestionItems { get; }

        [JsonProperty(PropertyName = "senderName", Required = Required.Always)]
        public string SenderName { get; }
    }
}
