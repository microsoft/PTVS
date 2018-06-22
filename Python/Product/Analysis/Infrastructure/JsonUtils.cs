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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    internal static class JsonUtils {
        public static T ToObjectPopulateDefaults<T>(this JToken token) => token.ToObject<T>(JsonSerializer.Create(PopulateContractResolver.CreateSettings()));

        public static object ToObjectPopulateDefaults(this JToken token, Type type) => token.ToObject(type, JsonSerializer.Create(PopulateContractResolver.CreateSettings()));

        public static T DeserializeObjectPopulateDefaults<T>(string value) => JsonConvert.DeserializeObject<T>(value, PopulateContractResolver.CreateSettings());

        public static T GetDefaultInstance<T>() => new JObject().ToObjectPopulateDefaults<T>();

        public static object GetDefaultInstance(Type type) => new JObject().ToObjectPopulateDefaults(type);

        private class PopulateContractResolver : DefaultContractResolver {
            public static JsonSerializerSettings CreateSettings() => new JsonSerializerSettings {
                DefaultValueHandling = DefaultValueHandling.Populate,
                ContractResolver = new PopulateContractResolver()
            };

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) 
                => ChangeDefaultValue(base.CreateProperty(member, memberSerialization));

            protected override JsonProperty CreatePropertyFromConstructorParameter(JsonProperty matchingMemberProperty, ParameterInfo parameterInfo) 
                => ChangeDefaultValue(base.CreatePropertyFromConstructorParameter(matchingMemberProperty, parameterInfo));

            private static JsonProperty ChangeDefaultValue(JsonProperty property) {
                if (property.DefaultValue == DefaultJsonAttribute.DummyValue) {
                    var type = property.PropertyType;
                    property.DefaultValue = type.IsArray 
                        ? Array.CreateInstance(type, 0)
                        : GetDefaultInstance(type);
                }

                return property;
            }
        }
    }
}