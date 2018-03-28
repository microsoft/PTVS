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
using System.Net;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    internal static class UriExtensions {
        public static string ToAbsolutePath(this Uri uri) {
            if(IsWindows()) {
                // VS Code always sends /-based paths with leading / such as
                // /f:/VSCP/src/test/pythonFiles. We need to clean this up.
                var path = uri.LocalPath.Replace('/', '\\');
                if(path.Length > 2 && path[0] == '\\') { // Looks like \C:\ or \\\
                    if (path[2] == ':' || path[2] == '\\') {
                        return path.Substring(1); // Drop the leading \
                    }
                }
                return path;
            }
            return uri.LocalPath;
        }

        private static bool IsWindows() {
#if DESKTOP
            return true;
#else
            return System.Runtime.InteropServices.RuntimeInformation
                .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif
        }
    }
}
