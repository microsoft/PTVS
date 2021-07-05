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

namespace Microsoft.CookiecutterTools.Telemetry {
    internal static class DictionaryExtension {
        public static IDictionary<string, object> FromAnonymousObject(object data) {
            IDictionary<string, object> dict;
            if (data != null) {
                dict = data as IDictionary<string, object>;
                if (dict == null) {
                    var attr = BindingFlags.Public | BindingFlags.Instance;
                    dict = new Dictionary<string, object>();
                    foreach (var property in data.GetType().GetProperties(attr)) {
                        if (property.CanRead) {
                            dict.Add(property.Name, property.GetValue(data, null));
                        }
                    }
                }
            } else {
                dict = new Dictionary<string, object>();
            }
            return dict;
        }
    }
}
