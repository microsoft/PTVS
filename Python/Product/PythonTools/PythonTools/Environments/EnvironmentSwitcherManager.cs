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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

// TODO: handle the workspace scenario

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
        private uint _selectionEventsCookie;
        private IVsHierarchy _previousHier;
        private PythonProjectNode _project;

        public event EventHandler EnvironmentsChanged;

        public EnvironmentSwitcherManager(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _optionsService = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
            _registryService = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
            _monitorSelection = serviceProvider.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;

            _optionsService.DefaultInterpreterChanged += OnDefaultInterpreterChanged;
            _registryService.InterpretersChanged += OnInterpretersChanged;
            _monitorSelection.AdviseSelectionEvents(this, out _selectionEventsCookie);

            AllFactories = Enumerable.Empty<IPythonInterpreterFactory>();
        }

        /// <summary>
        /// Returns whether a Python project, workspace or document is currently
        /// selected / active. Used to control the visibility of the status bar
        /// switcher.
        /// </summary>
        public bool IsInPythonMode { get; private set; }

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

        public PythonProjectNode Project {
            get => _project;
            private set {
                if (_project != null) {
                    _project.ActiveInterpreterChanged -= OnProjectActiveInterpreterChanged;
                    _project.InterpreterFactoriesChanged -= OnProjectInterpreterFactoriesChanged;
                }

                _project = value;

                if (_project != null) {
                    _project.ActiveInterpreterChanged += OnProjectActiveInterpreterChanged;
                    _project.InterpreterFactoriesChanged += OnProjectInterpreterFactoriesChanged;
                }
            }
        }

        public string GetSwitcherCommandKeyBinding() {
            try {
                return KeyBindingHelper.GetGlobalKeyBinding(GuidList.guidPythonToolsCmdSet, (int)PkgCmdIDList.cmdidViewEnvironmentStatus);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                // I've seen NRE thrown during VS shutdown
                return null;
            }
        }

        public Task SwitchToFactoryAsync(IPythonInterpreterFactory factory) {
            if (factory == null) {
                throw new ArgumentNullException(nameof(factory));
            }

            if (Project != null) {
                Project.SetInterpreterFactory(factory);
            } else {
                // For now we change the global default, but later we will improve this
                // so you can select different interpreter for a python file without
                // affecting the global default.
                _optionsService.DefaultInterpreter = factory;
            }

            return Task.CompletedTask;
        }

        private void OnInterpretersChanged(object sender, EventArgs e) {
            RefreshFactories();
        }

        private void OnDefaultInterpreterChanged(object sender, EventArgs e) {
            RefreshFactories();
        }

        private void OnProjectInterpreterFactoriesChanged(object sender, EventArgs e) {
            Debug.Assert(sender == Project);
            RefreshFactories();
        }

        private void OnProjectActiveInterpreterChanged(object sender, EventArgs e) {
            Debug.Assert(sender == Project);
            RefreshFactories();
        }

        private void RefreshFactories() {
            if (Project != null) {
                AllFactories = Project.InterpreterFactories.ToArray();
                CurrentFactory = Project.GetPythonInterpreterFactory();
            } else {
                AllFactories = _registryService.Interpreters.ToArray();
                CurrentFactory = _optionsService.DefaultInterpreter;
            }

            EnvironmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Reset() {
            Project = null;
            IsInPythonMode = false;
            AllFactories = Enumerable.Empty<IPythonInterpreterFactory>();
            CurrentFactory = null;

            EnvironmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew) {
            try {
                if (pHierNew != null && ErrorHandler.Succeeded(pHierNew.GetCanonicalName(itemidNew, out string filePath))) {
                    // We can get called multiple times for the same project,
                    // only process the first time it changes. It doesn't matter
                    // if it's a different document, since they all use the same
                    // environment(s).
                    if (pHierNew != _previousHier) {
                        Project = pHierNew.GetPythonProject();
                        IsInPythonMode = Project != null || ModulePath.IsPythonSourceFile(filePath);
                        RefreshFactories();
                    }
                } else {
                    Reset();
                }

                _previousHier = pHierNew;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                // Safety catch - we can't test all VS extensions / project types
                Reset();
            }

            return VSConstants.S_OK;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew) {
            try {
                if (VSConstants.DocumentFrame == elementid) {
                    var frame = varValueNew as IVsWindowFrame;
                    if (frame != null && ErrorHandler.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out object filePathObj))) {
                        object hierObj;
                        if (ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID.VSFPROPID_Hierarchy, out hierObj))) {
                            hierObj = null;
                        }

                        var hier = hierObj as IVsHierarchy;
                        var filePath = filePathObj as string;
                        Project = hier?.GetPythonProject();
                        IsInPythonMode = Project != null || ModulePath.IsPythonSourceFile(filePath);
                        RefreshFactories();
                    } else {
                        Reset();
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                // Safety catch - we can't test all VS extensions / project types
                Reset();
            }

            return VSConstants.S_OK;
        }

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) {
            return VSConstants.S_OK;
        }

        public void Dispose() {
            _optionsService.DefaultInterpreterChanged -= OnDefaultInterpreterChanged;
            _registryService.InterpretersChanged -= OnInterpretersChanged;
            _monitorSelection.UnadviseSelectionEvents(_selectionEventsCookie);

            if (Project != null) {
                Project.ActiveInterpreterChanged -= OnProjectActiveInterpreterChanged;
                Project.InterpreterFactoriesChanged -= OnProjectInterpreterFactoriesChanged;
            }
        }
    }
}
