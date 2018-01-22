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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Project {
    sealed class AddVirtualEnvironmentView : DependencyObject, INotifyPropertyChanged, IDisposable {
        private readonly IInterpreterRegistryService _interpreterService;
        private readonly PythonProjectNode _project;
        private readonly string _requirementsPath;
        private readonly string _projectHome;
        private readonly SemaphoreSlim _ready = new SemaphoreSlim(1);
        private InterpreterView _lastUserSelectedBaseInterpreter;

        public AddVirtualEnvironmentView(
            PythonProjectNode project,
            IInterpreterRegistryService interpreterService,
            string selectInterpreterId,
            string requirementsPath
        ) {
            _interpreterService = interpreterService;
            _project = project;
            _requirementsPath = requirementsPath;
            VirtualEnvBasePath = _projectHome = project.ProjectHome;
            Interpreters = new ObservableCollection<InterpreterView>(InterpreterView.GetInterpreters(project.Site, null, true));
            var selection = Interpreters.FirstOrDefault(v => v.Id == selectInterpreterId);
            if (selection == null) {
                selection = Interpreters.FirstOrDefault(v => v.Id == project.ActiveInterpreter?.Configuration.Id)
                    ?? Interpreters.LastOrDefault();
            }
            BaseInterpreter = selection;

            _project.InterpreterFactoriesChanged += OnInterpretersChanged;

            var venvName = "env";
            for (int i = 1; Directory.Exists(Path.Combine(_projectHome, venvName)); ++i) {
                venvName = "env" + i.ToString();
            }
            VirtualEnvName = venvName;

            CanInstallRequirementsTxt = File.Exists(_requirementsPath);
            WillInstallRequirementsTxt = CanInstallRequirementsTxt;
        }

        public void Dispose() {
            _ready.Dispose();
        }

        private async void OnInterpretersChanged(object sender, EventArgs e) {
            await Dispatcher.InvokeAsync(() => {
                Interpreters.Merge(
                    InterpreterView.GetInterpreters(_project.Site, _project),
                    InterpreterView.EqualityComparer,
                    InterpreterView.Comparer
                );
            });
        }


        public bool ShowBrowsePathError {
            get { return (bool)GetValue(ShowBrowsePathErrorProperty); }
            set { SetValue(ShowBrowsePathErrorProperty, value); }
        }

        public string BrowseOrigPrefix {
            get { return (string)GetValue(BrowseOrigPrefixProperty); }
            set { SetValue(BrowseOrigPrefixProperty, value); }
        }

        public static readonly DependencyProperty ShowBrowsePathErrorProperty = DependencyProperty.Register(
            "ShowBrowsePathError",
            typeof(bool),
            typeof(AddVirtualEnvironmentView),
            new PropertyMetadata(false)
        );

        public static readonly DependencyProperty BrowseOrigPrefixProperty = DependencyProperty.Register(
            "BrowseOrigPrefix",
            typeof(string),
            typeof(AddVirtualEnvironmentView),
            new PropertyMetadata()
        );


        public string VirtualEnvBasePath {
            get { return (string)GetValue(VirtualEnvBasePathProperty); }
            private set { SetValue(VirtualEnvBasePathPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey VirtualEnvBasePathPropertyKey =
            DependencyProperty.RegisterReadOnly("VirtualEnvBasePath",
                typeof(string),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata());
        public static readonly DependencyProperty VirtualEnvBasePathProperty =
            VirtualEnvBasePathPropertyKey.DependencyProperty;



        public string VirtualEnvName {
            get { return (string)GetValue(VirtualEnvNameProperty); }
            set { SetValue(VirtualEnvNameProperty, value); }
        }

        public static readonly DependencyProperty VirtualEnvNameProperty =
            DependencyProperty.Register("VirtualEnvName",
            typeof(string),
            typeof(AddVirtualEnvironmentView),
            new PropertyMetadata(VirtualEnvName_Changed));

        private static void VirtualEnvName_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var name = e.NewValue as string;
            var path = d.GetValue(VirtualEnvBasePathProperty) as string;
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) {
                d.SetValue(VirtualEnvPathPropertyKey, d.GetValue(VirtualEnvBasePathProperty));
            } else {
                name = name.TrimEnd('.', ' ');
                d.SetValue(VirtualEnvPathPropertyKey, PathUtils.GetAbsoluteDirectoryPath(path, name));
            }
        }


        public string VirtualEnvPath {
            get { return (string)GetValue(VirtualEnvPathProperty); }
            set { SetValue(VirtualEnvPathPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey VirtualEnvPathPropertyKey =
            DependencyProperty.RegisterReadOnly("VirtualEnvPath",
                typeof(string),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(VirtualEnvPath_Changed));
        public static readonly DependencyProperty VirtualEnvPathProperty =
            VirtualEnvPathPropertyKey.DependencyProperty;

        private static void VirtualEnvPath_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var aiv = d as AddVirtualEnvironmentView;
            if (aiv != null) {
                aiv.RefreshCanCreateVirtualEnv(e.NewValue as string);
            }
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

        private void RefreshCanCreateVirtualEnv(string path) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => RefreshCanCreateVirtualEnv(path)));
                return;
            }

            if (Interpreters.Count == 0) {
                WillCreateVirtualEnv = false;
                WillAddVirtualEnv = false;
                CannotCreateVirtualEnv = false;
                NoInterpretersInstalled = true;
                return;
            }

            if (!IsValidVirtualEnvPath(path) || BaseInterpreter == null) {
                WillCreateVirtualEnv = false;
                WillAddVirtualEnv = false;
                CannotCreateVirtualEnv = true;
                NoInterpretersInstalled = false;
                return;
            }


            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any()) {
                WillCreateVirtualEnv = false;

                var config = VirtualEnv.FindInterpreterConfiguration(null, path, _interpreterService);
                if (config != null && File.Exists(config.InterpreterPath)) {
                    var baseInterpView = Interpreters.FirstOrDefault(v => v.Id == config.Id);
                    if (baseInterpView != null) {
                        if (_lastUserSelectedBaseInterpreter == null) {
                            _lastUserSelectedBaseInterpreter = BaseInterpreter;
                        }
                        BaseInterpreter = baseInterpView;
                        WillAddVirtualEnv = true;
                    } else {
                        WillAddVirtualEnv = false;
                    }
                } else {
                    WillAddVirtualEnv = false;
                }
                CannotCreateVirtualEnv = !WillAddVirtualEnv;
                NoInterpretersInstalled = false;
            } else {
                WillCreateVirtualEnv = true;
                WillAddVirtualEnv = false;
                CannotCreateVirtualEnv = false;
                NoInterpretersInstalled = false;
                if (_lastUserSelectedBaseInterpreter != null) {
                    BaseInterpreter = _lastUserSelectedBaseInterpreter;
                    _lastUserSelectedBaseInterpreter = null;
                }
            }

        }

        public bool WillCreateVirtualEnv {
            get { return (bool)GetValue(WillCreateVirtualEnvProperty); }
            private set { SafeSetValue(WillCreateVirtualEnvPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey WillCreateVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly("WillCreateVirtualEnv",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty WillCreateVirtualEnvProperty =
            WillCreateVirtualEnvPropertyKey.DependencyProperty;

        public bool UseVEnv {
            get { return (bool)GetValue(UseVEnvProperty); }
            private set { SafeSetValue(UseVEnvPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey UseVEnvPropertyKey =
            DependencyProperty.RegisterReadOnly("UseVEnv",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty UseVEnvProperty =
            UseVEnvPropertyKey.DependencyProperty;

        public bool WillAddVirtualEnv {
            get { return (bool)GetValue(WillAddVirtualEnvProperty); }
            private set { SafeSetValue(WillAddVirtualEnvPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey WillAddVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly("WillAddVirtualEnv",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty WillAddVirtualEnvProperty =
            WillAddVirtualEnvPropertyKey.DependencyProperty;


        public bool CannotCreateVirtualEnv {
            get { return (bool)GetValue(CannotCreateVirtualEnvProperty); }
            private set { SetValue(CannotCreateVirtualEnvPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey CannotCreateVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly("CannotCreateVirtualEnv",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty CannotCreateVirtualEnvProperty =
            CannotCreateVirtualEnvPropertyKey.DependencyProperty;


        public bool NoInterpretersInstalled {
            get { return (bool)GetValue(NoInterpretersInstalledProperty); }
            set { SetValue(NoInterpretersInstalledPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey NoInterpretersInstalledPropertyKey =
            DependencyProperty.RegisterReadOnly("NoInterpretersInstalled",
                typeof(bool),
                typeof(AddInterpreterView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty NoInterpretersInstalledProperty =
            NoInterpretersInstalledPropertyKey.DependencyProperty;





        public InterpreterView BaseInterpreter {
            get { return (InterpreterView)SafeGetValue(BaseInterpreterProperty); }
            set { SafeSetValue(BaseInterpreterProperty, value); }
        }

        public static readonly DependencyProperty BaseInterpreterProperty =
            DependencyProperty.Register("BaseInterpreter",
                typeof(InterpreterView),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(null, BaseInterpreter_Changed));

        private static async void BaseInterpreter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var aiv = d as AddVirtualEnvironmentView;
            if (aiv == null) {
                return;
            }

            await aiv.UpdateInterpreter(e.NewValue as InterpreterView);
        }


        public bool CanInstallRequirementsTxt {
            get { return (bool)GetValue(CanInstallRequirementsTxtProperty); }
            set { SetValue(CanInstallRequirementsTxtProperty, value); }
        }

        public static readonly DependencyProperty CanInstallRequirementsTxtProperty =
            DependencyProperty.Register("CanInstallRequirementsTxt",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));

        public bool WillInstallRequirementsTxt {
            get { return (bool)GetValue(WillInstallRequirementsTxtProperty); }
            set { SetValue(WillInstallRequirementsTxtProperty, value); }
        }

        public static readonly DependencyProperty WillInstallRequirementsTxtProperty =
            DependencyProperty.Register("WillInstallRequirementsTxt",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));



        /// <summary>
        /// Waits for any background processing to complete. Properties of this
        /// object may be invalid while processing is ongoing.
        /// </summary>
        public async Task WaitForReady() {
            // Addresses https://pytools.codeplex.com/workitem/2312
            try {
                await _ready.WaitAsync();
            } catch (ObjectDisposedException) {
            }
        }

        internal async Task UpdateInterpreter(InterpreterView view) {
            if (!Dispatcher.CheckAccess()) {
                await Dispatcher.InvokeAsync(() => UpdateInterpreter(view));
                return;
            }

            try {
                await _ready.WaitAsync();
            } catch (ObjectDisposedException) {
                return;
            }

            try {
                WillInstallPip = false;
                WillInstallVirtualEnv = false;
                WillInstallElevated = false;
                MayNotSupportVirtualEnv = false;

                Debug.Assert(view != null);
                if (view == null) {
                    return;
                }

                var registry = _project.Site.GetComponentModel().GetService<IInterpreterRegistryService>();
                var interp = registry.FindInterpreter(view.Id);
                Debug.Assert(interp != null);
                if (interp == null) {
                    return;
                }

                //MayNotSupportVirtualEnv = !SupportsVirtualEnv.Contains(interp.Id);
                RefreshCanCreateVirtualEnv(VirtualEnvPath);

                var opts = _project.Site.GetComponentModel().GetService<IInterpreterOptionsService>();

                if (await interp.HasModuleAsync("venv", opts)) {
                    WillInstallPip = false;
                    WillInstallVirtualEnv = false;
                    UseVEnv = true;
                } else if (await interp.HasModuleAsync("virtualenv", opts)) {
                    WillInstallPip = false;
                    WillInstallVirtualEnv = false;
                    UseVEnv = false;
                } else {
                    WillInstallPip = await interp.HasModuleAsync("pip", opts);
                    WillInstallVirtualEnv = true;
                    UseVEnv = false;
                }
                WillInstallElevated = (WillInstallPip || WillInstallVirtualEnv) &&
                    _project.Site.GetPythonToolsService().GeneralOptions.ElevatePip;
            } finally {
                try {
                    _ready.Release();
                } catch (ObjectDisposedException) {
                }
            }
        }


        public bool MayNotSupportVirtualEnv {
            get { return (bool)GetValue(MayNotSupportVirtualEnvProperty); }
            set { SetValue(MayNotSupportVirtualEnvPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey MayNotSupportVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly("MayNotSupportVirtualEnv",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty MayNotSupportVirtualEnvProperty =
            MayNotSupportVirtualEnvPropertyKey.DependencyProperty;

        public bool WillInstallPip {
            get { return (bool)GetValue(WillInstallPipProperty); }
            private set { SafeSetValue(WillInstallPipPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey WillInstallPipPropertyKey =
            DependencyProperty.RegisterReadOnly("WillInstallPip",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty WillInstallPipProperty =
            WillInstallPipPropertyKey.DependencyProperty;

        public bool WillInstallVirtualEnv {
            get { return (bool)GetValue(WillInstallVirtualEnvProperty); }
            private set { SafeSetValue(WillInstallVirtualEnvPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey WillInstallVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly("WillInstallVirtualEnv",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty WillInstallVirtualEnvProperty =
            WillInstallVirtualEnvPropertyKey.DependencyProperty;

        public bool WillInstallElevated {
            get { return (bool)GetValue(WillInstallElevatedProperty); }
            private set { SafeSetValue(WillInstallElevatedPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey WillInstallElevatedPropertyKey =
            DependencyProperty.RegisterReadOnly("WillInstallElevated",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty WillInstallElevatedProperty =
            WillInstallElevatedPropertyKey.DependencyProperty;


        public ObservableCollection<InterpreterView> Interpreters {
            get { return (ObservableCollection<InterpreterView>)GetValue(InterpretersProperty); }
            private set { SetValue(InterpretersPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey InterpretersPropertyKey =
            DependencyProperty.RegisterReadOnly("Interpreters",
                typeof(ObservableCollection<InterpreterView>),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata());
        public static readonly DependencyProperty InterpretersProperty =
            InterpretersPropertyKey.DependencyProperty;



        public bool IsWorking {
            get { return (bool)GetValue(IsWorkingProperty); }
            private set { SafeSetValue(IsWorkingPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey IsWorkingPropertyKey =
            DependencyProperty.RegisterReadOnly("IsWorking",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty IsWorkingProperty =
            IsWorkingPropertyKey.DependencyProperty;

        public async Task Create() {
            IsWorking = true;
            try {
                var op = new AddVirtualEnvironmentOperation(
                    _project,
                    VirtualEnvPath,
                    BaseInterpreter.Id,
                    WillCreateVirtualEnv,
                    UseVEnv,
                    WillInstallRequirementsTxt,
                    _requirementsPath,
                    OutputWindowRedirector.GetGeneral(_project.Site)
                );
                await op.Run();
            } catch (OperationCanceledException) {
            } finally {
                IsWorking = false;
                RefreshCanCreateVirtualEnv(VirtualEnvPath);
            }
        }

        private object SafeGetValue(DependencyProperty property) {
            if (Dispatcher.CheckAccess()) {
                return GetValue(property);
            } else {
                return Dispatcher.Invoke((Func<object>)(() => GetValue(property)));
            }
        }

        private void SafeSetValue(DependencyProperty property, object value) {
            if (Dispatcher.CheckAccess()) {
                SetValue(property, value);
            } else {
                Dispatcher.BeginInvoke((Action)(() => SetValue(property, value)));
            }
        }

        private void SafeSetValue(DependencyPropertyKey property, object value) {
            if (Dispatcher.CheckAccess()) {
                SetValue(property, value);
            } else {
                Dispatcher.BeginInvoke((Action)(() => SetValue(property, value)));
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);

            var evt = PropertyChanged;
            if (evt != null) {
                evt(this, new PropertyChangedEventArgs(e.Property.Name));
            }
        }

        public void SelectInterpreter(string selection) {
            if (selection == null) {
                return;
            }

            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => SelectInterpreter(selection)));
                return;
            }

            var sel = Interpreters.FirstOrDefault(iv => iv.Id == selection);
            if (sel != null) {
                BaseInterpreter = sel;
            }
        }

    }
}
