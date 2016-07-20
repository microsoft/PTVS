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

using System;
using System.IO;
using Microsoft.Win32;

namespace TestUtilities {
    public static class VisualStudioPath {
        private static string _root = GetRootPath();

        private static string GetRootPath() {
            string vsDir = null;
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            using (var key = baseKey.OpenSubKey("SOFTWARE\\Microsoft\\VisualStudio\\SxS\\VS7")) {
                if (key != null) {
                    vsDir = key.GetValue(AssemblyVersionInfo.VSVersion) as string;
                }
            }

            return vsDir;
        }

        public static string Root {
            get {
                if (!Directory.Exists(_root)) {
                    throw new InvalidOperationException("Cannot find VS installation");
                }
                return _root;
            }
        }

        public static string PublicAssemblies {
            get {
                return Path.Combine(Root, "Common7", "IDE", "PublicAssemblies");
            }
        }

        public static string PrivateAssemblies {
            get {
                return Path.Combine(Root, "Common7", "IDE", "PrivateAssemblies");
            }
        }
    }
}
