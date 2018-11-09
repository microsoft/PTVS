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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;

namespace Microsoft.PythonTools.Environments {
    /// <summary>
    /// This keeps track of selection and other changes in the IDE and caches
    /// the data to be displayed in the environment status bar switcher,
    /// such as the list of all environments, the current environment,
    /// and the current project.
    /// </summary>
    sealed class EnvironmentSwitcherManager : IVsSelectionEvents, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IInterpreterRegistryService _registryService;
        private readonly IVsMonitorSelection _monitorSelection;
        private readonly IVsShell _shell;
        private readonly IVsFolderWorkspaceService _workspaceService;
        private uint _selectionEventsCookie;
        private IVsHierarchy _previousHier;
        private IEnvironmentSwitcherContext _context;
        private bool _isInitialized;

        public event EventHandler EnvironmentsChanged;

        public EnvironmentSwitcherManager(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _optionsService = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
            _registryService = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
            _monitorSelection = serviceProvider.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
            _shell = serviceProvider.GetService(typeof(SVsShell)) as IVsShell;
            _workspaceService = serviceProvider.GetComponentModel().GetService<IVsFolderWorkspaceService>();
            AllFactories = Enumerable.Empty<IPythonInterpreterFactory>();
        }

        public bool IsClosing { get; set; }

        /// <summary>
        /// Returns whether a Python project, workspace or document is currently
        /// selected / active. Used to control the visibility of the status bar
        /// switcher.
        /// </summary>
        public bool IsInPythonMode => Context != null;

        /// <summary>
        /// Interpreter factory that is currently applicable to the current
        /// project, workspace, or loose document. This may be null even when
        /// <see cref="IsInPythonMode"/> is <c>true</c>, for example when no
        /// environment is found on the machine.
        /// </summary>
        public IPythonInterpreterFactory CurrentFactory { get; private set; }

        /// <summary>
        /// List of factories to present to the user, based on the current
        /// project, workspace or loose document.
        /// </summary>
        /// <remarks>
        /// For a project, this is the list of environments referenced by the
        /// project. For a loose document or workspace, this is the list of all
        /// environments found on the machine. 
        /// </remarks>
        public IEnumerable<IPythonInterpreterFactory> AllFactories { get; private set; }

        public IEnvironmentSwitcherContext Context {
            get => _context;
            private set {
                if (_context != null) {
                    _context.EnvironmentsChanged -= OnInterpretersChanged;
                    _context.Dispose();
                }

                _context = value;

                if (_context != null) {
                    _context.EnvironmentsChanged += OnInterpretersChanged;
                }
            }
        }

        public void Initialize() {
            if (_isInitialized) {
                return;
            }

            _isInitialized = true;

            _optionsService.DefaultInterpreterChanged += OnDefaultInterpreterChanged;
            _registryService.InterpretersChanged += OnInterpretersChanged;
            _workspaceService.OnActiveWorkspaceChanged += OnActiveWorkspaceChanged;
            _monitorSelection.AdviseSelectionEvents(this, out _selectionEventsCookie);

            SetInitialContext();
        }

        public void Dispose() {
            if (_isInitialized) {
                _optionsService.DefaultInterpreterChanged -= OnDefaultInterpreterChanged;
                _registryService.InterpretersChanged -= OnInterpretersChanged;
                _workspaceService.OnActiveWorkspaceChanged -= OnActiveWorkspaceChanged;
                _monitorSelection.UnadviseSelectionEvents(_selectionEventsCookie);
            }

            if (Context != null) {
                Context.EnvironmentsChanged -= OnInterpretersChanged;
                Context.Dispose();
            }
        }

        public string GetSwitcherCommandKeyBinding() {
            // Don't call this during close because it throws an NRE
            return IsClosing
                ? null :
                KeyBindingHelper.GetGlobalKeyBinding(GuidList.guidPythonToolsCmdSet, (int)PkgCmdIDList.cmdidViewEnvironmentStatus);
        }

        public Task SwitchToFactoryAsync(IPythonInterpreterFactory factory) {
            return Context != null ? Context.ChangeFactoryAsync(factory) : Task.CompletedTask;
        }

        private Task OnActiveWorkspaceChanged(object sender, EventArgs e) {
            RefreshFactories();
            return Task.CompletedTask;
        }

        private void OnInterpretersChanged(object sender, EventArgs e) {
            RefreshFactories();
        }

        private void OnDefaultInterpreterChanged(object sender, EventArgs e) {
            RefreshFactories();
        }

        private void RefreshFactories() {
            if (IsClosing) {
                return;
            }

            AllFactories = Context?.AllFactories ?? Enumerable.Empty<IPythonInterpreterFactory>();
            CurrentFactory = Context?.CurrentFactory;
            EnvironmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Reset() {
            if (IsClosing) {
                return;
            }

            Context = null;
            AllFactories = Enumerable.Empty<IPythonInterpreterFactory>();
            CurrentFactory = null;

            EnvironmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetInitialContext() {
            IntPtr hierPtr = IntPtr.Zero;
            try {
                _monitorSelection.GetCurrentSelection(out hierPtr, out uint item, out _, out _);
                var hier = (hierPtr != IntPtr.Zero ? Marshal.GetObjectForIUnknown(hierPtr) : null) as IVsHierarchy;
                if (hier != null) {
                    HandleSelection(hier, item);
                } else if (ErrorHandler.Succeeded(_monitorSelection.GetCurrentElementValue(VSConstants.DocumentFrame, out object elementVal))) {
                    HandleElementValue(elementVal);
                } else {
                    Reset();
                }
            } finally {
                if (hierPtr != IntPtr.Zero) {
                    Marshal.Release(hierPtr);
                }
            }
        }

        private void UpdateContext(PythonProjectNode project, string filePath) {
            if (project != null) {
                Context = new EnvironmentSwitcherProjectContext(project);
            } else if (_workspaceService.CurrentWorkspace != null) {
                Context = new EnvironmentSwitcherWorkspaceContext(_serviceProvider, _workspaceService.CurrentWorkspace);
            } else if (ModulePath.IsPythonSourceFile(filePath)) {
                Context = new EnvironmentSwitcherFileContext(_serviceProvider, filePath);
            } else {
                Context = null;
            }

            RefreshFactories();
        }

        private void HandleSelection(IVsHierarchy pHierNew, uint itemidNew) {
            try {
                if (pHierNew != null &&
                    (ErrorHandler.Succeeded(pHierNew.GetCanonicalName(itemidNew, out string filePath)) ||
                    _workspaceService.CurrentWorkspace != null)) {
                    // We can get called multiple times for the same project,
                    // only process the first time it changes. It doesn't matter
                    // if it's a different document, since they all use the same
                    // environment(s).
                    if (pHierNew != _previousHier) {
                        var project = pHierNew.GetPythonProject();
                        UpdateContext(project, filePath);
                    }
                } else {
                    Reset();
                }

                _previousHier = pHierNew;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                // Safety catch - we can't test all VS extensions / project types
                Reset();
            }
        }

        private void HandleElementValue(object varValueNew) {
            try {
                var frame = varValueNew as IVsWindowFrame;
                if (frame != null && ErrorHandler.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object filePathObj))) {
                    object hierObj;
                    if (ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID.VSFPROPID_Hierarchy, out hierObj))) {
                        hierObj = null;
                    }

                    var hier = hierObj as IVsHierarchy;
                    var filePath = filePathObj as string;
                    var project = hier?.GetPythonProject();
                    UpdateContext(project, filePath);
                } else {
                    _previousHier = null;
                    Reset();
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                // Safety catch - we can't test all VS extensions / project types
                Reset();
            }
        }

        int IVsSelectionEvents.OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew) {
            HandleSelection(pHierNew, itemidNew);

            return VSConstants.S_OK;
        }

        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew) {
            if (VSConstants.DocumentFrame == elementid) {
                HandleElementValue(varValueNew);
            }

            return VSConstants.S_OK;
        }

        int IVsSelectionEvents.OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) {
            return VSConstants.S_OK;
        }
    }
}
