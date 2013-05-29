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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class PythonAssemblyReferenceNode : AssemblyReferenceNode {
        private bool _failedToAnalyze;

        public PythonAssemblyReferenceNode(PythonProjectNode root, ProjectElement element)
            : base(root, element) {
            AnalyzeReference(root.GetInterpreter());
        }

        public PythonAssemblyReferenceNode(PythonProjectNode root, string assemblyPath)
            : base(root, assemblyPath) {
            AnalyzeReference(root.GetInterpreter());
        }

        protected override void OnAssemblyReferenceChangedOnDisk(object sender, FileChangedOnDiskEventArgs e) {
            base.OnAssemblyReferenceChangedOnDisk(sender, e);

            var analyzer = ((PythonProjectNode)ProjectMgr).GetAnalyzer();
            if (analyzer != null && CommonUtils.IsSamePath(e.FileName, Url)) {
                if ((e.FileChangeFlag & (_VSFILECHANGEFLAGS.VSFILECHG_Attr | _VSFILECHANGEFLAGS.VSFILECHG_Size | _VSFILECHANGEFLAGS.VSFILECHG_Time | _VSFILECHANGEFLAGS.VSFILECHG_Add)) != 0) {
                    // file was modified, unload and reload the extension module from our database.
                    analyzer.RemoveReference(new ProjectAssemblyReference(AssemblyName, Url));

                    AnalyzeReference(analyzer.Interpreter);
                } else if ((e.FileChangeFlag & _VSFILECHANGEFLAGS.VSFILECHG_Del) != 0) {
                    // file was deleted, unload from our extension database
                    analyzer.RemoveReference(new ProjectAssemblyReference(AssemblyName, Url));
                }
            }
        }

        private void AnalyzeReference(IPythonInterpreter interp) {
            if (interp != null) {
                _failedToAnalyze = false;
                var task = interp.AddReferenceAsync(new ProjectAssemblyReference(AssemblyName, Url));

                // check if we get an exception, and if so mark ourselves as a dangling reference.
                task.ContinueWith(new TaskFailureHandler(TaskScheduler.FromCurrentSynchronizationContext(), this).HandleAddRefFailure);
            }
        }

        protected override bool CanShowDefaultIcon() {
            if (_failedToAnalyze) {
                return false;
            }

            return base.CanShowDefaultIcon();
        }

        public override void Remove(bool removeFromStorage) {
            base.Remove(removeFromStorage);
            var interp = ((PythonProjectNode)ProjectMgr).GetInterpreter();
            if (interp != null) {
                interp.RemoveReference(new ProjectAssemblyReference(AssemblyName, Url));
            }
        }

        class TaskFailureHandler {
            private readonly TaskScheduler _uiScheduler;
            private readonly PythonAssemblyReferenceNode _node;
            public TaskFailureHandler(TaskScheduler uiScheduler, PythonAssemblyReferenceNode refNode) {
                _uiScheduler = uiScheduler;
                _node = refNode;
            }

            public void HandleAddRefFailure(Task task) {
                if (task.Exception != null) {
                    Task.Factory.StartNew(MarkFailed, default(CancellationToken), TaskCreationOptions.None, _uiScheduler);
                }
            }

            public void MarkFailed() {
                _node._failedToAnalyze = true;
            }
        }
    }
}
