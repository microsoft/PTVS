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
    public static class UriExtensions {
        public static string ToAbsolutePath(this Uri uri) {
            if(IsWindows()) {
               var path = WebUtility.UrlDecode(uri.AbsolutePath).Replace('/', '\\');
                if(path.Contains(":\\")) {
                     if(path.Length > 2 && path[0] == '\\') {
                        if (path[2] == ':' || path[2] == '\\') {
                            // Fix URL like file:///C:\foo or file:///\\Server\Share
                            return path.Substring(1);
                        }
                    }
                }
                return path;
            }
            return uri.AbsolutePath;
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
