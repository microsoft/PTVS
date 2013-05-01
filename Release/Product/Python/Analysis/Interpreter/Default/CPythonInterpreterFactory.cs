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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreterFactory : IPythonInterpreterFactory, IInterpreterWithCompletionDatabase {
        private readonly string _description;
        private readonly Guid _id;
        private readonly InterpreterConfiguration _config;
        private readonly HashSet<WeakReference> _interpreters = new HashSet<WeakReference>();
        private PythonTypeDatabase _typeDb;
        private bool _generating;
        private string[] _missingModules;
        private readonly Timer _refreshLastUpdateTimesTrigger;
        private FileSystemWatcher _libWatcher;

        public CPythonInterpreterFactory(Version version)
            : this(version, Guid.Empty, "Default interpreter", "", "", "PYTHONPATH", ProcessorArchitecture.X86) {
        }

        public CPythonInterpreterFactory(Version version, Guid id, string description, string pythonPath, string pythonwPath, string pathEnvVar, ProcessorArchitecture arch) {
            if (version == default(Version)) {
                version = new Version(2, 7);
            }
            _description = description;
            _id = id;
            _config = new CPythonInterpreterConfiguration(pythonPath, pythonwPath, pathEnvVar, arch, version);

            _refreshLastUpdateTimesTrigger = new Timer(RefreshLastUpdateTimes_Elapsed);
            Task.Factory.StartNew(() => RefreshLastUpdateTimes());

            if (!string.IsNullOrEmpty(pythonPath) && File.Exists(pythonPath)) {
                try {
                    _libWatcher = new FileSystemWatcher {
                        IncludeSubdirectories = true,
                        Path = Path.Combine(Path.GetDirectoryName(pythonPath), "lib"),
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

        internal CPythonInterpreterFactory(Version version, PythonTypeDatabase typeDb)
            : this(version, Guid.Empty, "Test interpreter", "", "", "PYTHONPATH", ProcessorArchitecture.X86) {
            _typeDb = typeDb;
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
                } else if (_typeDb.DatabaseDirectory != DatabasePath && ConfigurableDatabaseExists()) {
                    // database has been generated for this interpreter, switch to the specific version.
                    _typeDb.DatabaseCorrupt -= OnDatabaseCorrupt;
                    _typeDb = new PythonTypeDatabase(DatabasePath, Is3x);
                    _typeDb.DatabaseCorrupt += OnDatabaseCorrupt;
                }

                var res = new CPythonInterpreter(this, _typeDb);

                _interpreters.Add(new WeakReference(res));

                return res;
            }
        }

        internal PythonTypeDatabase MakeTypeDatabase() {
            if (ConfigurableDatabaseExists()) {
                var res = new PythonTypeDatabase(DatabasePath, Is3x);
                res.DatabaseCorrupt += OnDatabaseCorrupt;
                return res;
            }

            // default DB is "never" corrupt
            return PythonTypeDatabase.CreateDefaultTypeDatabase(_config.Version);
        }

        private bool ConfigurableDatabaseExists() {
            if (File.Exists(Path.Combine(DatabasePath, Is3x ? "builtins.idb" : "__builtin__.idb"))) {
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

        bool IInterpreterWithCompletionDatabase.GenerateCompletionDatabase(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            return GenerateCompletionDatabaseWorker(options, databaseGenerationCompleted);
        }

        private bool GenerateCompletionDatabaseWorker(GenerateDatabaseOptions options, Action databaseGenerationCompleted) {
            lock (this) {
                _generating = true;
                if (_libWatcher != null) {
                    _libWatcher.EnableRaisingEvents = false;
                }
            }
            string outPath = DatabasePath;

            if (!PythonTypeDatabase.Generate(
                new PythonTypeDatabaseCreationRequest() { DatabaseOptions = options, Factory = this, OutputPath = outPath },
                () => {
                    lock (_interpreters) {
                        if (ConfigurableDatabaseExists()) {
                            if (_typeDb != null) {
                                _typeDb.DatabaseCorrupt -= OnDatabaseCorrupt;
                            }

                            _typeDb = new PythonTypeDatabase(outPath, Is3x);
                            _typeDb.DatabaseCorrupt += OnDatabaseCorrupt;
                            OnNewDatabaseAvailable();
                        }
                    }
                    databaseGenerationCompleted();
                    lock (this) {
                        _generating = false;
                        if (_libWatcher != null) {
                            _libWatcher.EnableRaisingEvents = true;
                        }
                    }
                    RefreshLastUpdateTimes();
                })) {
                lock (this) {
                    _generating = false;
                    if (_libWatcher != null) {
                        _libWatcher.EnableRaisingEvents = true;
                    }
                }
                RefreshLastUpdateTimes();
                return false;
            }

            return true;
        }

        private void OnDatabaseCorrupt(object sender, EventArgs args) {
            _typeDb = PythonTypeDatabase.CreateDefaultTypeDatabase(_config.Version);
            OnNewDatabaseAvailable();

            GenerateCompletionDatabaseWorker(
                GenerateDatabaseOptions.StdLibDatabase | GenerateDatabaseOptions.BuiltinDatabase,
                () => { }
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
            lock (this) {
                if (!ConfigurableDatabaseExists() && !_generating) {
                    _generating = true;
                    ThreadPool.QueueUserWorkItem(x => GenerateCompletionDatabaseWorker(GenerateDatabaseOptions.StdLibDatabase, () => { }));
                }
            }
        }

        public bool IsCurrent {
            get {
                return !_generating && Directory.Exists(DatabasePath) && _missingModules == null;
            }
        }

        private bool Is3x {
            get {
                return Configuration.Version.Major == 3;
            }
        }

        public void NotifyInvalidDatabase() {
            if (_typeDb != null) {
                _typeDb.OnDatabaseCorrupt();
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

        static string GetPackageName(string fullName) {
            int firstDot = fullName.IndexOf('.');
            return (firstDot > 0) ? fullName.Remove(firstDot) : fullName;
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
                    var packages = new List<string>(
                        from m in missingModules
                        group m by GetPackageName(m) into groupedByPackage
                        where groupedByPackage.Count() > 1
                        orderby groupedByPackage.Key
                        select groupedByPackage.Key);

                    if (packages.Count > 0 && packages.Count < 100) {
                        return string.Format(culture,
                            "{0} modules have not been analyzed.{2}Packages include:{2}    {1}",
                            missingModules.Length,
                            string.Join(Environment.NewLine + "    ", packages),
                            Environment.NewLine
                            );
                    } else {
                        return string.Format(culture,
                            "{0} modules have not been analyzed.",
                            missingModules.Length);
                    }
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
    }
}
