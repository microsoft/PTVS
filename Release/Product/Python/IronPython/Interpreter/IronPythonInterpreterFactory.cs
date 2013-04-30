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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonInterpreterFactory : IPythonInterpreterFactory, IInterpreterWithCompletionDatabase {
        private static readonly Guid _ipyInterpreterGuid = new Guid("{80659AB7-4D53-4E0C-8588-A766116CBD46}");
        private static readonly Guid _ipy64InterpreterGuid = new Guid("{FCC291AA-427C-498C-A4D7-4502D6449B8C}");
        private readonly InterpreterConfiguration _config;
        private readonly HashSet<WeakReference> _interpreters = new HashSet<WeakReference>();
        private readonly ProcessorArchitecture _arch;
        private bool _generating;
        private string[] _missingModules;
        private readonly Timer _refreshLastUpdateTimesTrigger;
        private FileSystemWatcher _libWatcher;

        public IronPythonInterpreterFactory(ProcessorArchitecture arch = ProcessorArchitecture.X86) {
            _arch = arch;
            _config = new IronPythonInterpreterConfiguration(arch);

            _refreshLastUpdateTimesTrigger = new Timer(RefreshLastUpdateTimes_Elapsed);
            Task.Factory.StartNew(() => RefreshLastUpdateTimes());

            if (string.IsNullOrEmpty(_config.InterpreterPath)) {
                throw new InvalidOperationException("IronPython is not installed.");
            }

            // Unit tests like to make lots of factories for non-existant
            // interpreters.
            if (File.Exists(_config.InterpreterPath)) {
                try {
                    _libWatcher = new FileSystemWatcher {
                        IncludeSubdirectories = true,
                        Path = Path.Combine(Path.GetDirectoryName(_config.InterpreterPath), "lib"),
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                    };
                    _libWatcher.Created += OnChanged;
                    _libWatcher.Deleted += OnChanged;
                    _libWatcher.Changed += OnChanged;
                    _libWatcher.Renamed += OnRenamed;
                    _libWatcher.EnableRaisingEvents = true;
                } catch (ArgumentException ex) {
                    Console.WriteLine("Error starting FileSystemWatcher:\r\n{0}", ex);
                }
            }
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
                get { return Path.Combine(IronPythonResolver.GetPythonInstallDir(), _arch == ProcessorArchitecture.X86 ? "ipy.exe" : "ipy64.exe"); }
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
            if (_libWatcher != null) {
                _libWatcher.EnableRaisingEvents = false;
            }
            string outPath = DatabasePath;

            if (!PythonTypeDatabase.Generate(
                new PythonTypeDatabaseCreationRequest() { DatabaseOptions = options, Factory = this, OutputPath = outPath },
                () => {
                    OnNewDatabaseAvailable();
                    databaseGenerationCompleted();
                    _generating = false;
                    if (_libWatcher != null) {
                        _libWatcher.EnableRaisingEvents = true;
                    }
                    RefreshLastUpdateTimes();
                }
            )) {
                _generating = false;
                if (_libWatcher != null) {
                    _libWatcher.EnableRaisingEvents = true;
                }
                RefreshLastUpdateTimes();
                return false;
            }
            return true;
        }

        public bool IsCurrent {
            get {
                return !_generating && Directory.Exists(DatabasePath) && _missingModules == null;
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

        public string GetAnalysisLogContent(IFormatProvider culture) {
            var analysisLog = Path.Combine(DatabasePath, "AnalysisLog.txt");
            if (File.Exists(analysisLog)) {
                try {
                    return File.ReadAllText(analysisLog);
                } catch (Exception e) {
                    return string.Format(culture, "Error reading: {0}", e);
                }
            }
            return null;
        }

        private void RefreshLastUpdateTimes() {
            bool initialValue = IsCurrent;

            if (Directory.Exists(DatabasePath)) {
                var missingModules = ModulePath.GetModulesInLib(this)
                    // TODO: Remove IsCompiled check when pyds referenced by pth files are properly analyzed
                    .Where(mp => !mp.IsCompiled)
                    .Select(mp => mp.ModuleName)
                    .Except(Directory.EnumerateFiles(DatabasePath, "*.idb").Select(f => Path.GetFileNameWithoutExtension(f)), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.InvariantCultureIgnoreCase)
                    .ToArray();

                if (missingModules.Length > 0) {
                    _missingModules = missingModules;
                } else {
                    _missingModules = null;
                }
            }

            OnIsCurrentChanged();
        }

        private void RefreshLastUpdateTimes_Elapsed(object state) {
            RefreshLastUpdateTimes();
        }

        private void OnRenamed(object sender, RenamedEventArgs e) {
            _refreshLastUpdateTimesTrigger.Change(1000, Timeout.Infinite);
        }

        private void OnChanged(object sender, FileSystemEventArgs e) {
            _refreshLastUpdateTimesTrigger.Change(1000, Timeout.Infinite);
        }

        public event EventHandler IsCurrentChanged;

        private void OnIsCurrentChanged() {
            var evt = IsCurrentChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public string GetIsCurrentReason(IFormatProvider culture) {
            var missingModules = _missingModules;
            if (_generating) {
                return "Currently regenerating";
            } else if (!Directory.Exists(DatabasePath)) {
                return "Database has never been generated";
            } else if (missingModules != null) {
                if (missingModules.Length < 100) {
                    return string.Format(culture,
                        "The following modules have not been analyzed:{0}    {1}",
                        Environment.NewLine,
                        string.Join(Environment.NewLine + "    ", missingModules)
                        );
                } else {
                    return string.Format(culture,
                        "{0} modules have not been analyzed.",
                        missingModules.Length
                        );
                }
            }

            return "Up to date";
        }

        public string GetIsCurrentReasonNonUI(IFormatProvider culture) {
            var missingModules = _missingModules;
            var reason = "Database at " + DatabasePath;
            if (_generating) {
                return reason + " is regenerating";
            } else if (!Directory.Exists(DatabasePath)) {
                return reason + " does not exist";
            } else if (missingModules != null) {
                return reason + " does not contain the following modules:" + Environment.NewLine +
                    string.Join(Environment.NewLine, missingModules);
            }

            return reason + " is up to date";
        }

        #endregion
    }
}
