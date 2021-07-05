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

namespace Microsoft.PythonTools {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    class ProvideBraceCompletionAttribute : RegistrationAttribute {
        private string _languageName;

        public ProvideBraceCompletionAttribute(string languageName) {
            _languageName = languageName;
        }

        public override void Register(RegistrationContext context) {
            using (Key serviceKey = context.CreateKey(LanguageServiceName)) {
                serviceKey.SetValue("ShowBraceCompletion", (int)1);
            }
        }

        public override void Unregister(RegistrationContext context) {
        }

        private string LanguageServiceName {
            get {
                return string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", "Languages\\Language Services", _languageName);
            }
        }
    }
}
