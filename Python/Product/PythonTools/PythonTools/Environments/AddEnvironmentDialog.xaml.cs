﻿// Python Tools for Visual Studio
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Internal.VisualStudio.PlatformUI;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Wpf;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Wpf;
using Utilities = Microsoft.PythonTools.Wpf.Utilities;

namespace Microsoft.PythonTools.Environments {
    internal partial class AddEnvironmentDialog : ModernDialog, IDisposable {
        public static readonly ICommand MoreInfo = new RoutedCommand();
        private bool _isStartupFocusSet;

        public static readonly DependencyProperty IsLandscapeProperty = DependencyProperty.Register("IsLandscape", typeof(bool), typeof(AddEnvironmentDialog));
        public static readonly DependencyProperty WindowWidthProperty = DependencyProperty.Register("WindowWidth", typeof(double), typeof(AddEnvironmentDialog));
        public static readonly DependencyProperty MinimumWindowWidthProperty = DependencyProperty.Register("MinimumWindowWidth", typeof(double), typeof(AddEnvironmentDialog));
        public static readonly DependencyProperty WindowHeightProperty = DependencyProperty.Register("WindowHeight", typeof(double), typeof(AddEnvironmentDialog));
        public static readonly DependencyProperty MinimumWindowHeightProperty = DependencyProperty.Register("MinimumWindowHeight", typeof(double), typeof(AddEnvironmentDialog));
        public bool IsLandscape {
            get {
                return (bool)GetValue(IsLandscapeProperty);
            }
            set {
                SetValue(IsLandscapeProperty, value);
            }
        }
        public double WindowWidth {
            get {
                return (double)GetValue(WindowWidthProperty);
            }
            set {
                SetValue(WindowWidthProperty, value);
            }
        }
        public double MinimumWindowWidth {
            get {
                return (double)GetValue(MinimumWindowWidthProperty);
            }
            set {
                SetValue(MinimumWindowWidthProperty, value);
            }
        }
        public double WindowHeight {
            get {
                return (double)GetValue(WindowHeightProperty);
            }
            set {
                SetValue(WindowHeightProperty, value);
            }
        }
        public double MinimumWindowHeight {
            get {
                return (double)GetValue(MinimumWindowHeightProperty);
            }
            set {
                SetValue(MinimumWindowHeightProperty, value);
            }
        }

        public AddEnvironmentDialog(IEnumerable<EnvironmentViewBase> pages, EnvironmentViewBase selected) {
            if (pages == null) {
                throw new ArgumentNullException(nameof(pages));
            }

            DataContext = new AddEnvironmentView(pages, selected);
            IsLandscape = Utilities.IsLandscape;
            var startWidth = Math.Min(SystemParameters.PrimaryScreenWidth, IsLandscape ? 1024 : 700);
            var startHeight = Math.Min(SystemParameters.PrimaryScreenHeight, IsLandscape ? 700 : 1024);
            WindowWidth = startWidth;
            WindowHeight = startHeight;
            MinimumWindowWidth = WindowWidth;
            MinimumWindowHeight = WindowHeight;
            InitializeComponent();
        }

        public AddEnvironmentView View => (AddEnvironmentView)DataContext;

        public enum PageKind {
            CondaEnvironment,
            VirtualEnvironment,
            ExistingEnvironment,
            InstalledEnvironment,
        }

        public static async Task ShowAddEnvironmentDialogAsync(
            IServiceProvider site,
            PythonProjectNode project,
            IPythonWorkspaceContext workspace,
            string existingCondaEnvName,
            string environmentYmlPath,
            string requirementsTxtPath,
            CancellationToken ct = default(CancellationToken)
        ) {
            // For now default to the first tab (virtual environment)
            await ShowAddVirtualEnvironmentDialogAsync(
                site,
                project,
                workspace,
                existingCondaEnvName,
                environmentYmlPath,
                requirementsTxtPath,
                ct
            );
        }

        public static async Task ShowAddVirtualEnvironmentDialogAsync(
            IServiceProvider site,
            PythonProjectNode project,
            IPythonWorkspaceContext workspace,
            string existingCondaEnvName,
            string environmentYmlPath,
            string requirementsTxtPath,
            CancellationToken ct = default(CancellationToken)
        ) {
            await ShowDialogAsync(
                PageKind.VirtualEnvironment,
                site,
                project,
                workspace,
                existingCondaEnvName,
                environmentYmlPath,
                requirementsTxtPath,
                ct
            );
        }

        public static async Task ShowAddCondaEnvironmentDialogAsync(
            IServiceProvider site,
            PythonProjectNode project,
            IPythonWorkspaceContext workspace,
            string existingCondaEnvName,
            string environmentYmlPath,
            string requirementsTxtPath,
            CancellationToken ct = default(CancellationToken)
        ) {
            await ShowDialogAsync(
                PageKind.CondaEnvironment,
                site,
                project,
                workspace,
                existingCondaEnvName,
                environmentYmlPath,
                requirementsTxtPath,
                ct
            );
        }

        public static async Task ShowAddExistingEnvironmentDialogAsync(
            IServiceProvider site,
            PythonProjectNode project,
            IPythonWorkspaceContext workspace,
            string existingCondaEnvName,
            string environmentYmlPath,
            string requirementsTxtPath,
            CancellationToken ct = default(CancellationToken)
        ) {
            await ShowDialogAsync(
                PageKind.ExistingEnvironment,
                site,
                project,
                workspace,
                existingCondaEnvName,
                environmentYmlPath,
                requirementsTxtPath,
                ct
            );
        }

        public static async Task ShowDialogAsync(
            PageKind activePage,
            IServiceProvider site,
            PythonProjectNode project,
            IPythonWorkspaceContext workspace,
            string existingCondaEnvName,
            string environmentYmlPath,
            string requirementsTxtPath,
            CancellationToken ct = default(CancellationToken)
        ) {
            if (site == null) {
                throw new ArgumentNullException(nameof(site));
            }

            ProjectView[] projectViews;
            ProjectView selectedProjectView;

            if (workspace != null) {
                var registryService = site.GetComponentModel().GetService<IInterpreterRegistryService>();
                var optionsService = site.GetComponentModel().GetService<IInterpreterOptionsService>();
                selectedProjectView = new ProjectView(workspace);
                projectViews = new ProjectView[] { selectedProjectView };
            } else {
                try {
                    var sln = (IVsSolution)site.GetService(typeof(SVsSolution));
                    var projects = sln?.EnumerateLoadedPythonProjects().ToArray() ?? Array.Empty<PythonProjectNode>();

                    projectViews = projects
                        .Select((projectNode) => new ProjectView(projectNode))
                        .ToArray();

                    selectedProjectView = projectViews.SingleOrDefault(pv => pv.Node == project);
                } catch (InvalidOperationException ex) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(AddEnvironmentDialog)));
                    projectViews = Array.Empty<ProjectView>();
                    selectedProjectView = null;
                }
            }

            if (selectedProjectView != null) {
                if (existingCondaEnvName != null) {
                    selectedProjectView.MissingCondaEnvName = existingCondaEnvName;
                }

                if (environmentYmlPath != null) {
                    selectedProjectView.EnvironmentYmlPath = environmentYmlPath;
                }

                if (requirementsTxtPath != null) {
                    selectedProjectView.RequirementsTxtPath = requirementsTxtPath;
                }
            }

            var addVirtualView = new AddVirtualEnvironmentView(
                site,
                projectViews,
                selectedProjectView
            );

            var addCondaView = new AddCondaEnvironmentView(
                site,
                projectViews,
                selectedProjectView
            );

            var addExistingView = new AddExistingEnvironmentView(
                site,
                projectViews,
                selectedProjectView
            );

            var addInstalledView = new AddInstalledEnvironmentView(
                site,
                projectViews,
                selectedProjectView
            );

            EnvironmentViewBase activeView;
            switch (activePage) {
                case PageKind.VirtualEnvironment:
                    activeView = addVirtualView;
                    break;
                case PageKind.CondaEnvironment:
                    activeView = addCondaView;
                    break;
                case PageKind.ExistingEnvironment:
                    activeView = addExistingView;
                    break;
                case PageKind.InstalledEnvironment:
                    activeView = addInstalledView;
                    break;
                default:
                    Debug.Assert(false, string.Format("Unknown page kind '{0}'", activePage));
                    activeView = null;
                    break;
            }

            using (var dlg = new AddEnvironmentDialog(
                new EnvironmentViewBase[] {
                    addVirtualView,
                    addCondaView,
                    addExistingView,
                    addInstalledView,
                },
                activeView
            )) {
                try {
                    WindowHelper.ShowModal(dlg);
                } catch (Exception) {
                    dlg.Close();
                    throw;
                }

                if (dlg.DialogResult ?? false) {
                    var view = dlg.View.PagesView.CurrentItem as EnvironmentViewBase;
                    Debug.Assert(view != null);
                    if (view != null) {
                        try {
                            await view.ApplyAsync();
                        } catch (Exception ex) when (!ex.IsCriticalException()) {
                            Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(AddEnvironmentDialog)), Strings.ProductTitle);
                        }
                    }
                }
            }
        }

        private void OkClick(object sender, System.Windows.RoutedEventArgs e) {
            var view = View.PagesView.CurrentItem as EnvironmentViewBase;
            Debug.Assert(view != null);
            if (view != null) {
                var errors = view.GetAllErrors();
                if (errors.Any()) {
                    MessageBox.Show(
                        Strings.AddEnvironmentValidationErrors.FormatUI(string.Join(Environment.NewLine + Environment.NewLine, errors)),
                        Strings.ProductTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }
            }

            DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, System.Windows.RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void MoreInfo_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void MoreInfo_Executed(object sender, ExecutedRoutedEventArgs e) {
            Process.Start(PythonToolsPackage.InterpreterHelpUrl)?.Dispose();
        }

        public void Dispose() {
            View.Dispose();
        }

        private void AddCondaEnvironmentControl_Loaded(object sender, RoutedEventArgs e) {
            if (_isStartupFocusSet) {
                return;
            }

            _isStartupFocusSet = true;
            var comboBox = ((AddCondaEnvironmentControl)sender).ProjectComboBox;
            comboBox.Focus();
        }

        private void AddVirtualEnvironmentControl_Loaded(object sender, RoutedEventArgs e) {
            if (_isStartupFocusSet) {
                return;
            }

            _isStartupFocusSet = true;
            var comboBox = ((AddVirtualEnvironmentControl)sender).ProjectComboBox;
            comboBox.Focus();
        }

        private void AddExistingEnvironmentControl_Loaded(object sender, RoutedEventArgs e) {
            _isStartupFocusSet = true;
        }

        private void AddInstalledEnvironmentControl_Loaded(object sender, RoutedEventArgs e) {
            _isStartupFocusSet = true;
        }
    }
}
