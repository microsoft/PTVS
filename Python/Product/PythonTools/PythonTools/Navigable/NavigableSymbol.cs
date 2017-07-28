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
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.PythonTools;

namespace Microsoft.PythonTools.Navigable {
    class NavigableSymbol : INavigableSymbol {
        private readonly static IOleCommandTarget _shellCommandDispatcher =
            PythonToolsPackage.GetGlobalService(typeof(SUIHostCommandDispatcher)) as IOleCommandTarget;

        public NavigableSymbol(SnapshotSpan span) {
            SymbolSpan = span;
        }

        public SnapshotSpan SymbolSpan { get; }

        public IEnumerable<INavigableRelationship> Relationships =>
            new List<INavigableRelationship>() { PredefinedNavigableRelationships.Definition };

        public void Navigate(INavigableRelationship relationship) {
            Debug.Assert(_shellCommandDispatcher != null);

            if (_shellCommandDispatcher != null) {
                Guid cmdGroup = VSConstants.GUID_VSStandardCommandSet97;
                uint cmdId = (uint)VSConstants.VSStd97CmdID.GotoDefn;

                ErrorHandler.CallWithCOMConvention(
                () => {
                    _shellCommandDispatcher.Exec(
                        ref cmdGroup, cmdId,
                        (uint)OLECMDEXECOPT.OLECMDEXECOPT_DODEFAULT,
                        System.IntPtr.Zero,
                        System.IntPtr.Zero);
                });
            }
        }
    }
}
