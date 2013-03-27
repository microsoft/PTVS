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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides access to an on-disk store of cached intellisense information.
    /// </summary>
    public sealed class PythonTypeDatabase : ITypeDatabaseReader {
        private readonly SharedDatabaseState _sharedState;
        internal readonly Dictionary<string, IPythonModule> _modules;
        internal readonly Dictionary<IPythonType, CPythonConstant> _constants;

        /// <summary>
        /// Gets the version of the analysis format that this class reads.
        /// </summary>
        public static readonly int CurrentVersion = 21;

        private static string _completionDatabasePath;
        private static string _baselineDatabasePath;

        public PythonTypeDatabase(string databaseDirectory, bool is3x = false, IBuiltinPythonModule builtinsModule = null) {
            _sharedState = new SharedDatabaseState(databaseDirectory, is3x, builtinsModule);
            _sharedState.ListenForCorruptDatabase(this);
        }

        /// <summary>
        /// Constructor used for the default type database specified with a version.
        /// </summary>
        internal PythonTypeDatabase(string databaseDirectory, Version languageVersion) {
            _sharedState = new SharedDatabaseState(databaseDirectory, languageVersion);
            _sharedState.ListenForCorruptDatabase(this);
        }

        internal PythonTypeDatabase(SharedDatabaseState cloning) {
            _sharedState = cloning;
            _sharedState.ListenForCorruptDatabase(this);
            _modules = new Dictionary<string, IPythonModule>();
            _constants = new Dictionary<IPythonType, CPythonConstant>();
        }

        /// <summary>
        /// Fired when the database is discovered to be corrrupted.  This can happen because a file
        /// wasn't successfully flushed to disk, or if the user modified the database by hand.
        /// </summary>
        public event EventHandler DatabaseCorrupt;

        /// <summary>
        /// Creates a light weight copy of this PythonTypeDatabase which supports adding 
        /// </summary>
        /// <returns></returns>
        public PythonTypeDatabase Clone() {
            if (_modules != null) {
                throw new InvalidOperationException("Cannot clone an already cloned type database");
            }
            return new PythonTypeDatabase(_sharedState);
        }

        /// <summary>
        /// Asynchrousnly loads the specified extension module into the type database making the completions available.
        /// 
        /// If the module has not already been analyzed it will be analyzed and then loaded.
        /// 
        /// If the specified module was already loaded it replaces the existing module.
        /// 
        /// Returns a new Task which can be blocked upon until the analysis of the new extension module is available.
        /// 
        /// If the extension module cannot be analyzed an exception is reproted.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token which can be used to cancel the async loading of the module</param>
        /// <param name="extensionModuleFilename">The filename of the extension module to be loaded</param>
        /// <param name="interpreter">The Python interprefer which will be used to analyze the extension module.</param>
        /// <param name="moduleName">The module name of the extension module.</param>
        public Task LoadExtensionModuleAsync(IPythonInterpreterFactory interpreter, string moduleName, string extensionModuleFilename, CancellationToken cancellationToken = default(CancellationToken)) {
            if (_modules == null) {
                return MakeExceptionTask(new InvalidOperationException("Can only LoadModules into a cloned PythonTypeDatabase"));
            }

            return Task.Factory.StartNew(
                new ExtensionModuleLoader(
                    TaskScheduler.FromCurrentSynchronizationContext(), 
                    this, 
                    interpreter, 
                    moduleName, 
                    extensionModuleFilename, 
                    cancellationToken
                ).LoadExtensionModule
            );
        }

        public bool UnloadExtensionModule(string moduleName) {
            return _modules.Remove(moduleName);
        }

        private static Task MakeExceptionTask(Exception e) {
            var res = new TaskCompletionSource<Task>();
            res.SetException(e);
            return res.Task;
        }

        internal class ProcessWaitHandle : WaitHandle {
            public ProcessWaitHandle(Process process) {
                Debug.Assert(process != null);
                SafeWaitHandle = new SafeWaitHandle(process.Handle, false); // Process owns the handle
            }
        }

        class ExtensionModuleLoader {
            private readonly PythonTypeDatabase _typeDb;
            private readonly IPythonInterpreterFactory _factory;
            private readonly string _moduleName;
            private readonly string _extensionFilename;
            private readonly CancellationToken _cancel;
            private readonly TaskScheduler _startScheduler;

            const string _extensionModuleInfoFile = "extensions.$list";

            public ExtensionModuleLoader(TaskScheduler startScheduler, PythonTypeDatabase typeDb, IPythonInterpreterFactory factory, string moduleName, string extensionFilename, CancellationToken cancel) {
                _typeDb = typeDb;
                _factory = factory;
                _moduleName = moduleName;
                _extensionFilename = extensionFilename;
                _cancel = cancel;
                _startScheduler = startScheduler;
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

                // we need to access _typeDb._modules on the UI thread.
                Task.Factory.StartNew(PublishModule, dbFile, default(CancellationToken), TaskCreationOptions.None, _startScheduler).Wait();
            }

            private void PublishModule(object state) {
                _typeDb._modules[_moduleName] = new CPythonModule(_typeDb, _moduleName, (string)state, false);
            }

            private FileStream OpenProjectExtensionList() {
                for (int i = 0; i < 50 && !_cancel.IsCancellationRequested; i++) {
                    try {
                        return new FileStream(Path.Combine(_typeDb.DatabaseDirectory, _extensionModuleInfoFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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
                dbFile = Path.Combine(_typeDb._sharedState.DatabaseDirectory, moduleName + ".$project.idb");
                int retryCount = 0;
                while (File.Exists(dbFile)) {
                    dbFile = Path.Combine(_typeDb._sharedState.DatabaseDirectory, moduleName + "." + ++retryCount + ".$project.idb");
                }

                var psi = new ProcessStartInfo();
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.FileName = interpreter.Configuration.InterpreterPath;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.Arguments =
                    "\"" + Path.Combine(GetPythonToolsInstallPath(), "ExtensionScraper.py") + "\"" +      // script to run
                    " scrape" +                                                                           // scrape
                    " -" +                                                                                // no module name
                    " \"" + extensionModuleFilename + "\"" +                                              // extension module path
                    " \"" + dbFile.Substring(0, dbFile.Length - 4) + "\"";                                // output file path (minus .idb)

                var proc = Process.Start(psi);
                OutputDataReceiver receiver = new OutputDataReceiver();
                proc.OutputDataReceived += receiver.OutputDataReceived;
                proc.ErrorDataReceived += receiver.OutputDataReceived;

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (_cancel.CanBeCanceled) {
                    if (WaitHandle.WaitAny(new[] { _cancel.WaitHandle, new ProcessWaitHandle(proc) }) != 1) {
                        // we were cancelled
                        return null;
                    }
                } else {
                    proc.WaitForExit();
                }

                if (proc.ExitCode == 0) {
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
                    var sw = new StreamWriter(fs);
                    sw.Write(String.Join(Environment.NewLine, existingModules));
                    sw.Flush();
                } else {
                    throw new CannotAnalyzeExtensionException(receiver.Received.ToString());
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
                    if(!Guid.TryParse(columns[interpreterGuidIndex], out interpGuid) ||            // corrupt data
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

        /// <summary>
        /// Determines if this PythonTypeDatabase can load modules or not.  Only PythonTypeDatabases
        /// which have been cloned can load modules.  All type databases which are cloned off of a parent
        /// support adding modules and will share the common information of the parent type database.
        /// 
        /// Once a type database has been cloned it cannot be cloned again.
        /// </summary>
        public bool CanLoadModules {
            get {
                return _modules != null;
            }
        }

        public static PythonTypeDatabase CreateDefaultTypeDatabase() {
            return new PythonTypeDatabase(BaselineDatabasePath);
        }

        public static PythonTypeDatabase CreateDefaultTypeDatabase(Version pythonLanguageVersion) {
            return new PythonTypeDatabase(BaselineDatabasePath, pythonLanguageVersion);
        }

        public IEnumerable<string> GetModuleNames() {
            foreach (var key in _sharedState.Modules.Keys) {
                yield return key;
            }

            if (_modules != null) {
                foreach (var key in _modules.Keys) {
                    yield return key;
                }
            }
        }

        public IPythonModule GetModule(string name) {
            IPythonModule res;
            if (_sharedState.Modules.TryGetValue(name, out res)) {
                return res;
            }

            if (_modules != null && _modules.TryGetValue(name, out res)) {
                return res;
            }

            return null;
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
            set {
                _sharedState.BuiltinModule = value;
            }
        }

        /// <summary>
        /// Creates a new completion database based upon the specified request.  Calls back the provided delegate when
        /// the generation has finished.
        /// </summary>
        public static bool Generate(PythonTypeDatabaseCreationRequest request, Action databaseGenerationCompleted) {
            if (String.IsNullOrEmpty(request.Factory.Configuration.InterpreterPath)) {
                return false;
            }

            string outPath = request.OutputPath;

            Thread t = new Thread(x => {
                if (!Directory.Exists(outPath)) {
                    Directory.CreateDirectory(outPath);
                }

                var psi = new ProcessStartInfo();
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.FileName = request.Factory.Configuration.InterpreterPath;
                psi.Arguments =
                    "\"" + Path.Combine(GetPythonToolsInstallPath(), "PythonScraper.py") + "\"" +       // script to run
                    " \"" + outPath + "\"" +                       // output dir
                    " \"" + BaselineDatabasePath + "\"";           // baseline file
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;

                var proc = new Process();
                proc.StartInfo = psi;
                StringBuilder output = new StringBuilder();
                try {
                    LogEvent(request, "START_SCRAPE " + psi.Arguments);

                    proc.Start();
                    proc.BeginErrorReadLine();
                    proc.BeginOutputReadLine();
                    proc.OutputDataReceived += (sender, args) => output.AppendLine(args.Data);
                    proc.ErrorDataReceived += (sender, args) => output.AppendLine(args.Data);
                    proc.WaitForExit();
                    LogEvent(request, "OUTPUT\r\n    " + output.Replace("\r\n", "\r\n    "));
                } catch (Win32Exception ex) {
                    // failed to start process, interpreter doesn't exist?           
                    LogEvent(request, "FAIL_SCRAPE " + ex.ToString().Replace("\r\n", " -- "));
                    LogEvent(request, "    " + output.Replace("\r\n", "\r\n    "));
                    databaseGenerationCompleted();
                    return;
                }

                if (proc.ExitCode != 0) {
                    LogEvent(request, "FAIL_SCRAPE " + proc.ExitCode);  
                } else {
                    LogEvent(request, "DONE (SCRAPE)");
                }

                if ((request.DatabaseOptions & GenerateDatabaseOptions.StdLibDatabase) != 0) {
                    psi = new ProcessStartInfo();
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.FileName = Path.Combine(GetPythonToolsInstallPath(), "Microsoft.PythonTools.Analyzer.exe");
                    string libDir;
                    string virtualEnvPackages;
                    GetLibDirs(request, out libDir, out virtualEnvPackages);

                    if (File.Exists(psi.FileName)) {
                        psi.Arguments = BuildArguments(request, outPath, libDir, virtualEnvPackages);

                        proc = new Process();
                        proc.StartInfo = psi;

                        try {
                            LogEvent(request, "START_STDLIB " + psi.Arguments);
                            proc.Start();
                            proc.WaitForExit();

                            if (proc.ExitCode == 0) {
                                LogEvent(request, "DONE (STDLIB)");
                            } else {
                                LogEvent(request, "FAIL_STDLIB " + proc.ExitCode);
                            }
                        } catch (Win32Exception ex) {
                            // failed to start the process           
                            LogEvent(request, "FAIL_STDLIB " + ex.ToString().Replace("\r\n", " -- "));
                        }

                        databaseGenerationCompleted();
                    }
                } else {
                    databaseGenerationCompleted();
                }
            });
            t.Start();
            return true;
        }

        private static string BuildArguments(PythonTypeDatabaseCreationRequest request, string outPath, string libDir, string virtualEnvPackages) {
            string args = "/dir " + "\"" + libDir + "\"" +
                " /version V" + request.Factory.Configuration.Version.ToString().Replace(".", "") +
                " /outdir " + "\"" + outPath + "\"" +
                " /indir " + "\"" + outPath + "\"";

            if (virtualEnvPackages != null) {
                args += " /dir \"" + virtualEnvPackages + "\"";
            }


            return args;
        }

        private static void GetLibDirs(PythonTypeDatabaseCreationRequest request, out string libDir, out string virtualEnvPackages) {
            libDir = Path.Combine(Path.GetDirectoryName(request.Factory.Configuration.InterpreterPath), "Lib");
            virtualEnvPackages = null;
            if (!Directory.Exists(libDir)) {
                string virtualEnvLibDir = Path.Combine(Path.GetDirectoryName(request.Factory.Configuration.InterpreterPath), "..\\Lib");
                string prefixFile = Path.Combine(virtualEnvLibDir, "orig-prefix.txt");
                if (Directory.Exists(virtualEnvLibDir) && File.Exists(prefixFile)) {
                    // virtual env is setup differently.  The EXE is in a Scripts directory with the Lib dir being at ..\Lib 
                    // relative to the EXEs dir.  There is alos an orig-prefix.txt which points at the normal full Python
                    // install.  Parse that file and include the normal Python install in the analysis.
                    try {
                        var lines = File.ReadAllLines(Path.Combine(prefixFile));
                        if (lines.Length >= 1 && lines[0].IndexOfAny(Path.GetInvalidPathChars()) == -1) {

                            string origLibDir = Path.Combine(lines[0], "Lib");
                            if (Directory.Exists(origLibDir)) {
                                // virtual env install
                                libDir = origLibDir;

                                virtualEnvPackages = Path.Combine(virtualEnvLibDir, "site-packages");
                            }
                        }
                    } catch (IOException) {
                    } catch (UnauthorizedAccessException) {
                    } catch (System.Security.SecurityException) {
                    }
                } else {
                    // try and find the lib dir based upon where site.py lives
                    var psi = new ProcessStartInfo(
                        request.Factory.Configuration.InterpreterPath,
                        "-c \"import site; print site.__file__\""
                    );
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;

                    var proc = Process.Start(psi);

                    OutputDataReceiver receiver = new OutputDataReceiver();
                    proc.OutputDataReceived += receiver.OutputDataReceived;
                    proc.ErrorDataReceived += receiver.OutputDataReceived;

                    proc.BeginErrorReadLine();
                    proc.BeginOutputReadLine();

                    proc.WaitForExit();

                    string siteFilename = receiver.Received.ToString().Trim();
                    
                    if (!String.IsNullOrWhiteSpace(siteFilename) &&
                        siteFilename.IndexOfAny(Path.GetInvalidPathChars()) == -1) {
                        var dirName = Path.GetDirectoryName(siteFilename);
                        if (Directory.Exists(dirName)) {
                            libDir = dirName;
                        }
                    }
                }
            }
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
                    _baselineDatabasePath = Path.Combine(GetPythonToolsInstallPath(), "CompletionDB");
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
#if DEV12
                        "12.0"
#elif DEV11
                        "11.0"
#else
                        "10.0"
#endif
                    );
                }
                return _completionDatabasePath;
            }
        }

        private static void LogEvent(PythonTypeDatabaseCreationRequest request, string contents) {
            for (int i = 0; i < 10; i++) {
                try {
                    File.AppendAllText(
                        GlobalLogFilename,
                        String.Format(
                            "\"{0}\" \"{1}\" \"{2}\" \"{3}\"{4}",
                            DateTime.Now.ToString("s"),
                            request.Factory.Configuration.InterpreterPath,
                            request.OutputPath,
                            contents,
                            Environment.NewLine
                        )
                    );
                    return;
                } catch (IOException) {
                    // racing with someone else generating?
                    Thread.Sleep(25);
                }
            }
        }

        void ITypeDatabaseReader.LookupType(object type, Action<IPythonType, bool> assign, PythonTypeDatabase instanceDb) {
            Debug.Assert(instanceDb == null);

            _sharedState.LookupType(type, assign, this);
        }

        string ITypeDatabaseReader.GetBuiltinTypeName(BuiltinTypeId id) {
            return _sharedState.GetBuiltinTypeName(id);
        }

        void ITypeDatabaseReader.RunFixups() {
            _sharedState.RunFixups();
        }

        void ITypeDatabaseReader.ReadMember(string memberName, Dictionary<string, object> memberValue, Action<string, IMember> assign, IMemberContainer container, PythonTypeDatabase instanceDb) {
            Debug.Assert(instanceDb == null);

            _sharedState.ReadMember(memberName, memberValue, assign, container, this);
        }

        void ITypeDatabaseReader.OnDatabaseCorrupt() {
            OnDatabaseCorrupt();
        }

        public void OnDatabaseCorrupt() {
            var dbCorrupt = DatabaseCorrupt;
            if (dbCorrupt != null) {
                dbCorrupt(this, EventArgs.Empty);
            }
        }

        internal CPythonConstant GetConstant(IPythonType type) {
            CPythonConstant constant;
            if (!_constants.TryGetValue(type, out constant)) {
                _constants[type] = constant = new CPythonConstant(type);
            }
            return constant;
        }

        // This is duplicated throughout different assemblies in PythonTools, so search for it if you update it.
        private static string GetPythonToolsInstallPath() {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (File.Exists(Path.Combine(path, "Microsoft.PythonTools.dll"))) {
                return path;
            }

            // running from the GAC in remote attach scenario.  Look to the VS install dir.
            using (var configKey = OpenVisualStudioKey()) {
                var installDir = configKey.GetValue("InstallDir") as string;
                if (installDir != null) {
                    var toolsPath = Path.Combine(installDir, "Extensions\\Microsoft\\Python Tools for Visual Studio\\2.0");
                    if (File.Exists(Path.Combine(toolsPath, "Microsoft.PythonTools.dll"))) {
                        return toolsPath;
                    }
                }
            }

            Debug.Assert(false, "Unable to determine Python Tools installation path");
            return string.Empty;
        }

        private static Win32.RegistryKey OpenVisualStudioKey() {
            if (Environment.Is64BitOperatingSystem) {
#if DEV12
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\12.0");
#elif DEV11
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#elif DEV10
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#else
#error Unsupported version of Visual Studio
#endif
            } else {
#if DEV12
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\12.0");
#elif DEV11
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\11.0");
#elif DEV10
                return Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\VisualStudio\\10.0");
#else
#error Unsupported version of Visual Studio
#endif
            }
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

        internal IPythonModule GetInstancedModule(string name) {
            IPythonModule res;
            if (_modules.TryGetValue(name, out res)) {
                return res;
            }
            return null;
        }

        class OutputDataReceiver {
            public readonly StringBuilder Received = new StringBuilder();

            public void OutputDataReceived(object sender, DataReceivedEventArgs e) {
                Received.Append(e.Data);
            }
        }
    }

}
