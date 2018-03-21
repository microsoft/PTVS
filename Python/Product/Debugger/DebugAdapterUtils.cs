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

using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace Microsoft.PythonTools.Debugger {
    public class DebugAdapterUtils {
        public static bool UseExperimentalDebugger() {
            var defaultValue = 1; // Experimental debugger is on by default
            if (!_useExperimental.HasValue) {
                try {
                    var experimentalKey = @"Software\Microsoft\PythonTools\Experimental";
                    using (var root = Registry.CurrentUser.OpenSubKey(experimentalKey, false)) {
                        var value = root?.GetValue("UseVsCodeDebugger", 1);
                        _useExperimental = ((int)value == 1);
                    }
                } catch (Exception) {
                    _useExperimental = (defaultValue == 1);
                }
            }

            return _useExperimental.Value;
        }
        private static bool? _useExperimental = null;
    }
}
