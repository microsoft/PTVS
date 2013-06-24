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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project {
    sealed class AddVirtualEnvironmentView : DependencyObject, INotifyPropertyChanged {
        readonly IInterpreterOptionsService _service;
        readonly HashSet<InterpreterView> _busy;
        internal readonly string _projectHome;

        // These interpreter IDs are known to support virtualenv.
        private static readonly IEnumerable<Guid> SupportsVirtualEnv = new[] {
            CPythonInterpreterFactoryConstants.Guid32,
            CPythonInterpreterFactoryConstants.Guid64
        };


        public AddVirtualEnvironmentView(string projectHome, IInterpreterOptionsService interpService) {
            _service = interpService;
            _projectHome = projectHome;
            VirtualEnvBasePath = _projectHome;
            _busy = new HashSet<InterpreterView>();
            Interpreters = new ObservableCollection<InterpreterView>(InterpreterView.GetInterpreters(interpService));
            var def = interpService.DefaultInterpreter;
            BaseInterpreter = Interpreters.FirstOrDefault(v => v.Interpreter == def) ?? Interpreters.LastOrDefault();

            _service.InterpretersChanged += OnInterpretersChanged;

            var venvName = "env";
            for (int i = 1; Directory.Exists(Path.Combine(_projectHome, venvName)); ++i) {
                venvName = "env" + i.ToString();
            }
            VirtualEnvName = venvName;
        }

        private void OnInterpretersChanged(object sender, EventArgs e) {
            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => OnInterpretersChanged(sender, e)));
                return;
            }
            var existing = Interpreters.Where(iv => iv.Interpreter != null).ToDictionary(iv => iv.Interpreter);
            var def = _service.DefaultInterpreter;

            int i = 0;
            foreach (var interp in _service.Interpreters) {
                if (!existing.Remove(interp)) {
                    Interpreters.Insert(i, new InterpreterView(interp, interp.Description, interp == def));
                }
                i += 1;
            }
            foreach (var kv in existing) {
                Interpreters.Remove(kv.Value);
            }
        }


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
            }

            d.SetValue(VirtualEnvPathPropertyKey, CommonUtils.GetAbsoluteDirectoryPath(path, name));
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
            if (string.IsNullOrEmpty(path) ||
                path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                return false;
            }

            path = CommonUtils.TrimEndSeparator(path);
            if (File.Exists(path)) {
                return false;
            }

            var name = Path.GetFileName(path).Trim();

            if (string.IsNullOrEmpty(name) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.IndexOf('.') >= 0) {
                return false;
            }

            return true;
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

                var options = VirtualEnv.FindInterpreterOptions(path, _service);
                if (options != null) {
                    var baseInterp = _service.FindInterpreter(options.Id, options.LanguageVersion);
                    BaseInterpreter = baseInterp != null ?
                        Interpreters.FirstOrDefault(iv => iv.Interpreter == baseInterp) :
                        null;
                    WillAddVirtualEnv = (BaseInterpreter != null);
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

        private static void BaseInterpreter_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var aiv = d as AddVirtualEnvironmentView;
            if (aiv == null) {
                return;
            }

            aiv.UpdateInterpreter(e.NewValue as InterpreterView);
        }

        private void UpdateInterpreter(InterpreterView view) {
            if (!view.Dispatcher.CheckAccess()) {
                view.Dispatcher.BeginInvoke((Action)(() => UpdateInterpreter(view)));
                return;
            }

            WillInstallPipAndVirtualEnv = false;
            WillInstallVirtualEnv = false;
            MayNotSupportVirtualEnv = false;

            var interp = view.Interpreter;
            Debug.Assert(interp != null);
            if (interp == null) {
                return;
            }

            MayNotSupportVirtualEnv = !SupportsVirtualEnv.Contains(interp.Id);
            RefreshCanCreateVirtualEnv(VirtualEnvPath);

            var libPath = interp.Configuration.LibraryPath;
            if (!string.IsNullOrEmpty(libPath) && Directory.Exists(libPath)) {
                bool hasPip = false;
                bool hasVirtualEnv = false;
                Task.Factory.StartNew((Action)(() => {
                    foreach (var mp in ModulePath.GetModulesInLib(libPath)) {
                        if (!hasPip && mp.ModuleName == "pip") {
                            hasPip = true;
                        }
                        if (!hasVirtualEnv && mp.ModuleName == "virtualenv") {
                            hasVirtualEnv = true;
                        }
                        if (hasPip && hasVirtualEnv) {
                            break;
                        }
                    }
                })).ContinueWith((Action<Task>)(t => {
                    WillInstallPipAndVirtualEnv = !hasPip;
                    WillInstallVirtualEnv = !hasVirtualEnv;
                }), TaskScheduler.FromCurrentSynchronizationContext());
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

        public bool WillInstallPipAndVirtualEnv {
            get { return (bool)GetValue(WillInstallPipAndVirtualEnvProperty); }
            private set { SafeSetValue(WillInstallPipAndVirtualEnvPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey WillInstallPipAndVirtualEnvPropertyKey =
            DependencyProperty.RegisterReadOnly("WillInstallPipAndVirtualEnv",
                typeof(bool),
                typeof(AddVirtualEnvironmentView),
                new PropertyMetadata(false));
        public static readonly DependencyProperty WillInstallPipAndVirtualEnvProperty =
            WillInstallPipAndVirtualEnvPropertyKey.DependencyProperty;

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

        public void SelectInterpreter(IPythonInterpreterFactory selection) {
            if (selection == null) {
                return;
            }

            if (!Dispatcher.CheckAccess()) {
                Dispatcher.BeginInvoke((Action)(() => SelectInterpreter(selection)));
                return;
            }

            var sel = Interpreters.FirstOrDefault(iv => iv.Interpreter == selection);
            if (sel != null) {
                BaseInterpreter = sel;
            }
        }

    }
}
