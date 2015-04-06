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
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides access to an on-disk store of cached intellisense information.
    /// </summary>
    public sealed class PythonTypeDatabase : ITypeDatabaseReader {
        private readonly PythonInterpreterFactoryWithDatabase _factory;
        private readonly SharedDatabaseState _sharedState;

        /// <summary>
        /// Gets the version of the analysis format that this class reads.
        /// </summary>
        public static readonly int CurrentVersion = 25;

        private static string _completionDatabasePath;
        private static string _referencesDatabasePath;
        private static string _baselineDatabasePath;

        public PythonTypeDatabase(
            PythonInterpreterFactoryWithDatabase factory,
            IEnumerable<string> databaseDirectories = null,
            PythonTypeDatabase innerDatabase = null
        ) {
            if (innerDatabase != null && factory.Configuration.Version != innerDatabase.LanguageVersion) {
                throw new InvalidOperationException("Language versions do not match");
            }

            _factory = factory;
            if (innerDatabase != null) {
                _sharedState = new SharedDatabaseState(innerDatabase._sharedState);
            } else {
                _sharedState = new SharedDatabaseState(_factory.Configuration.Version);
            }

            if (databaseDirectories != null) {
                foreach (var d in databaseDirectories) {
                    LoadDatabase(d);
                }
            }

            _sharedState.ListenForCorruptDatabase(this);
        }

        private PythonTypeDatabase(
            PythonInterpreterFactoryWithDatabase factory,
            string databaseDirectory,
            bool isDefaultDatabase
        ) {
            _factory = factory;
            _sharedState = new SharedDatabaseState(
                factory.Configuration.Version,
                databaseDirectory,
                defaultDatabase: isDefaultDatabase
            );
        }

        public PythonTypeDatabase Clone() {
            return new PythonTypeDatabase(_factory, null, this);
        }

        public PythonTypeDatabase CloneWithNewFactory(PythonInterpreterFactoryWithDatabase newFactory) {
            return new PythonTypeDatabase(newFactory, null, this);
        }

        public PythonTypeDatabase CloneWithNewBuiltins(IBuiltinPythonModule newBuiltins) {
            var newDb = new PythonTypeDatabase(_factory, null, this);
            newDb._sharedState.BuiltinModule = newBuiltins;
            return newDb;
        }

        public IPythonInterpreterFactoryWithDatabase InterpreterFactory {
            get {
                return _factory;
            }
        }

        /// <summary>
        /// Gets the Python version associated with this database.
        /// </summary>
        public Version LanguageVersion {
            get {
                return _factory.Configuration.Version;
            }
        }

        /// <summary>
        /// Loads modules from the specified path. Except for a builtins module,
        /// these will override any currently loaded modules.
        /// </summary>
        public void LoadDatabase(string databasePath) {
            _sharedState.LoadDatabase(databasePath);
        }

        /// <summary>
        /// Asynchrously loads the specified extension module into the type
        /// database making the completions available.
        /// 
        /// If the module has not already been analyzed it will be analyzed and
        /// then loaded.
        /// 
        /// If the specified module was already loaded it replaces the existing
        /// module.
        /// 
        /// Returns a new Task which can be blocked upon until the analysis of
        /// the new extension module is available.
        /// 
        /// If the extension module cannot be analyzed an exception is reproted.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token which can be
        /// used to cancel the async loading of the module</param>
        /// <param name="extensionModuleFilename">The filename of the extension
        /// module to be loaded</param>
        /// <param name="interpreter">The Python interprefer which will be used
        /// to analyze the extension module.</param>
        /// <param name="moduleName">The module name of the extension module.</param>
        public Task LoadExtensionModuleAsync(string moduleName, string extensionModuleFilename, CancellationToken cancellationToken = default(CancellationToken)) {
            return Task.Factory.StartNew(
                new ExtensionModuleLoader(
                    this,
                    _factory,
                    moduleName,
                    extensionModuleFilename,
                    cancellationToken
                ).LoadExtensionModule
            );
        }

        public bool UnloadExtensionModule(string moduleName) {
            IPythonModule dummy;
            return _sharedState.Modules.TryRemove(moduleName, out dummy);
        }

        private static Task MakeExceptionTask(Exception e) {
            var res = new TaskCompletionSource<Task>();
            res.SetException(e);
            return res.Task;
        }

        class ExtensionModuleLoader {
            private readonly PythonTypeDatabase _typeDb;
            private readonly IPythonInterpreterFactory _factory;
            private readonly string _moduleName;
            private readonly string _extensionFilename;
            private readonly CancellationToken _cancel;

            const string _extensionModuleInfoFile = "extensions.$list";

            public ExtensionModuleLoader(PythonTypeDatabase typeDb, IPythonInterpreterFactory factory, string moduleName, string extensionFilename, CancellationToken cancel) {
                _typeDb = typeDb;
                _factory = factory;
                _moduleName = moduleName;
                _extensionFilename = extensionFilename;
                _cancel = cancel;
            }

            public void LoadExtensionModule() {
                List<string> existingModules = new List<string>();
                string dbFile = null;
                // open the file locking it - only one person can look at the "database" of per-project analysis.
                using (var fs = OpenProjectExtensionList()) {
                    dbFile = FindDbFile(_factory, _extensionFilename, existingModules, dbFile, fs);

                    if (dbFile == null) {
                        dbFile = GenerateDbFile(_factory, _moduleName, _extensionFilename, existingModules, dbFile, fs);
                    }
                }

                _typeDb._sharedState.Modules[_moduleName] = new CPythonModule(_typeDb, _moduleName, dbFile, false);
            }

            private void PublishModule(object state) {
            }

            private FileStream OpenProjectExtensionList() {
                Directory.CreateDirectory(ReferencesDatabasePath);

                for (int i = 0; i < 50 && !_cancel.IsCancellationRequested; i++) {
                    try {
                        return new FileStream(Path.Combine(ReferencesDatabasePath, _extensionModuleInfoFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    } catch (IOException) {
                        if (_cancel.CanBeCanceled) {
                            _cancel.WaitHandle.WaitOne(100);
                        } else {
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                }

                throw new CannotAnalyzeExtensionException("Cannot access per-project extension registry.");
            }

            private string GenerateDbFile(IPythonInterpreterFactory interpreter, string moduleName, string extensionModuleFilename, List<string> existingModules, string dbFile, FileStream fs) {
                // we need to generate the DB file
                dbFile = Path.Combine(ReferencesDatabasePath, moduleName + ".$project.idb");
                int retryCount = 0;
                while (File.Exists(dbFile)) {
                    dbFile = Path.Combine(ReferencesDatabasePath, moduleName + "." + ++retryCount + ".$project.idb");
                }

                using (var output = interpreter.Run(
                    PythonToolsInstallPath.GetFile("ExtensionScraper.py"),
                    "scrape",
                    "-",                                    // do not use __import__
                    extensionModuleFilename,                // extension module path
                    Path.ChangeExtension(dbFile, null)      // output file path (minus .idb)
                    )) {
                    if (_cancel.CanBeCanceled) {
                        if (WaitHandle.WaitAny(new[] { _cancel.WaitHandle, output.WaitHandle }) != 1) {
                            // we were cancelled
                            return null;
                        }
                    } else {
                        output.Wait();
                    }

                    if (output.ExitCode == 0) {
                        // [FileName]|interpGuid|interpVersion|DateTimeStamp|[db_file.idb]
                        // save the new entry in the DB file
                        existingModules.Add(
                            String.Format("{0}|{1}|{2}|{3}|{4}",
                                extensionModuleFilename,
                                interpreter.Id,
                                interpreter.Configuration.Version,
                                new FileInfo(extensionModuleFilename).LastWriteTime.ToString("O"),
                                dbFile
                            )
                        );

                        fs.Seek(0, SeekOrigin.Begin);
                        fs.SetLength(0);
                        using (var sw = new StreamWriter(fs)) {
                            sw.Write(String.Join(Environment.NewLine, existingModules));
                            sw.Flush();
                        }
                    } else {
                        throw new CannotAnalyzeExtensionException(string.Join(Environment.NewLine, output.StandardErrorLines));
                    }
                }

                return dbFile;
            }

            const int extensionModuleFilenameIndex = 0;
            const int interpreterGuidIndex = 1;
            const int interpreterVersionIndex = 2;
            const int extensionTimeStamp = 3;
            const int dbFileIndex = 4;

            /// <summary>
            /// Finds the appropriate entry in our database file and returns the name of the .idb file to be loaded or null
            /// if we do not have a generated .idb file.
            /// </summary>
            private static string FindDbFile(IPythonInterpreterFactory interpreter, string extensionModuleFilename, List<string> existingModules, string dbFile, FileStream fs) {
                var reader = new StreamReader(fs);

                string line;
                while ((line = reader.ReadLine()) != null) {
                    // [FileName]|interpGuid|interpVersion|DateTimeStamp|[db_file.idb]
                    string[] columns = line.Split('|');
                    Guid interpGuid;
                    Version interpVersion;

                    if (columns.Length != 5) {
                        // malformed data...
                        continue;
                    }

                    if (File.Exists(columns[dbFileIndex])) {
                        // db file still exists
                        DateTime lastModified;
                        if (!File.Exists(columns[extensionModuleFilenameIndex]) ||  // extension has been deleted
                            !DateTime.TryParseExact(columns[extensionTimeStamp], "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out lastModified) ||
                            lastModified != new FileInfo(extensionModuleFilename).LastWriteTime) { // extension has been modified

                            // cleanup the stale DB files as we go...
                            try {
                                File.Delete(columns[4]);
                            } catch (IOException) {
                            }
                            continue;
                        }
                    } else {
                        continue;
                    }

                    // check if this is the file we're looking for...
                    if (!Guid.TryParse(columns[interpreterGuidIndex], out interpGuid) ||            // corrupt data
                        interpGuid != interpreter.Id ||                         // not our interpreter
                        !Version.TryParse(columns[interpreterVersionIndex], out interpVersion) ||     // corrupt data
                        interpVersion != interpreter.Configuration.Version ||
                        String.Compare(columns[extensionModuleFilenameIndex], extensionModuleFilename, StringComparison.OrdinalIgnoreCase) != 0) {   // not our interpreter

                        // nope, but remember the line for when we re-write out the DB.
                        existingModules.Add(line);
                        continue;
                    }

                    // this is our file, but continue reading the other lines for when we write out the DB...
                    dbFile = columns[dbFileIndex];
                }
                return dbFile;
            }
        }

        public static PythonTypeDatabase CreateDefaultTypeDatabase(PythonInterpreterFactoryWithDatabase factory) {
            return new PythonTypeDatabase(factory, BaselineDatabasePath, isDefaultDatabase: true);
        }

        internal static PythonTypeDatabase CreateDefaultTypeDatabase(Version languageVersion) {
            return new PythonTypeDatabase(InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(languageVersion),
                BaselineDatabasePath, isDefaultDatabase: true);
        }

        public IEnumerable<string> GetModuleNames() {
            return _sharedState.GetModuleNames();
        }

        public IPythonModule GetModule(string name) {
            return _sharedState.GetModule(name);
        }

        public string DatabaseDirectory {
            get {
                return _sharedState.DatabaseDirectory;
            }
        }

        public IBuiltinPythonModule BuiltinModule {
            get {
                return _sharedState.BuiltinModule;
            }
        }

        /// <summary>
        /// The exit code returned when database generation fails due to an
        /// invalid argument.
        /// </summary>
        public const int InvalidArgumentExitCode = -1;

        /// <summary>
        /// The exit code returned when database generation fails due to a
        /// non-specific error.
        /// </summary>
        public const int InvalidOperationExitCode = -2;

        /// <summary>
        /// The exit code returned when a database is already being generated
        /// for the interpreter factory.
        /// </summary>
        public const int AlreadyGeneratingExitCode = -3;

        /// <summary>
        /// The exit code returned when a database cannot be created for the
        /// interpreter factory.
        /// </summary>
        public const int NotSupportedExitCode = -4;

        public static async Task<int> GenerateAsync(PythonTypeDatabaseCreationRequest request) {
            var fact = request.Factory;
            var evt = request.OnExit;
            if (fact == null || !Directory.Exists(fact.Configuration.LibraryPath)) {
                if (evt != null) {
                    evt(NotSupportedExitCode);
                }
                return NotSupportedExitCode;
            }
            var outPath = request.OutputPath;

            var analyzerPath = PythonToolsInstallPath.GetFile("Microsoft.PythonTools.Analyzer.exe");

            Directory.CreateDirectory(CompletionDatabasePath);

            var baseDb = BaselineDatabasePath;
            if (request.ExtraInputDatabases.Any()) {
                baseDb = baseDb + ";" + string.Join(";", request.ExtraInputDatabases);
            }

            var logPath = Path.Combine(outPath, "AnalysisLog.txt");
            var glogPath = Path.Combine(CompletionDatabasePath, "AnalysisLog.txt");

            using (var output = ProcessOutput.RunHiddenAndCapture(
                analyzerPath,
                "/id", fact.Id.ToString("B"),
                "/version", fact.Configuration.Version.ToString(),
                "/python", fact.Configuration.InterpreterPath,
                request.DetectLibraryPath ? null : "/library",
                request.DetectLibraryPath ? null : fact.Configuration.LibraryPath,
                "/outdir", outPath,
                "/basedb", baseDb,
                (request.SkipUnchanged ? null : "/all"),  // null will be filtered out; empty strings are quoted
                "/log", logPath,
                "/glog", glogPath,
                "/wait", (request.WaitFor != null ? AnalyzerStatusUpdater.GetIdentifier(request.WaitFor) : "")
            )) {
                output.PriorityClass = ProcessPriorityClass.BelowNormal;
                int exitCode = await output;

                if (exitCode > -10 && exitCode < 0) {
                    try {
                        File.AppendAllLines(
                            glogPath,
                            new[] { string.Format("FAIL_STDLIB: ({0}) {1}", exitCode, output.Arguments) }
                                .Concat(output.StandardErrorLines)
                        );
                    } catch (IOException) {
                    } catch (ArgumentException) {
                    } catch (SecurityException) {
                    } catch (UnauthorizedAccessException) {
                    }
                }

                if (evt != null) {
                    evt(exitCode);
                }
                return exitCode;
            }
        }

        /// <summary>
        /// Invokes Analyzer.exe for the specified factory.
        /// </summary>
        [Obsolete("Use GenerateAsync instead")]
        public static void Generate(PythonTypeDatabaseCreationRequest request) {
            var onExit = request.OnExit;

            GenerateAsync(request).ContinueWith(t => {
                var exc = t.Exception;
                if (exc == null) {
                    return;
                }

                try {
                    var message = string.Format(
                        "ERROR_STDLIB: {0}\\{1}{2}{3}",
                        request.Factory.Id,
                        request.Factory.Configuration.Version,
                        Environment.NewLine,
                        (exc.InnerException ?? exc).ToString()
                    );

                    Debug.WriteLine(message);

                    var glogPath = Path.Combine(CompletionDatabasePath, "AnalysisLog.txt");
                    File.AppendAllText(glogPath, message);
                } catch (IOException) {
                } catch (ArgumentException) {
                } catch (SecurityException) {
                } catch (UnauthorizedAccessException) {
                }

                if (onExit != null) {
                    onExit(PythonTypeDatabase.InvalidOperationExitCode);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static bool DatabaseExists(string path) {
            string versionFile = Path.Combine(path, "database.ver");
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

        public static string GlobalLogFilename {
            get {
                return Path.Combine(CompletionDatabasePath, "AnalysisLog.txt");
            }
        }

        internal static string BaselineDatabasePath {
            get {
                if (_baselineDatabasePath == null) {
                    _baselineDatabasePath = Path.GetDirectoryName(
                        PythonToolsInstallPath.GetFile("CompletionDB\\__builtin__.idb")
                    );
                }
                return _baselineDatabasePath;
            }
        }

        public static string CompletionDatabasePath {
            get {
                if (_completionDatabasePath == null) {
                    _completionDatabasePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Python Tools",
                        "CompletionDB",
#if DEBUG
                        "Debug",
#endif
                        AssemblyVersionInfo.VSVersion
                    );
                }
                return _completionDatabasePath;
            }
        }

        private static string ReferencesDatabasePath {
            get {
                if (_referencesDatabasePath == null) {
                    _referencesDatabasePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Python Tools",
                        "ReferencesDB",
#if DEBUG
                        "Debug",
#endif
                        AssemblyVersionInfo.VSVersion
                    );
                }
                return _referencesDatabasePath;
            }
        }

        void ITypeDatabaseReader.LookupType(object type, Action<IPythonType> assign) {
            _sharedState.LookupType(type, assign);
        }

        string ITypeDatabaseReader.GetBuiltinTypeName(BuiltinTypeId id) {
            return _sharedState.GetBuiltinTypeName(id);
        }

        void ITypeDatabaseReader.ReadMember(string memberName, Dictionary<string, object> memberValue, Action<string, IMember> assign, IMemberContainer container) {
            _sharedState.ReadMember(memberName, memberValue, assign, container);
        }

        void ITypeDatabaseReader.OnDatabaseCorrupt() {
            OnDatabaseCorrupt();
        }

        public void OnDatabaseCorrupt() {
            _factory.NotifyCorruptDatabase();
        }

        internal CPythonConstant GetConstant(IPythonType type) {
            return _sharedState.GetConstant(type);
        }

        internal static bool TryGetLocation(Dictionary<string, object> table, ref int line, ref int column) {
            object value;
            if (table.TryGetValue("location", out value)) {
                object[] locationInfo = value as object[];
                if (locationInfo != null && locationInfo.Length == 2 && locationInfo[0] is int && locationInfo[1] is int) {
                    line = (int)locationInfo[0];
                    column = (int)locationInfo[1];
                    return true;
                }
            }
            return false;
        }


        public bool BeginModuleLoad(IPythonModule module, int millisecondsTimeout) {
            return _sharedState.BeginModuleLoad(module, millisecondsTimeout);
        }

        public void EndModuleLoad(IPythonModule module) {
            _sharedState.EndModuleLoad(module);
        }

        /// <summary>
        /// Returns true if the specified database has a version specified that
        /// matches the current build of PythonTypeDatabase. If false, attempts
        /// to load the database may fail with an exception.
        /// </summary>
        public static bool IsDatabaseVersionCurrent(string databasePath) {
            if (// Also ensures databasePath won't crash Path.Combine()
                Directory.Exists(databasePath) &&
                // Ensures that the database is not currently regenerating
                !File.Exists(Path.Combine(databasePath, "database.pid"))) {
                string versionFile = Path.Combine(databasePath, "database.ver");
                if (File.Exists(versionFile)) {
                    try {
                        return int.Parse(File.ReadAllText(versionFile)) == CurrentVersion;
                    } catch (IOException) {
                    } catch (UnauthorizedAccessException) {
                    } catch (SecurityException) {
                    } catch (InvalidOperationException) {
                    } catch (ArgumentException) {
                    } catch (OverflowException) {
                    } catch (FormatException) {
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the specified database is currently regenerating.
        /// </summary>
        public static bool IsDatabaseRegenerating(string databasePath) {
            return Directory.Exists(databasePath) &&
                File.Exists(Path.Combine(databasePath, "database.pid"));
        }

        /// <summary>
        /// Gets the default set of search paths based on the path to the root
        /// of the standard library.
        /// </summary>
        /// <param name="library">Root of the standard library.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        /// <remarks>New in 2.2</remarks>
        public static List<PythonLibraryPath> GetDefaultDatabaseSearchPaths(string library) {
            var result = new List<PythonLibraryPath>();
            if (!Directory.Exists(library)) {
                return result;
            }

            result.Add(new PythonLibraryPath(library, true, null));

            var sitePackages = Path.Combine(library, "site-packages");
            if (!Directory.Exists(sitePackages)) {
                return result;
            }

            result.Add(new PythonLibraryPath(sitePackages, false, null));
            result.AddRange(ModulePath.ExpandPathFiles(sitePackages)
                .Select(p => new PythonLibraryPath(p, false, null))
            );

            return result;
        }

        /// <summary>
        /// Gets the set of search paths by running the interpreter.
        /// </summary>
        /// <param name="interpreter">Path to the interpreter.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        /// <remarks>Added in 2.2</remarks>
        public static async Task<List<PythonLibraryPath>> GetUncachedDatabaseSearchPathsAsync(string interpreter) {
            List<string> lines;

            // sys.path will include the working directory, so we make an empty
            // path that we can filter out later
            var tempWorkingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempWorkingDir);

            try {
                using (var proc = ProcessOutput.Run(
                    interpreter,
                    new[] {
                        "-S",   // don't import site - we do that in code
                        "-E",   // ignore environment
                        "-c", "import sys;print('\\n'.join(sys.path));print('-');import site;site.main();print('\\n'.join(sys.path))"
                    },
                    tempWorkingDir,
                    null,
                    false,
                    null
                )) {
                    if (await proc != 0) {
                        throw new InvalidOperationException(string.Format(
                            "Cannot obtain list of paths{0}{1}",
                            Environment.NewLine,
                            string.Join(Environment.NewLine, proc.StandardErrorLines))
                        );
                    }

                    lines = proc.StandardOutputLines.ToList();
                }
            } finally {
                try {
                    Directory.Delete(tempWorkingDir, true);
                } catch (Exception ex) {
                    if (ex.IsCriticalException()) {
                        throw;
                    }
                }
            }

            var result = new List<PythonLibraryPath>();
            var treatPathsAsStandardLibrary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool builtinLibraries = true;
            foreach (var p in lines) {
                if (p == "-") {
                    if (builtinLibraries) {
                        // Seen all the builtins
                        builtinLibraries = false;
                        continue;
                    } else {
                        // Extra hyphen, so stop processing
                        break;
                    }
                }

                if (string.IsNullOrEmpty(p) || p == ".") {
                    continue;
                }

                string path;
                try {
                    if (!Path.IsPathRooted(p)) {
                        path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(interpreter), p));
                    } else {
                        path = Path.GetFullPath(p);
                    }
                } catch (ArgumentException) {
                    continue;
                } catch (PathTooLongException) {
                    continue;
                }

                if (string.Equals(p, tempWorkingDir, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (Directory.Exists(path)) {
                    if (builtinLibraries) {
                        // Looking at first section of output, which are
                        // considered to be the "standard library"
                        treatPathsAsStandardLibrary.Add(path);
                    } else {
                        // Looking at second section of output, which
                        // includes site-packages and .pth files
                        result.Add(new PythonLibraryPath(path, treatPathsAsStandardLibrary.Contains(path), null));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the set of search paths that were last saved for a database.
        /// </summary>
        /// <param name="databasePath">Path containing the database.</param>
        /// <returns>The cached list of search paths.</returns>
        /// <remarks>Added in 2.2</remarks>
        public static List<PythonLibraryPath> GetCachedDatabaseSearchPaths(string databasePath) {
            try {
                var result = new List<PythonLibraryPath>();

                using (var file = File.OpenText(Path.Combine(databasePath, "database.path"))) {
                    string line;
                    while ((line = file.ReadLine()) != null) {
                        try {
                            result.Add(PythonLibraryPath.Parse(line));
                        } catch (FormatException) {
                            Debug.Fail("Invalid format: " + line);
                        }
                    }
                }

                return result;
            } catch (IOException) {
                return null;
            }
        }

        /// <summary>
        /// Saves search paths for a database.
        /// </summary>
        /// <param name="databasePath">The path to the database.</param>
        /// <param name="paths">The list of search paths.</param>
        /// <remarks>Added in 2.2</remarks>
        public static void WriteDatabaseSearchPaths(string databasePath, IEnumerable<PythonLibraryPath> paths) {
            using (var file = new StreamWriter(Path.Combine(databasePath, "database.path"))) {
                foreach (var path in paths) {
                    file.WriteLine(path.ToString());
                }
            }
        }

        /// <summary>
        /// Returns ModulePaths representing the modules that should be analyzed
        /// for the given search paths.
        /// </summary>
        /// <param name="languageVersion">
        /// The Python language version to assume. This affects whether
        /// namespace packages are supported or not.
        /// </param>
        /// <param name="searchPaths">A sequence of paths to search.</param>
        /// <returns>
        /// All the expected modules, grouped based on codependency. When
        /// analyzing modules, all those in the same list should be analyzed
        /// together.
        /// </returns>
        /// <remarks>Added in 2.2</remarks>
        public static IEnumerable<List<ModulePath>> GetDatabaseExpectedModules(
            Version languageVersion,
            IEnumerable<PythonLibraryPath> searchPaths
        ) {
            var requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(languageVersion);

            var stdlibGroup = new List<ModulePath>();
            var packages = new List<List<ModulePath>> { stdlibGroup };

            foreach (var path in searchPaths ?? Enumerable.Empty<PythonLibraryPath>()) {
                if (path.IsStandardLibrary) {
                    stdlibGroup.AddRange(ModulePath.GetModulesInPath(
                        path.Path,
                        includeTopLevelFiles: true,
                        recurse: true,
                        // Always require __init__.py for stdlib folders
                        // Otherwise we will probably include libraries multiple
                        // times, and while Python 3.3+ allows this, it's really
                        // not a good idea.
                        requireInitPy: true
                    ));
                } else {
                    packages.Add(ModulePath.GetModulesInPath(
                        path.Path,
                        includeTopLevelFiles: true,
                        recurse: false,
                        basePackage: path.ModulePrefix
                    ).ToList());
                    packages.AddRange(ModulePath.GetModulesInPath(
                        path.Path,
                        includeTopLevelFiles: false,
                        recurse: true,
                        basePackage: path.ModulePrefix,
                        requireInitPy: requireInitPy
                    ).GroupBy(g => g.LibraryPath).Select(g => g.ToList()));
                }
            }

            return packages;
        }

    }

}
