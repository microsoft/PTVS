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
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    class ProvideFeatureFlagAttribute : RegistrationAttribute {
        private readonly string _name;
        private readonly bool _defaultValue;

        public ProvideFeatureFlagAttribute(string name, bool defaultValue) {
            _name = name;
            _defaultValue = defaultValue;
        }

        public override void Register(RegistrationContext context) {
            using (var engineKey = context.CreateKey("FeatureFlags\\" + _name.Replace('.', '\\'))) {
                engineKey.SetValue("Value", _defaultValue ? 1 : 0);
            }
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
