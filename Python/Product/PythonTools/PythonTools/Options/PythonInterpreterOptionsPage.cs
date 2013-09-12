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
using System.Diagnostics;
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
        private readonly IInterpreterOptionsService _service;
        internal Dictionary<IPythonInterpreterFactory, InterpreterOptions> _options = new Dictionary<IPythonInterpreterFactory, InterpreterOptions>();
        private Guid _defaultInterpreter;
        private Version _defaultInterpreterVersion;

        public PythonInterpreterOptionsPage()
            : base("Interpreters") {
            _service = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
            _service.InterpretersChanged += InterpretersChanged;
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
            var configurable = _service.KnownProviders.OfType<ConfigurablePythonInterpreterFactoryProvider>().FirstOrDefault();
            Debug.Assert(configurable != null);

            _defaultInterpreter = _service.DefaultInterpreter.Id;
            _defaultInterpreterVersion = _service.DefaultInterpreter.Configuration.Version;

            var placeholders = _options.Where(kv => kv.Key is InterpreterPlaceholder).ToArray();
            _options.Clear();
            foreach (var interpreter in _service.Interpreters) {
                _options[interpreter] = new InterpreterOptions {
                    Display = interpreter.Description,
                    Id = interpreter.Id,
                    InterpreterPath = interpreter.Configuration.InterpreterPath,
                    WindowsInterpreterPath = interpreter.Configuration.WindowsInterpreterPath,
                    LibraryPath = interpreter.Configuration.LibraryPath,
                    Version = interpreter.Configuration.Version.ToString(),
                    Architecture = FormatArchitecture(interpreter.Configuration.Architecture),
                    PathEnvironmentVariable = interpreter.Configuration.PathEnvironmentVariable,
                    IsConfigurable = configurable != null && configurable.IsConfigurable(interpreter),
                    SupportsCompletionDb = interpreter is IPythonInterpreterFactoryWithDatabase,
                    Factory = interpreter
                };
            }

            foreach (var kv in placeholders) {
                _options[kv.Key] = kv.Value;
            }

            if (_window != null) {
                _window.UpdateInterpreters();
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
            _service.BeginSuppressInterpretersChangedEvent();
            try {
                var configurable = _service.KnownProviders.OfType<ConfigurablePythonInterpreterFactoryProvider>().FirstOrDefault();
                Debug.Assert(configurable != null);

                if (configurable != null) {
                    foreach (var option in _options.Values) {
                        if (option.Removed) {
                            if (!option.Added) {
                                // if it was added and then immediately removed, don't save it.
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
                            var actualFactory = configurable.SetOptions(
                                new InterpreterFactoryCreationOptions {
                                    Id = option.Id,
                                    InterpreterPath = option.InterpreterPath ?? "",
                                    WindowInterpreterPath = option.WindowsInterpreterPath ?? "",
                                    LibraryPath = option.LibraryPath ?? "",
                                    PathEnvironmentVariableName = option.PathEnvironmentVariable ?? "",
                                    ArchitectureString = option.Architecture ?? "x86",
                                    LanguageVersionString = option.Version ?? "2.7",
                                    Description = option.Display,
                                }
                            );
                            if (option.InteractiveOptions != null) {
                                PythonToolsPackage.Instance.InteractiveOptionsPage.SaveOptions(actualFactory, option.InteractiveOptions);
                            }
                        }
                    }
                }

                var defaultInterpreter = GetWindow().DefaultInterpreter;

                if (defaultInterpreter != null) {
                    Version ver;
                    if (defaultInterpreter is InterpreterPlaceholder) {
                        ver = Version.Parse(_options[defaultInterpreter].Version ?? "2.7");
                    } else {
                        ver = defaultInterpreter.Configuration.Version;
                    }

                    // Search for the interpreter again, since it may be a
                    // placeholder rather than the actual instance.
                    _service.DefaultInterpreter = 
                        _service.FindInterpreter(defaultInterpreter.Id, ver) ??
                        _service.Interpreters.LastOrDefault();
                } else {
                    _service.DefaultInterpreter = _service.Interpreters.LastOrDefault();
                }
                _defaultInterpreter = _service.DefaultInterpreter.Id;
                _defaultInterpreterVersion = _service.DefaultInterpreter.Configuration.Version;

                foreach (var factory in _options.Keys.OfType<InterpreterPlaceholder>().ToArray()) {
                    _options.Remove(factory);
                }
            } finally {
                _service.EndSuppressInterpretersChangedEvent();
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
                    var newDefault = _service.FindInterpreter(_defaultInterpreter, _defaultInterpreterVersion);
                    if (newDefault != null) {
                        _service.DefaultInterpreter = newDefault;
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
                    var newDefault = _service.FindInterpreter(_defaultInterpreter, _defaultInterpreterVersion);
                    if (newDefault != null) {
                        _service.DefaultInterpreter = newDefault;
                    }
                }
            }
        }
    }
}
