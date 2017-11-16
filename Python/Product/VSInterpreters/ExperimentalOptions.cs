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
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter {
    public static class ExperimentalOptions {
        private const string ExperimentSubkey = @"Software\Microsoft\PythonTools\Experimental";
        internal const string NoDatabaseFactoryKey = "NoDatabaseFactory";
        internal static readonly Lazy<bool> _noDatabaseFactory = new Lazy<bool>(GetNoDatabaseFactory);

        public static bool GetNoDatabaseFactory() => GetBooleanFactoryFlag(NoDatabaseFactoryKey);

        private static bool GetBooleanFactoryFlag(string keyName) {
            using (var root = Registry.CurrentUser.OpenSubKey(ExperimentSubkey, false)) {
                var value = root?.GetValue(NoDatabaseFactoryKey);
                if (value == null) {
                    return false;
                }
                int? asInt = value as int?;
                if (asInt.HasValue) {
                    if (asInt.GetValueOrDefault() == 0) {
                        // REG_DWORD but 0 means no experiment
                        return false;
                    }
                } else if (string.IsNullOrEmpty(value as string)) {
                    // Empty string or no value means no experiment
                    return false;
                }
            }
            return true;
        }

        private static void SetBooleanFactoryFlag(string keyName, bool value) {
            using (var root = Registry.CurrentUser.CreateSubKey(ExperimentSubkey, true)) {
                if (root == null) {
                    throw new UnauthorizedAccessException();
                }
                if (value) {
                    root.SetValue(keyName, 1);
                } else {
                    root.SetValue(keyName, 0);
                }
            }
        }

        /// <summary>
        /// Returns the setting for the NoDatabaseFactory experiment.
        /// </summary>
        /// <remarks>
        /// The value returned is determined at the start of the session and
        /// cannot be modified while running.
        /// </remarks>
        public static bool NoDatabaseFactory {
            get {
                return _noDatabaseFactory.Value;
            }
            set {
                SetBooleanFactoryFlag(NoDatabaseFactoryKey, value);
            }
        }
    }
}
