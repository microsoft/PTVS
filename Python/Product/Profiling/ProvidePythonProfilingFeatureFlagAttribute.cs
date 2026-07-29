// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE,
// OR NON-INFRINGEMENT.
//
// See the Apache License 2.0 for the specific language governing permissions
// and limitations under the License.

using System;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Profiling {
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ProvidePythonProfilingFeatureFlagAttribute : RegistrationAttribute {
        private const string FeatureFlagKey = @"FeatureFlags\DiagnosticsHub\PythonProfilingEnabled";

        public override void Register(RegistrationContext context) {
            using (var key = context.CreateKey(FeatureFlagKey)) {
                key.SetValue("Value", 1);
            }
        }

        public override void Unregister(RegistrationContext context) {
            context.RemoveValue(FeatureFlagKey, "Value");
        }
    }
}
