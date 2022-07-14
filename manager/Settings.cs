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
using System.Configuration;

namespace Aufbauwerk.Asterisk
{
    public sealed class Settings : ConfigurationSection
    {
        private const string InstanceElement = "settings";
        private const string ServersElement = "servers";
        private const string ServiceNameAttribute = "serviceName";
        private const string SmsElement = "sms";

        public static Settings Instance { get; } = (Settings)ConfigurationManager.GetSection(InstanceElement);

        public sealed class Server : ConfigurationElement
        {
            private const string NameAttribute = "name";
            private const string HostAttribute = "host";
            private const string PortAttribute = "port";
            private const string PrefixAttribute = "prefix";
            private const string TimeoutAttribute = "timeout";
            private const string ShutdownTimeLimitAttribute = "shutdownTimeLimit";
            private const string RetryIntervalAttribute = "retryInterval";
            private const string UsernameAttribute = "username";
            private const string SecretAttribute = "secret";
            private const string ExtensionPatternAttribute = "extensionPattern";
            private const string DeviceFormatAttribute = "deviceFormat";

            [ConfigurationProperty(NameAttribute, IsRequired = true, IsKey = true)]
            public string Name => (string)this[NameAttribute];

            [ConfigurationProperty(HostAttribute, IsRequired = true)]
            public string Host => (string)this[HostAttribute];

            [ConfigurationProperty(PortAttribute, DefaultValue = 8088)]
            [IntegerValidator(MinValue = 0, MaxValue = 65535)]
            public int Port => (int)this[PortAttribute];

            [ConfigurationProperty(PrefixAttribute, DefaultValue = "asterisk")]
            public string Prefix => (string)this[PrefixAttribute];

            [ConfigurationProperty(TimeoutAttribute, DefaultValue = "00:01:00")]
            [PositiveTimeSpanValidator]
            public TimeSpan Timeout => (TimeSpan)this[TimeoutAttribute];

            [ConfigurationProperty(ShutdownTimeLimitAttribute, DefaultValue = "00:00:10")]
            [PositiveTimeSpanValidator]
            public TimeSpan ShutdownTimeLimit => (TimeSpan)this[ShutdownTimeLimitAttribute];

            [ConfigurationProperty(RetryIntervalAttribute, DefaultValue = "00:00:30")]
            [PositiveTimeSpanValidator]
            public TimeSpan RetryInterval => (TimeSpan)this[RetryIntervalAttribute];

            [ConfigurationProperty(UsernameAttribute, IsRequired = true)]
            public string Username => (string)this[UsernameAttribute];

            [ConfigurationProperty(SecretAttribute, IsRequired = true)]
            public string Secret => (string)this[SecretAttribute];

            [ConfigurationProperty(ExtensionPatternAttribute, IsRequired = true)]
            public string ExtensionPattern => (string)this[ExtensionPatternAttribute];

            [ConfigurationProperty(DeviceFormatAttribute, DefaultValue = "Custom:$0")]
            public string DeviceFormat => (string)this[DeviceFormatAttribute];

            public override string ToString() => $"{Host}:{Port}{(string.IsNullOrEmpty(Prefix) ? "" : "/")}{Prefix}";
        }

        public sealed class ServerCollection : ConfigurationElementCollection, IEnumerable<Server>
        {
            protected override ConfigurationElement CreateNewElement() => new Server();

            protected override object GetElementKey(ConfigurationElement element) => ((Server)element).Name;

            public new IEnumerator<Server> GetEnumerator()
            {
                var enumerator = base.GetEnumerator();
                while (enumerator.MoveNext()) yield return (Server)enumerator.Current;
            }
        }

        public sealed class SmsSettings : ConfigurationElement
        {
            private const string UsernameAttribute = "username";
            private const string PasswordAttribute = "password";
            private const string VpnIdAttribute = "vpnId";
            private const string CustomerCodeAttribute = "customerCode";
            private const string TextTemplateAttribute = "textTemplate";
            private const string MaximumAgeAttribute = "maximumAge";
            private const string RetriesAttribute = "retries";
            private const string LoginRetryIntervalAttribute = "loginRetryInterval";

            [ConfigurationProperty(UsernameAttribute, IsRequired = true)]
            public string Username => (string)this[UsernameAttribute];

            [ConfigurationProperty(PasswordAttribute, IsRequired = true)]
            public string Password => (string)this[PasswordAttribute];

            [ConfigurationProperty(VpnIdAttribute, IsRequired = true)]
            public string VpnId => (string)this[VpnIdAttribute];

            [ConfigurationProperty(CustomerCodeAttribute, IsRequired = true)]
            public string CustomerCode => (string)this[CustomerCodeAttribute];

            [ConfigurationProperty(TextTemplateAttribute, IsRequired = true)]
            public string TextTemplate => (string)this[TextTemplateAttribute];

            [ConfigurationProperty(MaximumAgeAttribute, DefaultValue = "24:00:00")]
            [PositiveTimeSpanValidator]
            public TimeSpan MaximumAge => (TimeSpan)this[MaximumAgeAttribute];

            [ConfigurationProperty(RetriesAttribute, DefaultValue = 2)]
            [IntegerValidator(MinValue = 0)]
            public int Retries => (int)this[RetriesAttribute];

            [ConfigurationProperty(LoginRetryIntervalAttribute, DefaultValue = "00:15:00")]
            [PositiveTimeSpanValidator]
            public TimeSpan LoginRetryInterval => (TimeSpan)this[LoginRetryIntervalAttribute];
        }

        [ConfigurationProperty(ServersElement, IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(ServerCollection))]
        public ServerCollection Servers => (ServerCollection)base[ServersElement];

        [ConfigurationProperty(ServiceNameAttribute, DefaultValue = "AsteriskManager")]
        public string ServiceName => (string)this[ServiceNameAttribute];

        [ConfigurationProperty(SmsElement, IsRequired = true)]
        public SmsSettings Sms => (SmsSettings)this[SmsElement];
    }
}
