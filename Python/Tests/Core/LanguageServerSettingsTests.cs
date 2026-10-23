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

#if DEV18_OR_LATER
using System.Text.Json;
#endif
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace PythonToolsTests {
    [TestClass]
    public class LanguageServerSettingsTests {
        [TestMethod, Priority(0)]
        public void NewtonsoftSerializesStringLiteralNavigationDefault() {
            var json = JObject.FromObject(CreateSettings());
            var analysis = json["analysis"];

            Assert.AreEqual(true, analysis?["gotoDefinitionInStringLiteral"]?.Value<bool>());
            Assert.IsNull(analysis?["GotoDefinitionInStringLiteral"]);
        }

#if DEV18_OR_LATER
        [TestMethod, Priority(0)]
        public void SystemTextJsonSerializesStringLiteralNavigationDefault() {
            using (var json = JsonDocument.Parse(JsonSerializer.Serialize(CreateSettings()))) {
                var analysis = json.RootElement.GetProperty("analysis");

                Assert.IsTrue(analysis.GetProperty("gotoDefinitionInStringLiteral").GetBoolean());
                Assert.IsFalse(analysis.TryGetProperty("GotoDefinitionInStringLiteral", out _));
            }
        }
#endif

        private static LanguageServerSettings.PythonSettings CreateSettings()
            => new LanguageServerSettings.PythonSettings {
                analysis = new LanguageServerSettings.PythonSettings.PythonAnalysisSettings()
            };
    }
}
