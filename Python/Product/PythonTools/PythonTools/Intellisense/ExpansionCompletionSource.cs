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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;

namespace Microsoft.PythonTools.Intellisense {
    class ExpansionCompletionSource {
        private readonly IServiceProvider _serviceProvider;
        private readonly Task<IEnumerable<CompletionResult>> _snippets;

        public ExpansionCompletionSource(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
            _snippets = GetAvailableSnippets();
        }

        public Task<IEnumerable<CompletionResult>> GetCompletionsAsync() => _snippets;

        private async Task<IEnumerable<CompletionResult>> GetAvailableSnippets() {
            var textMgr = _serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            IVsExpansionManager vsmgr;
            IExpansionManager mgr;
            if (textMgr == null || ErrorHandler.Failed(textMgr.GetExpansionManager(out vsmgr)) ||
                (mgr = vsmgr as IExpansionManager) == null) {
                return null;
            }

            try {
                var enumerator = await mgr.EnumerateExpansionsAsync(GuidList.guidPythonLanguageServiceGuid, 1, null, 0, 0, 0);
                if (enumerator == null) {
                    return null;
                }

                var res = new List<CompletionResult>();

                foreach (var e in COMEnumerable.ToList<VsExpansion>(enumerator.Next)) {
                    res.Add(new CompletionResult(
                        e.shortcut,
                        e.shortcut,
                        e.shortcut,
                        e.description,
                        PythonMemberType.CodeSnippet,
                        null
                    ));
                }

                return res;
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                return null;
            }
        }
    }
}
