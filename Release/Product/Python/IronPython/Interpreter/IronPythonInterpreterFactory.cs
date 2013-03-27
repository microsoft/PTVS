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
        private static readonly Guid _ipy64InterpreterGuid = new Guid("{FCC291AA-427C-498C-A4D7-4502D6449B8C}");
        private readonly InterpreterConfiguration _config;
        private readonly HashSet<WeakReference> _interpreters = new HashSet<WeakReference>();
        private readonly ProcessorArchitecture _arch;
        private bool _generating;

        public IronPythonInterpreterFactory(ProcessorArchitecture arch = ProcessorArchitecture.X86) {
            _arch = arch;
            _config = new IronPythonInterpreterConfiguration(arch);
        }

        public InterpreterConfiguration Configuration {
            get {
                return _config;
            }
        }

        public string Description {
            get {
                if (_arch == ProcessorArchitecture.X86) {
                    return "IronPython";
                }

                return "IronPython 64-bit";
            }
        }

        public Guid Id {
            get {
                if (_arch == ProcessorArchitecture.X86) {
                    return _ipyInterpreterGuid;
                }
                return _ipy64InterpreterGuid;
            }
        }

        public IPythonInterpreter CreateInterpreter() {
            var res = new IronPythonInterpreter(this);
            if (!ConfigurableDatabaseExists()) {
                _interpreters.Add(new WeakReference(res));
            }
            return res;
        }

        class IronPythonInterpreterConfiguration : InterpreterConfiguration {
            private readonly ProcessorArchitecture _arch;
            
            public IronPythonInterpreterConfiguration(ProcessorArchitecture arch) {
                _arch = arch;
            }

            public override string InterpreterPath {
                get { 
                    return Path.Combine(IronPythonResolver.GetPythonInstallDir(), _arch == ProcessorArchitecture.X86 ? "ipy.exe" : "ipy64.exe"); 
                }
            }

            public override string WindowsInterpreterPath {
                get { return Path.Combine(IronPythonResolver.GetPythonInstallDir(), _arch == ProcessorArchitecture.X86 ? "ipyw.exe" : "ipyw64.exe"); }
            }

            public override string PathEnvironmentVariable {
                get { return "IRONPYTHONPATH"; }
            }

            public override ProcessorArchitecture Architecture {
                get { return _arch; }
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
            _generating = true;
            string outPath = DatabasePath;

            if (!PythonTypeDatabase.Generate(
                new PythonTypeDatabaseCreationRequest() { DatabaseOptions = options, Factory = this, OutputPath = outPath },
                () => {
                    OnNewDatabaseAvailable();
                    databaseGenerationCompleted();
                    _generating = false;
                }
            )) {
                _generating = false;
                return false;
            }
            return true;
        }

        public bool IsCurrent {
            get {
                return !_generating;
            }
        }

        void IInterpreterWithCompletionDatabase.AutoGenerateCompletionDatabase() {
            if (!ConfigurableDatabaseExists() && !_generating) {
                ThreadPool.QueueUserWorkItem(x => GenerateCompletionDatabaseWorker(GenerateDatabaseOptions.StdLibDatabase, () => { }));
            }
        }

        internal bool ConfigurableDatabaseExists() {
            if (File.Exists(Path.Combine(DatabasePath, "__builtin__.idb"))) {
                string versionFile = Path.Combine(DatabasePath, "database.ver");
                if (File.Exists(versionFile)) {
                    try {
                        string allLines = File.ReadAllText(versionFile);
                        int version;
                        return Int32.TryParse(allLines, out version) && version == PythonTypeDatabase.CurrentVersion;
                    } catch (IOException) {
                    }
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

        public void NotifyInvalidDatabase() {
            foreach (var interpreter in _interpreters) {
                var curInterpreter = interpreter.Target as IronPythonInterpreter;
                if (curInterpreter != null) {
                    curInterpreter.NotifyInvalidDatabase();
                }
            }
        }

        public string DatabasePath {
            get {
                return Path.Combine(PythonTypeDatabase.CompletionDatabasePath, String.Format("{0}\\{1}", Id, Configuration.Version));
            }
        }

        public string GetAnalysisLogContent() {
            var analysisLog = Path.Combine(DatabasePath, "AnalysisLog.txt");
            if (File.Exists(analysisLog)) {
                try {
                    return File.ReadAllText(analysisLog);
                } catch (Exception e) {
                    return "Error reading: " + e;
                }
            }
            return null;
        }

        #endregion

    }
}
