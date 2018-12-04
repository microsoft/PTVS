// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Ipc.Json {
    public sealed class UriJsonConverter : JsonConverter {
        public static readonly JsonConverter Instance = new UriJsonConverter();

        public override bool CanConvert(Type objectType) {
            return typeof(Uri).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            var uri = serializer.Deserialize<string>(reader);
            return string.IsNullOrEmpty(uri) ? null : new Uri(uri);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            if (value is Uri u) {
                serializer.Serialize(writer, u.AbsoluteUri);
            } else {
                serializer.Serialize(writer, null);
            }
        }
    }
}
