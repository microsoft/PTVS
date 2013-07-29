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
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Analysis {
    internal class PyLibAnalyzer : IDisposable {
        private const string AnalysisLimitsKey = "Software\\Microsoft\\VisualStudio\\" +
            AssemblyVersionInfo.VSVersion +
            "\\PythonTools\\Analysis\\StandardLibrary";

        private readonly Guid _id;
        private readonly Version _version;
        private readonly string _interpreter;
        private readonly string _library;
        private readonly HashSet<string> _builtinSourceLibraries;
        private readonly string _outDir;
        private readonly List<string> _baseDb;
        private readonly string _logPrivate, _logGlobal, _logDiagnostic;

        private bool _all;
        private readonly HashSet<string> _needsRefresh;

        private readonly AnalyzerStatusUpdater _updater;
        private CancellationToken _cancel;
        private TextWriter _listener;
        private List<List<ModulePath>> _fileGroups;
        private HashSet<string> _existingDatabase;

        private const string BuiltinName2x = "__builtin__.idb";
        private const string BuiltinName3x = "builtins.idb";

        private static void Help() {
            Console.WriteLine("Python Library Analyzer {0} ({1})",
                AssemblyVersionInfo.StableVersion,
                AssemblyVersionInfo.Version);
            Console.WriteLine("Generates a cached analysis database for a Python interpreter.");
            Console.WriteLine();
            Console.WriteLine(" /id         [GUID]             - specify GUID of the interpreter being used");
            Console.WriteLine(" /v[ersion]  [version]          - specify language version to be used (x.y format)");
            Console.WriteLine(" /py[thon]   [filename]         - full path to the Python interpreter to use");
            Console.WriteLine(" /lib[rary]  [directory]        - full path to the Python library to analyze");
            Console.WriteLine(" /outdir     [output dir]       - specify output directory for analysis (default " +
                              "is current dir)");
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
            } catch (IdentifierInUseException) {
                Console.Error.WriteLine("This interpreter is already being analyzed.");
                return -3;
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
                        inst.TraceError("Analysis failed{0}{1}", Environment.NewLine, e.ToString());
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
            _builtinSourceLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _builtinSourceLibraries.Add(_library);

            if (_id != Guid.Empty) {
                var identifier = AnalyzerStatusUpdater.GetIdentifier(_id, _version);
                _updater = new AnalyzerStatusUpdater(identifier);
                // We worry about initialization exceptions here, specifically
                // that our identifier may already be in use.
                _updater.WaitForWorkerStarted();
                _updater.ThrowPendingExceptions();
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
                        File.AppendAllText(_logGlobal,
                            string.Format("{0:s} {1} {2}{3}",
                                DateTime.Now,
                                message,
                                Environment.CommandLine,
                                Environment.NewLine
                            )
                        );
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
            if (_listener != null) {
                _listener.Flush();
                _listener.Close();
                _listener = null;
            }
        }

        private static PyLibAnalyzer MakeFromArguments(IEnumerable<string> args) {
            var options = ParseArguments(args)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.InvariantCultureIgnoreCase);

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

        internal void StartTraceListener() {
            if (string.IsNullOrEmpty(_logPrivate) || _logPrivate.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_logPrivate));
            _listener = new StreamWriter(
                new FileStream(_logPrivate, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                Encoding.UTF8);
            _listener.WriteLine();
            TraceInformation("Start analysis");
        }

        internal void Prepare() {
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

            if (!string.IsNullOrEmpty(_interpreter)) {
                var dllPath = Path.Combine(Path.GetDirectoryName(_interpreter), "DLLs");
                if (Directory.Exists(dllPath)) {
                    _builtinSourceLibraries.Add(dllPath);
                    _fileGroups.Add(ModulePath.GetModulesInPath(dllPath, recurse: false).ToList());
                }
            }

            Directory.CreateDirectory(_outDir);

            _existingDatabase = new HashSet<string>(
                Directory.EnumerateFiles(_outDir, "*.idb", SearchOption.AllDirectories),
                StringComparer.OrdinalIgnoreCase);
        }

        internal string PythonScraperPath {
            get {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var file = Path.Combine(dir, "PythonScraper.py");
                return file;
            }
        }

        internal string ExtensionScraperPath {
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
            destPath = destPath ?? GetOutputFile(path);

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

        internal void Scrape() {
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
            using (var output = ProcessOutput.RunHiddenAndCapture(_interpreter,
                "-c",
                "import sys; print('\\n'.join(sys.builtin_module_names))")) {
                output.Wait();
                if (output.ExitCode != 0) {
                    // Don't delete anything if we don't get the right names.
                    _existingDatabase.Clear();
                    TraceInformation("Getting builtin names");
                    TraceInformation("Command {0}", output.Arguments);
                    if (output.StandardErrorLines.Any()) {
                        TraceError("Errors{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                    }
                } else {
                    builtinNames = new HashSet<string>(output.StandardOutputLines);
                }
            }
            TraceVerbose("Builtin names are: {0}", string.Join(", ", builtinNames.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)));

            var builtinModulePaths = builtinNames
                .Where(n => !string.IsNullOrEmpty(n) && n.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                .Select(n => GetOutputFile(n))
                .ToArray();

            if (_all || builtinModulePaths.Any(p => !File.Exists(p)) || !File.Exists(Path.Combine(_outDir, "database.ver"))) {
                _all = true;
                // Scape builtin Python types
                using (var output = ProcessOutput.RunHiddenAndCapture(_interpreter, PythonScraperPath, _outDir, _baseDb.First())) {
                    TraceInformation("Scraping builtin modules");
                    TraceInformation("Command: {0}", output.Arguments);
                    output.Wait();

                    if (output.StandardOutputLines.Any()) {
                        TraceInformation("Output{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardOutputLines));
                    }
                    if (output.StandardErrorLines.Any()) {
                        TraceWarning("Errors{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                    }

                    if (output.ExitCode != 0) {
                        if (output.ExitCode.HasValue) {
                            TraceError("Failed to scrape builtin modules (Exit Code: {0})", output.ExitCode);
                        } else {
                            TraceError("Failed to scrape builtin modules");
                        }
                        return;
                    } else {
                        TraceInformation("Scraped builtin modules");
                    }
                }
            }
            _existingDatabase.ExceptWith(builtinModulePaths);

            var scrapeFiles = _fileGroups.SelectMany(l => l)
                .Where(mp => mp.IsCompiled)
                .ToArray();

            foreach (var file in scrapeFiles) {
                var destFile = Path.ChangeExtension(GetOutputFile(file), null);
                _existingDatabase.Remove(destFile + ".idb");
                if (ShouldAnalyze(file)) {
                    _needsRefresh.Add(file.LibraryPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));

                    using (var output = ProcessOutput.RunHiddenAndCapture(_interpreter, ExtensionScraperPath, "scrape", file.ModuleName, "-", destFile)) {
                        TraceInformation("Scraping {0}", file.ModuleName);
                        TraceInformation("Command: {0}", output.Arguments);
                        output.Wait();

                        if (output.StandardOutputLines.Any()) {
                            TraceInformation("Output{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardOutputLines));
                        }
                        if (output.StandardErrorLines.Any()) {
                            TraceWarning("Errors{0}{1}", Environment.NewLine, string.Join(Environment.NewLine, output.StandardErrorLines));
                        }

                        if (output.ExitCode != 0) {
                            if (output.ExitCode.HasValue) {
                                TraceError("Failed to scrape {1} (Exit code: {0})", output.ExitCode, file.ModuleName);
                            } else {
                                TraceError("Failed to scrape {0}", file.ModuleName);
                            }
                        } else {
                            TraceVerbose("Scraped {0}", file.ModuleName);
                        }
                    }
                }
            }
            if (scrapeFiles.Any()) {
                TraceInformation("Scraped {0} files", scrapeFiles.Length);
            }
        }

        internal void Analyze() {
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
                    TraceWarning("Failed to open \"{0}\" for logging{1}{2}", _logDiagnostic, Environment.NewLine, ex.ToString());
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
                if (files.Count == 0) {
                    continue;
                }

                bool needAnalyze = false;
                foreach (var file in files) {
                    var destName = GetOutputFile(file); 
                    _existingDatabase.Remove(destName);

                    // Deliberately short-circuit the check once we know we'll
                    // need to analyze this group. We don't break because we
                    // still have to remove the files from _existingDatabase.
                    needAnalyze = needAnalyze || ShouldAnalyze(file, destName);
                }

                if (!needAnalyze) {
                    TraceInformation("Skipped group \"{0}\"", files[0].LibraryPath);
                    progressOffset += files.Count;
                    if (_updater != null) {
                        _updater.UpdateStatus(AnalysisStatus.Analyzing, progressOffset, progressTotal);
                    }
                    continue;
                }

                TraceInformation("Start group \"{0}\" with {1} files", files[0].LibraryPath, files.Count);
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
                        var errors = new CollectingErrorSink();
                        var opts = new ParserOptions() { BindReferences = true, ErrorSink = errors };

                        TraceInformation("Parsing \"{0}\" (\"{1}\")", modules[i].ModuleName, modules[i].FilePath);
                        ast = Parser.CreateParser(sourceUnit, _version.ToLanguageVersion(), opts).ParseFile();
                        if (errors.Errors.Any() || errors.Warnings.Any()) {
                            TraceWarning("File \"{0}\" contained parse errors", modules[i].FilePath);
                            TraceInformation(string.Join(Environment.NewLine, errors.Errors.Concat(errors.Warnings)
                                .Select(er => string.Format("{0} {1}", er.Span, er.Message))));
                        }
                    } catch (Exception ex) {
                        TraceError("Error parsing \"{0}\" \"{1}\"{2}{3}", modules[i].ModuleName, modules[i].FilePath, Environment.NewLine, ex.ToString());
                    }
                    nodes.Add(ast);
                }
                TraceInformation("Parsing complete");

                for (int i = 0; i < modules.Count && !_cancel.IsCancellationRequested; i++) {
                    var ast = nodes[i];

                    if (ast != null) {
                        modules[i].UpdateTree(ast, null);
                    }
                }

                for (int i = 0; i < modules.Count && !_cancel.IsCancellationRequested; i++) {
                    try {
                        var ast = nodes[i];
                        if (ast != null) {
                            TraceInformation("Analyzing \"{0}\"", modules[i].ModuleName);
                            modules[i].Analyze(_cancel, true);
                            TraceVerbose("Analyzed \"{0}\"", modules[i].FilePath);
                        }
                    } catch (Exception ex) {
                        TraceError("Error analyzing \"{0}\" \"{1}\"{2}{3}", modules[i].ModuleName, modules[i].FilePath, Environment.NewLine, ex.ToString());
                    }
                }

                if (modules.Count > 0 && !_cancel.IsCancellationRequested) {
                    TraceInformation("Starting analysis of {0} modules", modules.Count);
                    modules[0].AnalysisGroup.AnalyzeQueuedEntries(_cancel);
                    TraceInformation("Analysis complete");
                }

                if (_cancel.IsCancellationRequested) {
                    break;
                }

                TraceInformation("Saving group \"{0}\"", files[0].LibraryPath);
                var outDir = GetOutputDir(files[0]);
                Directory.CreateDirectory(outDir);
                new SaveAnalysis().Save(projectState, outDir);
                TraceInformation("End of group \"{0}\"", files[0].LibraryPath);
                AnalysisLog.EndFileGroup();

                progressOffset += files.Count;
                AnalysisLog.Flush();
            }
        }

        internal void Epilogue() {
            File.WriteAllText(Path.Combine(_outDir, "database.ver"), PythonTypeDatabase.CurrentVersion.ToString());
        }

        internal void Clean() {
            TraceInformation("Deleting {0} files", _existingDatabase.Count);
            foreach (var file in _existingDatabase) {
                try {
                    TraceVerbose("Deleting \"{0}\"", file);
                    File.Delete(file);
                    File.Delete(file + ".$memlist");
                } catch {
                }
            }
        }

        private string GetOutputDir(ModulePath file) {
            if (_builtinSourceLibraries.Contains(file.LibraryPath)) {
                return _outDir;
            } else {
                return Path.Combine(_outDir,
                    Regex.Replace(CommonUtils.GetRelativeFilePath(_library, file.LibraryPath), @"[.\\/]", "_")
                );
            }
        }

        private string GetOutputFile(string builtinName) {
            return Path.Combine(_outDir, builtinName + ".idb");
        }
        
        private string GetOutputFile(ModulePath file) {
            return Path.Combine(GetOutputDir(file), file.ModuleName + ".idb");
        }

        internal void TraceInformation(string message, params object[] args) {
            _listener.WriteLine(DateTime.Now.ToString("s") + ": " + string.Format(message, args));
            _listener.Flush();
        }

        internal void TraceWarning(string message, params object[] args) {
            _listener.WriteLine(DateTime.Now.ToString("s") + ": [WARNING] " + string.Format(message, args));
            _listener.Flush();
        }

        internal void TraceError(string message, params object[] args) {
            _listener.WriteLine(DateTime.Now.ToString("s") + ": [ERROR] " + string.Format(message, args));
            _listener.Flush();
        }

        [Conditional("DEBUG")]
        internal void TraceVerbose(string message, params object[] args) {
            _listener.WriteLine(DateTime.Now.ToString("s") + ": [VERBOSE] " + string.Format(message, args));
            _listener.Flush();
        }
    }
}