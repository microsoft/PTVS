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
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Navigation {

    /// <summary>
    /// This interface defines the service that finds Python files inside a hierarchy
    /// and builds the informations to expose to the class view or object browser.
    /// </summary>
    [Guid(PythonConstants.LibraryManagerServiceGuid)]
    internal interface IPythonLibraryManager : ILibraryManager {
    }

    /// <summary>
    /// Implementation of the service that builds the information to expose to the symbols
    /// navigation tools (class view or object browser) from the Python files inside a
    /// hierarchy.
    /// </summary>
    [Guid(PythonConstants.LibraryManagerGuid)]
    internal class PythonLibraryManager : LibraryManager, IPythonLibraryManager {
        private readonly PythonToolsPackage/*!*/ _package;

        public PythonLibraryManager(PythonToolsPackage/*!*/ package)
            : base(package) {
            _package = package;
        }

        public override LibraryNode CreateFileLibraryNode(LibraryNode parent, HierarchyNode hierarchy, string name, string filename) {
            return new PythonFileLibraryNode(parent, hierarchy, hierarchy.Caption, filename);
        }

        protected override void OnNewFile(LibraryTask task) {
            if (IsNonMemberItem(task.ModuleID.Hierarchy, task.ModuleID.ItemID)) {
                return;
            }
            AnalysisEntry item;
            if (task.TextBuffer == null || !task.TextBuffer.TryGetPythonProjectEntry(out item)) {
                task.ModuleID.Hierarchy
                    .GetProject()
                    .GetPythonProject()
                    .GetAnalyzer()
                    .AnalyzeFileAsync(task.FileName).ContinueWith(x => {
                        item = x.Result;

                        if (item != null) {
                            // We subscribe to OnNewAnalysis here instead of OnNewParseTree so that 
                            // in the future we can use the analysis to include type information in the
                            // object browser (for example we could include base type information with
                            // links elsewhere in the object browser).
                            item.AnalysisComplete += (sender, args) => {
                                _package.GetUIThread().InvokeAsync(() => FileParsed(task))
                                    .HandleAllExceptions(_package, GetType())
                                    .DoNotWait();
                            };
                        }
                    });
            }
        }
    }
}
