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
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonInterpreterFactory : IPythonInterpreterFactory, IInterpreterWithCompletionDatabase {
        private static readonly Guid _ipyInterpreterGuid = new Guid("{80659AB7-4D53-4E0C-8588-A766116CBD46}");
        private readonly InterpreterConfiguration _config = new IronPythonInterpreterConfiguration();
        private readonly HashSet<WeakReference> _interpreters = new HashSet<WeakReference>();
        private bool _generating;

        public IronPythonInterpreterFactory() {
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public string Description {
            get { return "IronPython"; }
        }

        public Guid Id {
            get { return _ipyInterpreterGuid; }
        }

        public IPythonInterpreter CreateInterpreter() {
            var res = new IronPythonInterpreter(this);
            if (!ConfigurableDatabaseExists()) {
                _interpreters.Add(new WeakReference(res));
            }
            return res;
        }

        class IronPythonInterpreterConfiguration : InterpreterConfiguration {
            public override string InterpreterPath {
                get { return Path.Combine(IronPythonInterpreter.GetPythonInstallDir(), "ipy.exe"); }
            }

            public override string WindowsInterpreterPath {
                get { return Path.Combine(IronPythonInterpreter.GetPythonInstallDir(), "ipyw.exe"); }
            }

            public override string PathEnvironmentVariable {
                get { return "IRONPYTHONPATH"; }
            }

            public override ProcessorArchitecture Architecture {
                get { return ProcessorArchitecture.MSIL; }
            }

            public override Version Version {
                get {
                    return new Version(2, 7);
                }
            }
        }

        #region IInterpreterWithCompletionDatabase

        bool IInterpreterWithCompletionDatabase.GenerateCompletionDatabase(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            return GenerateCompletionDatabaseWorker(options, databaseGenerationCompleted);
        }

        private bool GenerateCompletionDatabaseWorker(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            string outPath = GetConfiguredDatabasePath();

            return PythonTypeDatabase.Generate(
                new PythonTypeDatabaseCreationRequest() { DatabaseOptions = options, Factory = this, OutputPath = outPath },
                () => {
                    OnNewDatabaseAvailable();
                    databaseGenerationCompleted();
                    _generating = false;
                }
            );
        }

        void IInterpreterWithCompletionDatabase.AutoGenerateCompletionDatabase() {
            if (!ConfigurableDatabaseExists() && !_generating) {
                _generating = true;
                ThreadPool.QueueUserWorkItem(x => GenerateCompletionDatabaseWorker(GenerateDatabaseOptions.StdLibDatabase, () => { }));
            }
        }

        internal string GetConfiguredDatabasePath() {
            return Path.Combine(GetCompletionDatabaseDirPath(), String.Format("{0}\\{1}", Id, Configuration.Version));
        }

        private string GetCompletionDatabaseDirPath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Python Tools\\CompletionDB"
            );
        }

        internal bool ConfigurableDatabaseExists() {
            if (File.Exists(Path.Combine(GetConfiguredDatabasePath(), "builtins.idb"))) {
                string versionFile = Path.Combine(GetConfiguredDatabasePath(), "database.ver");
                if (File.Exists(versionFile)) {
                    string allLines = File.ReadAllText(versionFile);
                    int version;
                    return Int32.TryParse(allLines, out version) && version == PythonTypeDatabase.CurrentVersion;
                }
                return false;
            }
            return false;
        }

        private void OnNewDatabaseAvailable() {
            foreach (var interpreter in _interpreters) {
                var curInterpreter = interpreter.Target as IronPythonInterpreter;
                if (curInterpreter != null) {
                    curInterpreter.LoadNewTypeDb();
                }
            }
            _interpreters.Clear();
        }

        #endregion
    }
}
