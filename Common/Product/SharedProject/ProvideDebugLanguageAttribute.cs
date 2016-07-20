// Visual Studio Shared Project
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

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Debugger.Interop;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioTools {
    class ProvideDebugLanguageAttribute : RegistrationAttribute {
        private readonly string _languageGuid, _languageName, _engineGuid, _eeGuid;

        public ProvideDebugLanguageAttribute(string languageName, string languageGuid, string eeGuid, string debugEngineGuid) {
            _languageName = languageName;
            _languageGuid = languageGuid;
            _eeGuid = eeGuid;
            _engineGuid = debugEngineGuid;
        }

        public override void Register(RegistrationContext context) {
            var langSvcKey = context.CreateKey("Languages\\Language Services\\" + _languageName + "\\Debugger Languages\\" + _languageGuid);
            langSvcKey.SetValue("", _languageName);
            // 994... is the vendor ID (Microsoft)
            var eeKey = context.CreateKey("AD7Metrics\\ExpressionEvaluator\\" + _languageGuid + "\\{994B45C4-E6E9-11D2-903F-00C04FA302A1}");
            eeKey.SetValue("Language", _languageName);
            eeKey.SetValue("Name", _languageName);
            eeKey.SetValue("CLSID", _eeGuid);

            var engineKey = eeKey.CreateSubkey("Engine");
            engineKey.SetValue("0", _engineGuid);
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
