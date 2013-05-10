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
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;

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
        private IInterpreterOptionsService _service;
        internal List<InterpreterOptions> _options = new List<InterpreterOptions>();
        private Guid _defaultInterpreter;
        private Version _defaultInterpreterVersion;

        private IInterpreterOptionsService Service {
            get {
                if (_service == null) {
                    _service = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
                }
                return _service;
            }
        }

        public PythonInterpreterOptionsPage()
            : base("Interpreters") {
            Service.InterpretersChanged += InterpretersChanged;
        }

        void InterpretersChanged(object sender, EventArgs e) {
            LoadSettingsFromStorage();
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

        public override void LoadSettingsFromStorage() {
            _defaultInterpreter = Service.DefaultInterpreter.Id;
            _defaultInterpreterVersion = Service.DefaultInterpreter.Configuration.Version;

            _options.Clear();
            foreach (var interpreter in Service.InterpretersOrDefault) {
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

        public override void SaveSettingsToStorage() {
            Service.BeginSuppressInterpretersChangedEvent();
            try {
                var configurable = Service.KnownProviders.OfType<IPythonConfigurableInterpreterFactoryProvider>().First();

                foreach (var option in _options) {
                    if (option.Removed) {
                        if (!option.Added) {
                            // it was added and then immediately removed, don't save it.
                            configurable.RemoveInterpreter(option.Id);
                        }
                        continue;
                    } else if (option.Added) {
                        if (option.Id == Guid.Empty) {
                            option.Id = Guid.NewGuid();
                        }
                        option.Added = false;
                    }

                    if (option.IsConfigurable) {
                        // save configurable interpreter options
                        configurable.SetOptions(
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
                    }
                }

                var defaultInterpreter = GetWindow().GetOption(GetWindow().DefaultInterpreter);

                if (defaultInterpreter != null) {
                    Service.DefaultInterpreter = 
                        Service.FindInterpreter(defaultInterpreter.Id, defaultInterpreter.Version) ??
                        Service.Interpreters.LastOrDefault();
                } else {
                    Service.DefaultInterpreter = Service.Interpreters.LastOrDefault();
                }
                _defaultInterpreter = Service.DefaultInterpreter.Id;
                _defaultInterpreterVersion = Service.DefaultInterpreter.Configuration.Version;
            } finally {
                Service.EndSuppressInterpretersChangedEvent();
            }
        }

        private static string GetInterpreterSettingPath(Guid id, Version version) {
            return id.ToString() + "\\" + version.ToString() + "\\";
        }

        /// <summary>
        /// Gets or sets the default interpreter ID.
        /// </summary>
        /// <remarks
        /// The actual default will only be changed if an interpreter is
        /// available that matches both <see cref="DefaultInterpreter"/> and
        /// <see cref="DefaultInterpreterVersion"/>. These properties may be
        /// invalid if an interpreter does not exist and settings have not been
        /// saved or loaded recently.
        /// 
        /// Use <see cref="IInterpreterOptionsService"/> to accurately determine
        /// the default interpreter.
        /// </remarks>
        public Guid DefaultInterpreter {
            get {
                return _defaultInterpreter;
            }
            set {
                if (_defaultInterpreter != value) {
                    _defaultInterpreter = value;
                    var newDefault = Service.FindInterpreter(_defaultInterpreter, _defaultInterpreterVersion);
                    if (newDefault != null) {
                        Service.DefaultInterpreter = newDefault;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the default interpreter version. This should be a
        /// string in "major.minor" format, for example, "3.2".
        /// </summary>
        /// <remarks
        /// The actual default will only be changed if an interpreter is
        /// available that matches both <see cref="DefaultInterpreter"/> and
        /// <see cref="DefaultInterpreterVersion"/>. These properties may be
        /// invalid if an interpreter does not exist and settings have not been
        /// saved or loaded recently.
        /// 
        /// Use <see cref="IInterpreterOptionsService"/> to accurately determine
        /// the default interpreter.
        /// </remarks>
        public string DefaultInterpreterVersion {
            get {
                return _defaultInterpreterVersion.ToString();
            }
            set {
                var ver = Version.Parse(value);
                if (_defaultInterpreterVersion != ver) {
                    _defaultInterpreterVersion = ver;
                    var newDefault = Service.FindInterpreter(_defaultInterpreter, _defaultInterpreterVersion);
                    if (newDefault != null) {
                        Service.DefaultInterpreter = newDefault;
                    }
                }
            }
        }
    }
}
