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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.PlatformUI;

namespace Microsoft.PythonTools.Environments {
    sealed class AddVirtualEnvironmentView : EnvironmentViewBase {
        private readonly SemaphoreSlim _ready = new SemaphoreSlim(1);
        private static readonly string InvalidPrintableFileCharsString = GetInvalidPrintableFileChars();

        public AddVirtualEnvironmentView(
            IServiceProvider serviceProvider,
            ProjectView[] projects,
            ProjectView selectedProject
        ) : base(serviceProvider, projects, selectedProject) {
            PageName = Strings.AddVirtualEnvironmentTabHeader;
            SetAsCurrent = SelectedProject != null;
            SetAsDefault = false;
            ViewInEnvironmentWindow = false;
            Progress = new ProgressControlViewModel();
            Progress.ProgressMessage = Strings.AddVirtualEnvironmentPreviewInProgress;
            Progress.IsProgressDisplayed = false;

            ResetProjectDependentProperties();
        }

        private static readonly DependencyPropertyKey WillCreateVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(WillCreateVirtualEnv), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty WillCreateVirtualEnvProperty =
            WillCreateVirtualEnvPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey UseVEnvPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(UseVEnv), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty UseVEnvProperty =
            UseVEnvPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey UseVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(UseVirtualEnv), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty UseVirtualEnvProperty =
            UseVirtualEnvPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey CannotCreateVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(CannotCreateVirtualEnv), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty CannotCreateVirtualEnvProperty =
            CannotCreateVirtualEnvPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey NoInterpretersInstalledPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(NoInterpretersInstalled), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty NoInterpretersInstalledProperty =
            NoInterpretersInstalledPropertyKey.DependencyProperty;

        public static readonly DependencyProperty BaseInterpreterProperty =
            DependencyProperty.Register(nameof(BaseInterpreter), typeof(InterpreterView), typeof(AddVirtualEnvironmentView), new PropertyMetadata(null, BaseInterpreter_Changed));

        public static readonly DependencyProperty CanInstallRequirementsTxtProperty =
            DependencyProperty.Register(nameof(CanInstallRequirementsTxt), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty WillInstallRequirementsTxtProperty =
            DependencyProperty.Register(nameof(WillInstallRequirementsTxt), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        private static readonly DependencyPropertyKey WillInstallPipPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(WillInstallPip), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty WillInstallPipProperty =
            WillInstallPipPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey WillInstallVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(WillInstallVirtualEnv), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty WillInstallVirtualEnvProperty =
            WillInstallVirtualEnvPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey WillRegisterGloballyPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(WillRegisterGlobally), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty WillRegisterGloballyProperty =
            WillRegisterGloballyPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey IsUsingGlobalDefaultEnvPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsUsingGlobalDefaultEnv), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false));

        public static readonly DependencyProperty IsUsingGlobalDefaultEnvProperty =
            IsUsingGlobalDefaultEnvPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey InterpretersPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(Interpreters), typeof(ObservableCollection<InterpreterView>), typeof(AddVirtualEnvironmentView), new PropertyMetadata());

        public static readonly DependencyProperty InterpretersProperty =
            InterpretersPropertyKey.DependencyProperty;

        public static readonly DependencyProperty IsRegisterCustomEnvProperty =
            DependencyProperty.Register(nameof(IsRegisterCustomEnv), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(false, IsRegisterCustomEnv_Changed));

        public static readonly DependencyProperty IsRegisterCustomEnvEnabledProperty =
            DependencyProperty.Register(nameof(IsRegisterCustomEnvEnabled), typeof(bool), typeof(AddVirtualEnvironmentView), new PropertyMetadata(true));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(AddVirtualEnvironmentView), new PropertyMetadata("", Description_Changed));

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(ProgressControlViewModel), typeof(AddVirtualEnvironmentView));

        public static readonly DependencyProperty BrowseOrigPrefixProperty =
            DependencyProperty.Register(nameof(BrowseOrigPrefix), typeof(string), typeof(AddVirtualEnvironmentView), new PropertyMetadata());

        public static readonly DependencyProperty VirtualEnvNameProperty =
            DependencyProperty.Register(nameof(VirtualEnvName), typeof(string), typeof(AddVirtualEnvironmentView), new PropertyMetadata("", VirtualEnvName_Changed));

        public static readonly DependencyProperty LocationPathProperty =
            DependencyProperty.Register(nameof(LocationPath), typeof(string), typeof(AddVirtualEnvironmentView), new PropertyMetadata("", LocationPath_Changed));

        private static readonly DependencyProperty RequirementsPathProperty =
            DependencyProperty.Register(nameof(RequirementsPath), typeof(string), typeof(AddVirtualEnvironmentView), new PropertyMetadata("", RequirementsPath_Changed));

        public ProgressControlViewModel Progress {
            get { return (ProgressControlViewModel)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public string BrowseOrigPrefix {
            get { return (string)GetValue(BrowseOrigPrefixProperty); }
            set { SetValue(BrowseOrigPrefixProperty, value); }
        }

        public string VirtualEnvName {
            get { return (string)GetValue(VirtualEnvNameProperty); }
            set { SetValue(VirtualEnvNameProperty, value); }
        }

        public string LocationPath {
            get { return (string)GetValue(LocationPathProperty); }
            set { SetValue(LocationPathProperty, value); }
        }

        public string RequirementsPath {
            get { return (string)GetValue(RequirementsPathProperty); }
            set { SetValue(RequirementsPathProperty, value); }
        }

        public bool WillCreateVirtualEnv {
            get { return (bool)GetValue(WillCreateVirtualEnvProperty); }
            private set { SetValue(WillCreateVirtualEnvPropertyKey, value); }
        }

        public bool UseVEnv {
            get { return (bool)GetValue(UseVEnvProperty); }
            private set { SetValue(UseVEnvPropertyKey, value); }
        }

        public bool UseVirtualEnv {
            get { return (bool)GetValue(UseVirtualEnvProperty); }
            private set { SetValue(UseVirtualEnvPropertyKey, value); }
        }

        public bool CannotCreateVirtualEnv {
            get { return (bool)GetValue(CannotCreateVirtualEnvProperty); }
            private set { SetValue(CannotCreateVirtualEnvPropertyKey, value); }
        }

        public bool NoInterpretersInstalled {
            get { return (bool)GetValue(NoInterpretersInstalledProperty); }
            set { SetValue(NoInterpretersInstalledPropertyKey, value); }
        }

        public InterpreterView BaseInterpreter {
            get { return (InterpreterView)GetValue(BaseInterpreterProperty); }
            set { SetValue(BaseInterpreterProperty, value); }
        }

        public bool CanInstallRequirementsTxt {
            get { return (bool)GetValue(CanInstallRequirementsTxtProperty); }
            set { SetValue(CanInstallRequirementsTxtProperty, value); }
        }

        public bool WillInstallRequirementsTxt {
            get { return (bool)GetValue(WillInstallRequirementsTxtProperty); }
            set { SetValue(WillInstallRequirementsTxtProperty, value); }
        }

        public bool WillInstallPip {
            get { return (bool)GetValue(WillInstallPipProperty); }
            private set { SetValue(WillInstallPipPropertyKey, value); }
        }

        public bool WillInstallVirtualEnv {
            get { return (bool)GetValue(WillInstallVirtualEnvProperty); }
            private set { SetValue(WillInstallVirtualEnvPropertyKey, value); }
        }

        public bool WillRegisterGlobally {
            get { return (bool)GetValue(WillRegisterGloballyProperty); }
            private set { SetValue(WillRegisterGloballyPropertyKey, value); }
        }

        public bool IsUsingGlobalDefaultEnv {
            get { return (bool)GetValue(IsUsingGlobalDefaultEnvProperty); }
            private set { SetValue(IsUsingGlobalDefaultEnvPropertyKey, value); }
        }

        public ObservableCollection<InterpreterView> Interpreters {
            get { return (ObservableCollection<InterpreterView>)GetValue(InterpretersProperty); }
            private set { SetValue(InterpretersPropertyKey, value); }
        }

        public bool IsRegisterCustomEnv {
            get { return (bool)GetValue(IsRegisterCustomEnvProperty); }
            set { SetValue(IsRegisterCustomEnvProperty, value); }
        }

        public bool IsRegisterCustomEnvEnabled {
            get { return (bool)GetValue(IsRegisterCustomEnvEnabledProperty); }
            set { SetValue(IsRegisterCustomEnvEnabledProperty, value); }
        }

        public string Description {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        private static void IsRegisterCustomEnv_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var v = (d as AddVirtualEnvironmentView);
            if (v != null) {
                if (!v.IsRegisterCustomEnv) {
                    v.SetAsDefault = false;
                }
                v.RefreshCanCreateVirtualEnv();
            }
        }

        private static void Description_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            (d as AddVirtualEnvironmentView)?.RefreshCanCreateVirtualEnv();
        }

        private static void VirtualEnvName_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            (d as AddVirtualEnvironmentView)?.RefreshCanCreateVirtualEnv();
        }

        private static void LocationPath_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            (d as AddVirtualEnvironmentView)?.RefreshCanCreateVirtualEnv();
        }

        private static void RequirementsPath_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            (d as AddVirtualEnvironmentView)?.RefreshCanCreateVirtualEnv();
        }

        private static void BaseInterpreter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            (d as AddVirtualEnvironmentView)?.UpdateInterpreter(e.NewValue as InterpreterView);
        }

        private static bool IsValidVirtualEnvPath(string path) {
            if (!PathUtils.IsValidPath(path)) {
                return false;
            }

            path = PathUtils.TrimEndSeparator(path);
            if (File.Exists(path)) {
                return false;
            }

            var name = Path.GetFileName(path).Trim();

            return !string.IsNullOrEmpty(name) &&
                name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private void RefreshCanCreateVirtualEnv() {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => RefreshCanCreateVirtualEnv()));
                return;
            }

            string path = string.IsNullOrEmpty(LocationPath)
                ? string.Empty :
                Path.Combine(LocationPath, VirtualEnvName);

            if (Interpreters == null || Interpreters.Count == 0) {
                WillCreateVirtualEnv = false;
                CannotCreateVirtualEnv = false;
                NoInterpretersInstalled = true;
            } else if (!PathUtils.IsValidFile(VirtualEnvName) || !IsValidVirtualEnvPath(path) || BaseInterpreter == null || IsFolderNotEmpty(path) || IsInvalidDescription()) {
                WillCreateVirtualEnv = false;
                CannotCreateVirtualEnv = true;
                NoInterpretersInstalled = false;
            } else {
                LocationPath = PathUtils.GetParent(path);
                WillCreateVirtualEnv = true;
                CannotCreateVirtualEnv = false;
                NoInterpretersInstalled = false;
            }

            if (string.IsNullOrEmpty(VirtualEnvName.Trim())) {
                SetError(nameof(VirtualEnvName), Strings.AddVirtualEnvironmentNameEmpty);
            }  else if (!PathUtils.IsValidFile(VirtualEnvName)) {
                SetError(nameof(VirtualEnvName), Strings.AddVirtualEnvironmentNameInvalid.FormatUI(InvalidPrintableFileCharsString));
            } else if (!IsValidVirtualEnvPath(path)) {
                SetError(nameof(VirtualEnvName), Strings.AddVirtualEnvironmentLocationInvalid.FormatUI(path));
            } else if (IsFolderNotEmpty(path)) {
                SetError(nameof(VirtualEnvName), Strings.AddVirtualEnvironmentLocationNotEmpty.FormatUI(path));
            } else {
                ClearErrors(nameof(VirtualEnvName));
            }

            bool canRegisterGlobally = false;
            if (IsRegisterCustomEnv && string.IsNullOrEmpty(Description)) {
                SetError(nameof(Description), Strings.AddEnvironmentDescriptionEmpty);
            } else if (IsRegisterCustomEnv && Description.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                SetError(nameof(Description), Strings.AddEnvironmentDescriptionInvalid);
            } else {
                ClearErrors(nameof(Description));
                canRegisterGlobally = true;
            }

            if (!string.IsNullOrEmpty(RequirementsPath) && !File.Exists(RequirementsPath)) {
                SetError(nameof(RequirementsPath), Strings.AddVirtualEnvironmentFileInvalid.FormatUI(RequirementsPath));
            } else {
                ClearErrors(nameof(RequirementsPath));
            }

            CanInstallRequirementsTxt = File.Exists(RequirementsPath);
            WillInstallRequirementsTxt = CanInstallRequirementsTxt && WillCreateVirtualEnv;
            WillRegisterGlobally = IsRegisterCustomEnv && canRegisterGlobally && WillCreateVirtualEnv;
            IsUsingGlobalDefaultEnv = true;
            if (SelectedProject != null && SelectedProject.Node != null && SelectedProject.Node.IsActiveInterpreterGlobalDefault) {
                IsUsingGlobalDefaultEnv = false;
            }
            
            // Enable the Create button only if there are no validation errors,
            // and if progress is not already being displayed
            IsAcceptEnabled = !HasErrors && !Progress.IsProgressDisplayed;
            AcceptCaption = Strings.AddEnvironmentCreateButton;
            AcceptAutomationName = Strings.AddEnvironmentCreateButtonAutomationName;
        }

        private static bool IsFolderNotEmpty(string path) {
            return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
        }

        private bool IsInvalidDescription() {
            return IsRegisterCustomEnv && string.IsNullOrEmpty(Description);
        }

        protected override void ResetProjectDependentProperties() {
            LocationPath = SelectedProject?.HomeFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            VirtualEnvName = GetDefaultEnvName();
            Interpreters = new ObservableCollection<InterpreterView>(InterpreterView.GetInterpreters(Site, null, true, InterpreterView.InterpreterFilter.ExcludeAll));
            BaseInterpreter = Interpreters.FirstOrDefault(v => v.Id == SelectedProject?.ActiveInterpreterId) ?? Interpreters.LastOrDefault();
            RequirementsPath = SelectedProject?.RequirementsTxtPath ?? string.Empty;
            CanInstallRequirementsTxt = File.Exists(RequirementsPath);
            WillInstallRequirementsTxt = CanInstallRequirementsTxt && WillCreateVirtualEnv;
            SetAsCurrent = SetAsCurrent && SelectedProject != null;
        }

        private string GetDefaultEnvName() {
            string venvName = string.Empty;
            if (!string.IsNullOrEmpty(LocationPath)) {
                venvName = "env";
                for (int i = 1; Directory.Exists(Path.Combine(LocationPath, venvName)); ++i) {
                    venvName = "env" + i.ToString();
                }
            } else {
                venvName = "env";
            }

            return venvName;
        }

        public override async Task ApplyAsync() {
            try {
                await _ready.WaitAsync();
            } catch (ObjectDisposedException) {
                return;
            }

            try {
                var op = new AddVirtualEnvironmentOperation(
                    Site,
                    SelectedProject?.Node,
                    SelectedProject?.Workspace,
                    Path.Combine(LocationPath, VirtualEnvName),
                    BaseInterpreter.Id,
                    UseVEnv,
                    WillInstallRequirementsTxt,
                    RequirementsPath,
                    IsRegisterCustomEnv,
                    Description,
                    SetAsCurrent,
                    SetAsDefault,
                    ViewInEnvironmentWindow,
                    OutputWindowRedirector.GetGeneral(Site)
                );

                await op.RunAsync();
            } finally {
                try {
                    _ready.Release();
                } catch (ObjectDisposedException) {
                }
            }
        }

        public override string ToString() {
            return Strings.AddVirtualEnvironmentTabHeader;
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                _ready.Dispose();
            }

            base.Dispose(disposing);
        }

        private void UpdateInterpreter(InterpreterView interpreterView) {
            UpdateInterpreterAsync(interpreterView).HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        // returns a space-delimited string containing invalid chars for a filename
        private static string GetInvalidPrintableFileChars() {
            var invalidChars = Path.GetInvalidFileNameChars();
            var invalidPrintableChars = invalidChars.Where(c => !char.IsControl(c));
            return string.Join(" ", invalidPrintableChars);
        }


        internal async Task UpdateInterpreterAsync(InterpreterView interpreterView) {
            if (!Dispatcher.CheckAccess()) {
                await Dispatcher.InvokeAsync(() => UpdateInterpreterAsync(interpreterView));
                return;
            }

            try {
                await _ready.WaitAsync();
            } catch (ObjectDisposedException) {
                return;
            }

            Progress.IsProgressDisplayed = true;
            IsAcceptEnabled = false;

            try {
                WillInstallPip = false;
                WillInstallVirtualEnv = false;
                WillRegisterGlobally = false;
                IsUsingGlobalDefaultEnv = false;
                UseVEnv = false;
                UseVirtualEnv = false;
                IsAcceptShieldVisible = false;

                if (interpreterView == null) {
                    return;
                }

                var interp = RegistryService.FindInterpreter(interpreterView.Id);
                Debug.Assert(interp != null);
                if (interp == null) {
                    return;
                }

                RefreshCanCreateVirtualEnv();

                if (await interp.HasModuleAsync("venv", OptionsService)) {
                    WillInstallPip = false;
                    WillInstallVirtualEnv = false;
                    UseVEnv = true;
                    UseVirtualEnv = false;
                } else if (await interp.HasModuleAsync("virtualenv", OptionsService)) {
                    WillInstallPip = false;
                    WillInstallVirtualEnv = false;
                    UseVEnv = false;
                    UseVirtualEnv = true;
                } else {
                    WillInstallPip = await interp.HasModuleAsync("pip", OptionsService);
                    WillInstallVirtualEnv = true;
                    UseVEnv = false;
                    UseVirtualEnv = true;
                }

                IsAcceptShieldVisible = (WillInstallPip || WillInstallVirtualEnv) &&
                    Site.GetPythonToolsService().GeneralOptions.ElevatePip;
            } finally {
                try {
                    _ready.Release();
                } catch (ObjectDisposedException) {
                }

                Progress.IsProgressDisplayed = false;
                RefreshCanCreateVirtualEnv();
            }
        }
    }
}
