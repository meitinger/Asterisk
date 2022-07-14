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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Aufbauwerk.Asterisk
{
    internal interface IJsonRequest { }

    internal interface IJsonResponse { }

    internal static class JsonHelpers
    {
        public static async Task<T> Deserialize<T>(this HttpContent content) where T : IJsonResponse
        {
            try { return JsonConvert.DeserializeObject<T>(await content.ReadAsStringAsync()) ?? throw new JsonException("Null value returned."); }
            catch (JsonException e) { throw new HttpRequestException("Invalid JSON returned.", e); }
        }

        public static StringContent Serialize(this IJsonRequest request) => new(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
    }
}
