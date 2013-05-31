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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.PythonTools.TestAdapter {
    enum SolutionChangedReason {
        None,
        Load,
        Unload,
    }

    class SolutionEventsListenerEventArgs : EventArgs {
        public IVsProject Project { get; private set; }
        public SolutionChangedReason ChangedReason { get; private set; }

        public SolutionEventsListenerEventArgs(IVsProject project, SolutionChangedReason reason) {
            Project = project;
            ChangedReason = reason;
        }

    }

    class SolutionEventsListener : IVsSolutionEvents {
        private readonly IVsSolution _solution;
        private uint _cookie = VSConstants.VSCOOKIE_NIL;

        /// <summary>
        /// Fires an event when a project is opened/closed/loaded/unloaded
        /// </summary>
        public event EventHandler<SolutionEventsListenerEventArgs> SolutionChanged;

        public SolutionEventsListener(IServiceProvider serviceProvider) {
            ValidateArg.NotNull(serviceProvider, "serviceProvider");
            _solution = serviceProvider.GetService<IVsSolution>(typeof(SVsSolution));
        }

        public void StartListeningForChanges() {
            if (_solution != null) {
                int hr = _solution.AdviseSolutionEvents(this, out _cookie);
                ErrorHandler.ThrowOnFailure(hr); // do nothing if this fails
            }
        }

        public void StopListeningForChanges() {
            if (_cookie != VSConstants.VSCOOKIE_NIL && _solution != null) {
                int hr = _solution.UnadviseSolutionEvents(_cookie);
                ErrorHandler.Succeeded(hr); // do nothing if this fails

                _cookie = VSConstants.VSCOOKIE_NIL;
            }
        }

        public void OnSolutionUpdated(IVsProject project, SolutionChangedReason reason) {
            var evt = SolutionChanged;
            if (evt != null) {
                evt(this, new SolutionEventsListenerEventArgs(project, reason));
            }
        }

        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) {
            var project = pRealHierarchy as IVsProject;
            if (project != null) {
                OnSolutionUpdated(project, SolutionChangedReason.Load);
            }
            return VSConstants.S_OK;
        }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) {
            var project = pHierarchy as IVsProject;
            if (project != null) {
                OnSolutionUpdated(project, SolutionChangedReason.Load);
            }
            return VSConstants.S_OK;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution) {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved) {
            return VSConstants.S_OK;
        }

        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) {
            var project = pRealHierarchy as IVsProject;
            if (project != null) {
                OnSolutionUpdated(project, SolutionChangedReason.Unload);
            }
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved) {
            OnSolutionUpdated(null, SolutionChangedReason.Unload);
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) {
            return VSConstants.S_OK;
        }
    }
}
