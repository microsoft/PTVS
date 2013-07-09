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
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Analysis {
    internal class PyLibAnalyzer : IDisposable {
        private const string AnalysisLimitsKey = "Software\\Microsoft\\VisualStudio\\" + AssemblyVersionInfo.VSVersion + "\\PythonTools\\Analysis\\StandardLibrary";

        private readonly Guid _id;
        private readonly Version _version;
        private readonly string _interpreter;
        private readonly string _library;
        private readonly string _outDir;
        private readonly List<string> _baseDb;
        private readonly string _logPrivate, _logGlobal, _logDiagnostic;

        private bool _all;
        private readonly HashSet<string> _needsRefresh;

        private readonly AnalyzerStatusUpdater _updater;
        private CancellationToken _cancel;
        private TextWriterTraceListener _listener;
        private List<List<ModulePath>> _fileGroups;
        private HashSet<string> _existingDatabase;

        private const string BuiltinName2x = "__builtin__.idb";
        private const string BuiltinName3x = "builtins.idb";

        private static void Help() {
            Console.WriteLine("Python Library Analyzer {0} ({1})", AssemblyVersionInfo.StableVersion, AssemblyVersionInfo.Version);
            Console.WriteLine("Generates a cached analysis database for a Python interpreter.");
            Console.WriteLine();
            Console.WriteLine(" /id         [GUID]             - specify GUID of the interpreter being used");
            Console.WriteLine(" /v[ersion]  [version]          - specify language version to be used (x.y format)");
            Console.WriteLine(" /py[thon]   [filename]         - full path to the Python interpreter to use");
            Console.WriteLine(" /lib[rary]  [directory]        - full path to the Python library to analyze");
            Console.WriteLine(" /outdir     [output dir]       - specify output directory for analysis (default is current dir)");
            Console.WriteLine(" /all                           - don't skip file groups that look up to date");

            Console.WriteLine(" /basedb     [input dir]        - specify directory for baseline analysis.");
            Console.WriteLine(" /log        [filename]         - write analysis log");
            Console.WriteLine(" /glog       [filename]         - write start/stop events");
            Console.WriteLine(" /diag       [filename]         - write detailed (CSV) analysis log");
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseArguments(IEnumerable<string> args) {
            string currentKey = null;

            using (var e = args.GetEnumerator()) {
                while (e.MoveNext()) {
                    if (e.Current.StartsWith("/")) {
                        if (currentKey != null) {
                            yield return new KeyValuePair<string, string>(currentKey, null);
                        }
                        currentKey = e.Current.Substring(1).Trim();
                    } else {
                        yield return new KeyValuePair<string, string>(currentKey, e.Current);
                        currentKey = null;
                    }
                }
            }
        }

        public static int Main(string[] args) {
            PyLibAnalyzer inst;
            try {
                inst = MakeFromArguments(args);
            } catch (ArgumentNullException ex) {
                Console.Error.WriteLine("{0} is a required argument", ex.Message);
                Help();
                return -1;
            } catch (ArgumentException ex) {
                Console.Error.WriteLine("'{0}' is not valid for {1}", ex.Message, ex.ParamName);
                Help();
                return -1;
            } catch (InvalidOperationException ex) {
                Console.Error.WriteLine(ex.Message);
                Help();
                return -2;
            }

            try {
                for (bool ready = false; !ready; ) {
                    try {
                        inst.StartTraceListener();
                        ready = true;
                    } catch (IOException) {
                        Thread.Sleep(20000);
                    }
                }

                inst.LogToGlobal("START_STDLIB");

#if DEBUG
                // Running with the debugger attached will skip the
                // unhandled exception handling to allow easier debugging.
                if (Debugger.IsAttached) {
                    // Ensure that this code block matches the protected one
                    // below.

                    inst.Prepare();
                    inst.Scrape();
                    inst.Analyze();
                    inst.Epilogue();
                    inst.Clean();
                } else {
#endif
                    try {
                        inst.Prepare();
                        inst.Scrape();
                        inst.Analyze();
                        inst.Epilogue();
                        inst.Clean();
                    } catch (Exception e) {
                        Console.WriteLine("Error while saving analysis: {0}{1}", Environment.NewLine, e.ToString());
                        inst.LogToGlobal("FAIL_STDLIB" + Environment.NewLine + e.ToString());
                        Trace.TraceError("ANALYSIS FAIL:{0}{1}", Environment.NewLine, e.ToString());
                        return -10;
                    }
#if DEBUG
                }
#endif

                inst.LogToGlobal("DONE_STDLIB");

            } finally {
                inst.Dispose();
            }

            return 0;
        }

        public PyLibAnalyzer(Guid id,
                             Version langVersion,
                             string interpreter,
                             string library,
                             List<string> baseDb,
                             string outDir,
                             string logPrivate,
                             string logGlobal,
                             string logDiagnostic,
                             bool rescanAll) {
            _id = id;
            _version = langVersion;
            _interpreter = interpreter;
            _library = library;
            _baseDb = baseDb;
            _outDir = outDir;
            _logPrivate = logPrivate;
            _logGlobal = logGlobal;
            _logDiagnostic = logDiagnostic;
            _all = rescanAll;
            _needsRefresh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_id != Guid.Empty) {
                var identifier = AnalyzerStatusUpdater.GetIdentifier(_id, _version);
                _updater = new AnalyzerStatusUpdater(identifier);
                // Immediately inform any listeners that we've started running
                // successfully.
                _updater.UpdateStatus(AnalysisStatus.Preparing, 0, 0);
            }
            // TODO: Link cancellation into the updater
            _cancel = CancellationToken.None;
        }

        public void LogToGlobal(string message) {
            if (!string.IsNullOrEmpty(_logGlobal)) {
                for (int retries = 10; retries > 0; --retries) {
                    try {
                        File.AppendAllText(_logGlobal, string.Format("{0:s} {1} {2}{3}", DateTime.Now, message, Environment.CommandLine, Environment.NewLine));
                        break;
                    } catch (DirectoryNotFoundException) {
                        // Create the directory and try again
                        Directory.CreateDirectory(Path.GetDirectoryName(_logGlobal));
                    } catch (IOException) {
                        // racing with someone else generating?
                        Thread.Sleep(25);
                    }
                }
            }
        }

        public void Dispose() {
            if (_updater != null) {
                _updater.Dispose();
            }
        }

        private static PyLibAnalyzer MakeFromArguments(IEnumerable<string> args) {
            var options = ParseArguments(args).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.InvariantCultureIgnoreCase);

            string value;

            Guid id;
            Version version;
            string interpreter, library, outDir;
            List<string> baseDb;
            string logPrivate, logGlobal, logDiagnostic;
            bool rescanAll;

            if (!options.TryGetValue("id", out value)) {
                id = Guid.Empty;
            } else if (!Guid.TryParse(value, out id)) {
                throw new ArgumentException(value, "id");
            }

            if (!options.TryGetValue("version", out value) && !options.TryGetValue("v", out value)) {
                throw new ArgumentNullException("version");
            } else if (!Version.TryParse(value, out version)) {
                throw new ArgumentException(value, "version");
            }

            if (!options.TryGetValue("python", out value) && !options.TryGetValue("py", out value)) {
                value = null;
            }
            if (!string.IsNullOrEmpty(value) && value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                throw new ArgumentException(value, "python");
            }
            interpreter = value;

            if (!options.TryGetValue("library", out value) && !options.TryGetValue("lib", out value)) {
                throw new ArgumentNullException("library");
            }
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                throw new ArgumentException(value, "library");
            }
            library = value;

            if (!options.TryGetValue("outdir", out value)) {
                value = Environment.CurrentDirectory;
            }
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                throw new ArgumentException(value, "outdir");
            }
            outDir = value;

            if (!options.TryGetValue("basedb", out value)) {
                value = Environment.CurrentDirectory;
            }
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                throw new ArgumentException(value, "basedb");
            }
            baseDb = value.Split(';').ToList();

            // Private log defaults to in current directory
            if (!options.TryGetValue("log", out value)) {
                value = Path.Combine(Environment.CurrentDirectory, "Analysislog.txt");
            }
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                throw new ArgumentException(value, "log");
            }
            if (!Path.IsPathRooted(value)) {
                value = Path.Combine(outDir, value);
            }
            logPrivate = value;

            // Global log defaults to null - we don't write start/stop events.
            if (!options.TryGetValue("glog", out value)) {
                value = null;
            }
            if (!string.IsNullOrEmpty(value) && value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                throw new ArgumentException(value, "glog");
            }
            if (!string.IsNullOrEmpty(value) && !Path.IsPathRooted(value)) {
                value = Path.Combine(outDir, value);
            }
            logGlobal = value;

            // Diagnostic log defaults to registry setting or else we don't use it.
            if (!options.TryGetValue("diag", out value)) {
                using (var key = Registry.CurrentUser.OpenSubKey(AnalysisLimitsKey)) {
                    if (key != null) {
                        value = key.GetValue("LogPath") as string;
                    }
                }
            }
            if (!string.IsNullOrEmpty(value) && value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                throw new ArgumentException(value, "diag");
            }
            if (!string.IsNullOrEmpty(value) && !Path.IsPathRooted(value)) {
                value = Path.Combine(outDir, value);
            }
            logDiagnostic = value;

            rescanAll = options.ContainsKey("all");

            return new PyLibAnalyzer(
                id,
                version,
                interpreter,
                library,
                baseDb,
                outDir,
                logPrivate,
                logGlobal,
                logDiagnostic,
                rescanAll);
        }

        private void StartTraceListener() {
            if (string.IsNullOrEmpty(_logPrivate) || _logPrivate.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_logPrivate));
            _listener = new TextWriterTraceListenerWithDateTime(new FileStream(_logPrivate, FileMode.Create, FileAccess.Write, FileShare.Read));
            Trace.Listeners.Add(_listener);
            Trace.AutoFlush = true;
        }

        private void Prepare() {
            if (_updater != null) {
                _updater.UpdateStatus(AnalysisStatus.Preparing, 0, 0);
            }

            // Concat the contents of directories referenced by .pth files
            // to ensure that they are overruled by normal packages in
            // naming collisions.
            var allModuleNames = new HashSet<string>(StringComparer.Ordinal);

            _fileGroups = ModulePath.GetModulesInLib(_library, allModuleNames)
                .GroupBy(mp => mp.LibraryPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.ToList())
                .ToList();

            _existingDatabase = new HashSet<string>(Directory.EnumerateFiles(_outDir, "*.idb"), StringComparer.OrdinalIgnoreCase);
        }

        private string PythonScraperPath {
            get {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var file = Path.Combine(dir, "PythonScraper.py");
                return file;
            }
        }

        private string ExtensionScraperPath {
            get {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var file = Path.Combine(dir, "ExtensionScraper.py");
                return file;
            }
        }

        private bool ShouldAnalyze(ModulePath path, string destPath = null) {
            if (_all) {
                return true;
            }
            if (_needsRefresh.Contains(path.LibraryPath)) {
                return true;
            }
            if (destPath == null) {
                destPath = Path.Combine(_outDir, path.ModuleName + ".idb");
            }
            
            if (!File.Exists(destPath) ||
                File.GetLastWriteTimeUtc(destPath) < File.GetLastWriteTimeUtc(path.SourceFile)) {
                if (path.LibraryPath.Equals(_library, StringComparison.OrdinalIgnoreCase)) {
                    _all = true;
                } else {
                    _needsRefresh.Add(path.LibraryPath);
                }
                return true;
            }
            return false;
        }

        private void Scrape() {
            if (!Directory.Exists(_outDir)) {
                Directory.CreateDirectory(_outDir);
            }
            if (string.IsNullOrEmpty(_interpreter)) {
                return;
            }

            if (_updater != null) {
                _updater.UpdateStatus(AnalysisStatus.Scraping, 0, 0);
            }

            // Ignoring case because these will become file paths, even though
            // they are case-sensitive module names.
            var builtinNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            builtinNames.Add(_version.Major == 3 ? BuiltinName3x : BuiltinName2x);
            using (var output = ProcessOutput.RunHiddenAndCapture(_interpreter, "-c", "import sys; print('\\n'.join(sys.builtin_module_names))")) {
                output.Wait();
                if (output.ExitCode != 0) {
                    // Don't delete anything if we don't get the right names.
                    _existingDatabase.Clear();
                    Trace.TraceInformation("SCRAPE BEGIN: {0}", output.Arguments);
                    if (output.StandardErrorLines.Any()) {
                        Trace.TraceWarning("SCRAPE ERRORS: {0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                    }
                } else {
                    builtinNames = new HashSet<string>(output.StandardOutputLines);
                }
            }
            var builtinModulePaths = builtinNames
                .Where(n => !string.IsNullOrEmpty(n) && n.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                .Select(n => Path.Combine(_outDir, n + ".idb"))
                .ToArray();

            if (_all || builtinModulePaths.Any(p => !File.Exists(p)) || !File.Exists(Path.Combine(_outDir, "database.ver"))) {
                _all = true;
                // Scape builtin Python types
                using (var output = ProcessOutput.RunHiddenAndCapture(_interpreter, PythonScraperPath, _outDir, _baseDb.First())) {
                    Trace.TraceInformation("SCRAPE BEGIN: {0}", output.Arguments);
                    output.Wait();

                    if (output.StandardOutputLines.Any()) {
                        Trace.TraceInformation("SCRAPE OUTPUT: {0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardOutputLines));
                    }
                    if (output.StandardErrorLines.Any()) {
                        Trace.TraceWarning("SCRAPE ERRORS: {0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                    }

                    if (output.ExitCode != 0) {
                        if (output.ExitCode.HasValue) {
                            Trace.TraceError("SCRAPE FAIL: ({0})", output.ExitCode);
                        } else {
                            Trace.TraceError("SCRAPE FAIL: (~)");
                        }
                        return;
                    } else {
                        Trace.TraceInformation("SCRAPE COMPLETE: 0");
                    }
                }
            }
            _existingDatabase.ExceptWith(builtinModulePaths);

            var scrapeFiles = _fileGroups.SelectMany(l => l)
                .Where(mp => mp.IsCompiled)
                .ToList();

            foreach (var file in scrapeFiles) {
                var destFile = Path.Combine(_outDir, file.ModuleName);
                _existingDatabase.Remove(destFile + ".idb");
                if (ShouldAnalyze(file)) {
                    _needsRefresh.Add(file.LibraryPath);
                    using (var output = ProcessOutput.RunHiddenAndCapture(_interpreter, ExtensionScraperPath, "scrape", file.ModuleName, "-", destFile)) {
                        Trace.TraceInformation("SCRAPE BEGIN: {0}", output.Arguments);
                        output.Wait();

                        if (output.StandardOutputLines.Any()) {
                            Trace.TraceInformation("SCRAPE OUTPUT: {0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardOutputLines));
                        }
                        if (output.StandardErrorLines.Any()) {
                            Trace.TraceWarning("SCRAPE ERRORS: {0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                        }

                        if (output.ExitCode != 0) {
                            if (output.ExitCode.HasValue) {
                                Trace.TraceError("SCRAPE FAIL: ({0}) {1}", output.ExitCode, file.ModuleName);
                            } else {
                                Trace.TraceError("SCRAPE FAIL: (~) {0}", file.ModuleName);
                            }
                        } else {
                            Trace.TraceInformation("SCRAPE COMPLETE: (0) {0}", file.ModuleName);
                        }
                    }
                }
            }
        }

        private void Analyze() {
            if (_updater != null) {
                _updater.UpdateStatus(AnalysisStatus.Analyzing, 0, 1);
            }

            int progressOffset = 0;
            int progressTotal = 0;
            foreach (var files in _fileGroups) {
                progressTotal += files.Count;
            }

            if (!string.IsNullOrEmpty(_logDiagnostic) && AnalysisLog.Output == null) {
                try {
                    AnalysisLog.Output = new StreamWriter(new FileStream(_logDiagnostic, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
                    AnalysisLog.AsCSV = _logDiagnostic.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase);
                } catch (Exception ex) {
                    Trace.TraceWarning("Failed to open {0} for logging: {1}", _logDiagnostic, string.Join("--", ex.ToString().Split('\r', '\n').Where(s => !string.IsNullOrWhiteSpace(s))));
                }
            }

            var factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(
                _version,
                null,
                _baseDb.Skip(1).Concat(Enumerable.Repeat(_outDir, 1)).ToArray()
            );

            foreach (var fileGroup in _fileGroups) {
                if (_cancel.IsCancellationRequested) {
                    break;
                }

                var files = fileGroup.Where(file => !file.IsCompiled).ToList();

                bool needAnalyze = false;
                foreach (var file in files) {
                    var destName = Path.Combine(_outDir, file.ModuleName + ".idb");
                    _existingDatabase.Remove(destName);
                    
                    // Deliberately short-circuit the check once we know we'll
                    // need to analyze this group. We don't break because we
                    // still have to remove the files from _existingDatabase.
                    needAnalyze = needAnalyze || ShouldAnalyze(file, destName);
                }

                if (!needAnalyze) {
                    Trace.TraceInformation("GROUP SKIPPED \"{0}\"", files[0].LibraryPath);
                    progressOffset += files.Count;
                    if (_updater != null) {
                        _updater.UpdateStatus(AnalysisStatus.Analyzing, progressOffset, progressTotal);
                    }
                    continue;
                }

                Trace.TraceInformation("GROUP START \"{0}\"", files[0].LibraryPath);
                AnalysisLog.StartFileGroup(files[0].LibraryPath, files.Count);
                Console.WriteLine("Now analyzing: {0}", files[0].LibraryPath);

                var projectState = new PythonAnalyzer(factory);

                int mostItemsInQueue = 0;
                if (_updater != null) {
                    projectState.SetQueueReporting(itemsInQueue => {
                        if (itemsInQueue > mostItemsInQueue) {
                            mostItemsInQueue = itemsInQueue;
                        }

                        if (mostItemsInQueue > 0) {
                            _updater.UpdateStatus(AnalysisStatus.Analyzing, progressOffset + (files.Count * (mostItemsInQueue - itemsInQueue)) / mostItemsInQueue, progressTotal);
                        } else {
                            _updater.UpdateStatus(AnalysisStatus.Analyzing, 0, 0);
                        }
                    }, 10);
                }

                using (var key = Registry.CurrentUser.OpenSubKey(AnalysisLimitsKey)) {
                    if (key != null) {
                        projectState.Limits = AnalysisLimits.LoadFromStorage(key, defaultToStdLib: true);
                    }
                }

                var modules = new List<IPythonProjectEntry>();
                for (int i = 0; i < files.Count; i++) {
                    modules.Add(projectState.AddModule(files[i].ModuleName, files[i].SourceFile));
                }

                var nodes = new List<PythonAst>();
                for (int i = 0; i < modules.Count && !_cancel.IsCancellationRequested; i++) {
                    PythonAst ast = null;
                    try {
                        var sourceUnit = new FileStream(files[i].SourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);

                        Trace.TraceInformation("PARSE START: \"{0}\" (\"{1}\")", modules[i].ModuleName, modules[i].FilePath);
                        ast = Parser.CreateParser(sourceUnit, _version.ToLanguageVersion(), new ParserOptions() { BindReferences = true }).ParseFile();
                        Trace.TraceInformation("PARSE END: \"{0}\" (\"{1}\")", modules[i].ModuleName, modules[i].FilePath);
                    } catch (Exception ex) {
                        Trace.TraceError("PARSE ERROR: \"{0}\" \"{1}\"{2}{3}", modules[i].ModuleName, modules[i].FilePath, Environment.NewLine, ex.ToString());
                    }
                    nodes.Add(ast);
                }

                for (int i = 0; i < modules.Count && !_cancel.IsCancellationRequested; i++) {
                    var ast = nodes[i];

                    if (ast != null) {
                        modules[i].UpdateTree(ast, null);
                    }
                }

                for (int i = 0; i < modules.Count && !_cancel.IsCancellationRequested; i++) {
                    var ast = nodes[i];
                    if (ast != null) {
                        Trace.TraceInformation("ANALYSIS START: \"{0}\"", modules[i].FilePath);
                        modules[i].Analyze(_cancel, true);
                        Trace.TraceInformation("ANALYSIS END: \"{0}\"", modules[i].FilePath);
                    }
                }

                if (modules.Count > 0 && !_cancel.IsCancellationRequested) {
                    modules[0].AnalysisGroup.AnalyzeQueuedEntries(_cancel);
                }

                if (_cancel.IsCancellationRequested) {
                    break;
                }

                Trace.TraceInformation("SAVING GROUP: \"{0}\"", files[0].LibraryPath);
                new SaveAnalysis().Save(projectState, _outDir);
                Trace.TraceInformation("GROUP END \"{0}\"", files[0].LibraryPath);
                AnalysisLog.EndFileGroup();

                progressOffset += files.Count;
                AnalysisLog.Flush();
            }
        }

        private void Epilogue() {
            File.WriteAllText(Path.Combine(_outDir, "database.ver"), PythonTypeDatabase.CurrentVersion.ToString());
        }

        private void Clean() {
            foreach (var file in _existingDatabase) {
                try {
                    Trace.TraceInformation("DELETING FILE: \"{0}\"", file);
                    File.Delete(file);
                    File.Delete(file + ".$memlist");
                } catch {
                }
            }
        }
    }

    class TextWriterTraceListenerWithDateTime : TextWriterTraceListener {
        public TextWriterTraceListenerWithDateTime(Stream stream) : base(stream) {
#if DEBUG
            this.Filter = new EventTypeFilter(SourceLevels.Information);
#else
            this.Filter = new EventTypeFilter(SourceLevels.Warning);
#endif
        }

        public override void WriteLine(string message) {
            Writer.WriteLine(DateTime.Now.ToString("s") + ": " + message);
        }
    }
}