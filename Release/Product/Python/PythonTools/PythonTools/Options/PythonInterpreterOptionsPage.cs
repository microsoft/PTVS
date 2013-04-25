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
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Provides the dialog page for configuring the interpreter options.  Accessible via automation via 
    /// dte.get_Properties("Python Tools", "Interpreters") and exposes the following values:
    /// 
    ///     DefaultInterpreter  -       Guid   - The ID of the default interpreter
    ///     DefaultInterpreterVersion - string - The version number of the default interpreter
    ///     
    /// New in 1.5.
    /// </summary>
    [ComVisible(true)]
    public sealed class PythonInterpreterOptionsPage : PythonDialogPage {
        private PythonInterpreterOptionsControl _window;
        internal List<InterpreterOptions> _options = new List<InterpreterOptions>();
        private Guid _defaultInterpreter;
        private Version _defaultInterpreterVersion;

        public PythonInterpreterOptionsPage()
            : base("Interpreters") {
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                return GetWindow();
            }
        }

        private PythonInterpreterOptionsControl GetWindow() {
            if (_window == null) {
                _window = new PythonInterpreterOptionsControl();
            }
            return _window;
        }

        public override void ResetSettings() {
            _defaultInterpreter = Guid.Empty;
            _defaultInterpreterVersion = new Version();
        }

        private const string DefaultInterpreterSetting = "DefaultInterpreter";
        private const string DefaultInterpreterVersionSetting = "DefaultInterpreterVersion";

        public override void LoadSettingsFromStorage() {
            _defaultInterpreter = GetDefaultInterpreterId();
            _defaultInterpreterVersion = GetDefaultInterpreterVersion();

            var model = (IComponentModel)PythonToolsPackage.GetGlobalService(typeof(SComponentModel));
            var interpreters = model.GetAllPythonInterpreterFactories();
            _options.Clear();
            foreach (var interpreter in interpreters) {
                _options.Add(
                    new InterpreterOptions() {
                        Display = interpreter.GetInterpreterDisplay(),
                        Id = interpreter.Id,
                        InterpreterPath = interpreter.Configuration.InterpreterPath,
                        WindowsInterpreterPath = interpreter.Configuration.WindowsInterpreterPath,
                        Version = interpreter.Configuration.Version.ToString(),
                        Architecture = FormatArchitecture(interpreter.Configuration.Architecture),
                        PathEnvironmentVariable = interpreter.Configuration.PathEnvironmentVariable,
                        IsConfigurable = interpreter is ConfigurablePythonInterpreterFactory,
                        SupportsCompletionDb = interpreter is IInterpreterWithCompletionDatabase,
                        Factory = interpreter
                    }
                );
            }

            if (_window != null) {
                _window.InitInterpreters();
            }
        }

        private static string FormatArchitecture(ProcessorArchitecture arch) {
            switch (arch) {
                case ProcessorArchitecture.Amd64: return "x64";
                case ProcessorArchitecture.X86: return "x86";
                default: return "Unknown";
            }
        }

        private Version GetDefaultInterpreterVersion() {
            string defaultInterpreterVersion = LoadString(DefaultInterpreterVersionSetting) ?? String.Empty;
            if (!String.IsNullOrEmpty(defaultInterpreterVersion)) {
                return new Version(defaultInterpreterVersion);
            }
            return new Version();
        }

        private Guid GetDefaultInterpreterId() {
            string defaultInterpreter = LoadString(DefaultInterpreterSetting) ?? String.Empty;
            if (!String.IsNullOrEmpty(defaultInterpreter)) {
                return new Guid(defaultInterpreter);
            }
            return Guid.Empty;
        }

        public override void SaveSettingsToStorage() {
            var defaultInterpreter = GetWindow().GetOption(GetWindow().DefaultInterpreter);

            if (defaultInterpreter != null) {
                Version defaultVersion;
                if (!Version.TryParse(defaultInterpreter.Version, out defaultVersion)) {
                    defaultVersion = new Version(2, 6);
                    defaultInterpreter.Version = "2.6";
                }

                if (defaultInterpreter.Id == Guid.Empty) {
                    defaultInterpreter.Id = Guid.NewGuid();
                }

                if (defaultInterpreter.Id != GetDefaultInterpreterId() || defaultVersion != GetDefaultInterpreterVersion()) {
                    _defaultInterpreter = defaultInterpreter.Id;
                    _defaultInterpreterVersion = defaultVersion;

                    SaveString(DefaultInterpreterSetting, defaultInterpreter.Id.ToString());
                    SaveString(DefaultInterpreterVersionSetting, defaultVersion.ToString());

                    var defaultInterpChanged = DefaultInterpreterChanged;
                    if (defaultInterpChanged != null) {
                        DefaultInterpreterChanged(this, EventArgs.Empty);
                    }
                }
            } else {
                _defaultInterpreter = Guid.Empty;
                _defaultInterpreterVersion = new Version();
            }

            var model = (IComponentModel)PythonToolsPackage.GetGlobalService(typeof(SComponentModel));
            var configurable = model.GetService<IPythonConfigurableInterpreterFactoryProvider>();

            for (int i = 0; i < _options.Count; ) {
                var option = _options[i];

                bool added = false;
                if (option.Removed) {
                    if (!option.Added) {
                        // it was added and then immediately removed, don't save it.
                        configurable.RemoveInterpreter(option.Id);
                    }
                    _options.RemoveAt(i);
                    continue;
                } else if (option.Added) {
                    if (option.Id == Guid.Empty) {
                        option.Id = Guid.NewGuid();
                    }
                    option.Added = false;
                    added = true;
                }

                if (_defaultInterpreter == Guid.Empty) {
                    _defaultInterpreter = _options[i].Id;
                    Version defaultVers;
                    if (!Version.TryParse(_options[i].Version, out defaultVers)) {
                        defaultVers = new Version(2, 6);
                        _options[i].Version = "2.6";
                    }
                    _defaultInterpreterVersion = defaultVers;
                }

                if (option.IsConfigurable) {
                    // save configurable interpreter options
                    var fact = configurable.SetOptions(
                        option.Id,
                        new Dictionary<string, object>() {
                            { "InterpreterPath", option.InterpreterPath ?? "" },
                            { "WindowsInterpreterPath", option.WindowsInterpreterPath ?? "" },
                            { "PathEnvironmentVariable", option.PathEnvironmentVariable ?? "" },
                            { "Architecture", option.Architecture ?? "x86" },
                            { "Version", option.Version ?? "2.6" },
                            { "Description", option.Display },
                        }
                    );

                    if (added) {
                        if (PythonToolsPackage.Instance.InteractiveOptionsPage._window != null) {
                            PythonToolsPackage.Instance.InteractiveOptionsPage._window.NewInterpreter(fact);
                        }
                    }
                }

                i++;
            }

            var interpChanged = InterpretersChanged;
            if (interpChanged != null) {
                interpChanged(this, EventArgs.Empty);
            }
        }

        internal event EventHandler InterpretersChanged;
        internal event EventHandler DefaultInterpreterChanged;

        private static string GetInterpreterSettingPath(Guid id, Version version) {
            return id.ToString() + "\\" + version.ToString() + "\\";
        }

        public Guid DefaultInterpreter {
            get {
                return DefaultInterpreterValue;
            }
            set {
                if (value != DefaultInterpreterValue) {
                    DefaultInterpreterValue = value;
                    RaiseDefaultInterpreterChanged();
                }
            }
        }

        public string DefaultInterpreterVersion {
            get {
                return DefaultInterpreterVersionValue.ToString();
            }
            set {
                var newValue = Version.Parse(value);
                if (newValue != DefaultInterpreterVersionValue) {
                    DefaultInterpreterVersionValue = newValue;
                    RaiseDefaultInterpreterChanged();
                }
            }
        }

        internal void RaiseDefaultInterpreterChanged() {
            var changed = DefaultInterpreterChanged;
            if (changed != null) {
                DefaultInterpreterChanged(this, EventArgs.Empty);
            }
        }

        internal Guid DefaultInterpreterValue {
            get { return _defaultInterpreter; }
            set { _defaultInterpreter = value; }
        }

        internal Version DefaultInterpreterVersionValue {
            get { return _defaultInterpreterVersion; }
            set { _defaultInterpreterVersion = value; }
        }
    }
}
