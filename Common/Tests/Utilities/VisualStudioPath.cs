/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
