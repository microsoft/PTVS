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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreterFactory : IPythonInterpreterFactory, IInterpreterWithCompletionDatabase {
        private readonly string _description;
        private readonly Guid _id;
        private readonly InterpreterConfiguration _config;
        private readonly HashSet<WeakReference> _interpreters = new HashSet<WeakReference>();
        private PythonTypeDatabase _typeDb;
        private bool _generating;

        public CPythonInterpreterFactory(Version version, Guid id, string description, string pythonPath, string pythonwPath, string pathEnvVar, ProcessorArchitecture arch) {
            if (version == default(Version)) {
                version = new Version(2, 7);
            }
            _description = description;
            _id = id;
            _config = new CPythonInterpreterConfiguration(pythonPath, pythonwPath, pathEnvVar, arch, version);
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public string Description {
            get { return _description; }
        }

        public Guid Id {
            get { return _id; }
        }

        public IPythonInterpreter CreateInterpreter() {
            lock (_interpreters) {
                if (_typeDb == null) {
                    _typeDb = MakeTypeDatabase();
                } else if (_typeDb.DatabaseDirectory != GetConfiguredDatabasePath() && ConfigurableDatabaseExists()) {
                    // database has been generated for this interpreter, switch to the specific version.
                    _typeDb = new PythonTypeDatabase(GetConfiguredDatabasePath(), Is3x);
                }

                var res = new CPythonInterpreter(_typeDb);

                if (!ConfigurableDatabaseExists()) {
                    _interpreters.Add(new WeakReference(res));
                }
                return res;
            }
        }

        internal PythonTypeDatabase MakeTypeDatabase() {
            if (ConfigurableDatabaseExists()) {
                return new PythonTypeDatabase(GetConfiguredDatabasePath(), Is3x);
            }
            return PythonTypeDatabase.CreateDefaultTypeDatabase();
        }

        private bool ConfigurableDatabaseExists() {
            if (File.Exists(Path.Combine(GetConfiguredDatabasePath(), Is3x ? "builtins.idb" : "__builtin__.idb"))) {
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

        private string GetCompletionDatabaseDirPath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Python Tools\\CompletionDB"
            );
        }

        private string GetConfiguredDatabasePath() {
            return Path.Combine(GetCompletionDatabaseDirPath(), String.Format("{0}\\{1}", Id, Configuration.Version));
        }

        bool IInterpreterWithCompletionDatabase.GenerateCompletionDatabase(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            return GenerateCompletionDatabaseWorker(options, databaseGenerationCompleted);
        }

        private bool GenerateCompletionDatabaseWorker(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            string outPath = GetConfiguredDatabasePath();

            return PythonTypeDatabase.Generate(
                new PythonTypeDatabaseCreationRequest() { DatabaseOptions = options, Factory = this, OutputPath = outPath },
                () => {
                    lock (_interpreters) {
                        if (ConfigurableDatabaseExists()) {
                            _typeDb = new PythonTypeDatabase(outPath, Is3x);

                            OnNewDatabaseAvailable();
                        }
                    }
                    databaseGenerationCompleted();

                    _generating = false;
                }
            );
        }

        private void OnNewDatabaseAvailable() {
            foreach (var interpreter in _interpreters) {
                var curInterpreter = interpreter.Target as CPythonInterpreter;
                if (curInterpreter != null) {
                    curInterpreter.TypeDb = _typeDb;
                }
            }
            _interpreters.Clear();
        }

        void IInterpreterWithCompletionDatabase.AutoGenerateCompletionDatabase() {
            if (!ConfigurableDatabaseExists() && !_generating) {
                _generating = true;
                ThreadPool.QueueUserWorkItem(x => GenerateCompletionDatabaseWorker(GenerateDatabaseOptions.StdLibDatabase, () => { }));
            }
        }

        private bool Is3x {
            get {
                return Configuration.Version.Major == 3;
            }
        }
    }
}
