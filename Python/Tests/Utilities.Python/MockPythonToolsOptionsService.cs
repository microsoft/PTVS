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
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Options;

namespace TestUtilities.Python {
    public sealed class MockPythonToolsOptionsService : IPythonToolsOptionsService {
        private Dictionary<string, Dictionary<string, string>> _options = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public void SaveString(string name, string category, string value) {
            Dictionary<string, string> catDict;
            if (!_options.TryGetValue(category, out catDict)) {
                _options[category] = catDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catDict[name] = value;
        }

        public string LoadString(string name, string category) {
            Dictionary<string, string> catDict;
            string res;
            if (!_options.TryGetValue(category, out catDict) ||
                !catDict.TryGetValue(name, out res)) {
                return null;
            }

            return res;
        }

        public void DeleteCategory(string category) {
            foreach (var key in _options.Keys.Where(k => k.StartsWith(category + "\\") || k == category).ToList()) {
                _options.Remove(key);
            }
        }
    }
}
