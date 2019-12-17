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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.LanguageServerClient {
    internal static class LiveShareExtensions {
        private static Guid SessionJoinedContextGuid = Guid.Parse("c6f0e3cb-a3c3-49bd-bad2-7aad8690c15b");

        public static bool IsInLiveShareClientSession(this IServiceProvider serviceProvider) {
            ThreadHelper.ThrowIfNotOnUIThread();

            var monitorSelection = serviceProvider.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();
            ErrorHandler.ThrowOnFailure(monitorSelection.GetCmdUIContextCookie(ref SessionJoinedContextGuid, out var cookie));
            ErrorHandler.ThrowOnFailure(monitorSelection.IsCmdUIContextActive(cookie, out var active));

            return active != 0;
        }
    }
}
