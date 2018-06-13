// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonInterpreterFactory : IPythonInterpreterFactory, IPythonInterpreterFactoryWithLog, ICustomInterpreterSerialization, IDisposable {
        private readonly string _databasePath, _searchPathCachePath;
        private readonly object _searchPathsLock = new object();
        private IReadOnlyList<string> _searchPaths;
        private IReadOnlyDictionary<string, string> _searchPathPackages;

        private bool _disposed, _loggedBadDbPath;
        private readonly bool _skipCache, _useDefaultDatabase;

        private AnalysisLogWriter _log;
        // Available for tests to override
        internal static bool LogToConsole = false;

#if DEBUG
        const int LogCacheSize = 1;
        const int LogRotationSize = 16384;
#else
        const int LogCacheSize = 20;
        const int LogRotationSize = 4096;
#endif

        public AstPythonInterpreterFactory(InterpreterConfiguration config, InterpreterFactoryCreationOptions options)
            : this(config, options, string.IsNullOrEmpty(options?.DatabasePath)) { }

        private AstPythonInterpreterFactory(
            InterpreterConfiguration config,
            InterpreterFactoryCreationOptions options,
            bool useDefaultDatabase
        ) {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));
            CreationOptions = options ?? new InterpreterFactoryCreationOptions();
            try {
                LanguageVersion = Configuration.Version.ToLanguageVersion();
            } catch (InvalidOperationException ex) {
                throw new ArgumentException(ex.Message, ex);
            }
            BuiltinModuleName = BuiltinTypeId.Unknown.GetModuleName(LanguageVersion);

            _databasePath = CreationOptions.DatabasePath;
            _useDefaultDatabase = useDefaultDatabase;
            if (_useDefaultDatabase) {
                var dbPath = Path.Combine("DefaultDB", $"v{Configuration.Version.Major}", "python.pyi");
                if (InstallPath.TryGetFile(dbPath, out string biPath)) {
                    CreationOptions.DatabasePath = _databasePath = Path.GetDirectoryName(biPath);
                } else {
                    _skipCache = true;
                }
            } else {
                _searchPathCachePath = Path.Combine(_databasePath, "database.path");

                _log = new AnalysisLogWriter(Path.Combine(_databasePath, "AnalysisLog.txt"), false, LogToConsole, LogCacheSize);
                _log.Rotate(LogRotationSize);
                _log.MinimumLevel = CreationOptions.TraceLevel;
            }
            _skipCache = !CreationOptions.UseExistingCache;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AstPythonInterpreterFactory() {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                _disposed = true;
                _log?.Flush(synchronous: true);

                if (disposing) {
                    if (_log != null) {
                        _log.Dispose();
                    }
                }
            }
        }

        bool ICustomInterpreterSerialization.GetSerializationInfo(out string assembly, out string typeName, out Dictionary<string, object> properties) {
            assembly = GetType().Assembly.Location;
            typeName = GetType().FullName;
            properties = CreationOptions.ToDictionary();
            Configuration.WriteToDictionary(properties);
            if (_useDefaultDatabase) {
                properties["UseDefaultDatabase"] = true;
            }
            return true;
        }

        internal AstPythonInterpreterFactory(Dictionary<string, object> properties) :
            this(
                InterpreterConfiguration.FromDictionary(properties),
                InterpreterFactoryCreationOptions.FromDictionary(properties),
                properties.ContainsKey("UseDefaultDatabase")
            ) { }

        public InterpreterConfiguration Configuration { get; }

        public InterpreterFactoryCreationOptions CreationOptions { get; }

        public PythonLanguageVersion LanguageVersion { get; }

        public string BuiltinModuleName { get; }

        public event EventHandler ImportableModulesChanged;

        public void NotifyImportNamesChanged() {
            lock (_searchPathsLock) {
                _searchPaths = null;
                _searchPathPackages = null;
            }


            if (File.Exists(_searchPathCachePath)) {
                PathUtils.DeleteFile(_searchPathCachePath);
            }

            ImportableModulesChanged?.Invoke(this, EventArgs.Empty);
        }

        public virtual IPythonInterpreter CreateInterpreter() => new AstPythonInterpreter(this, _log);

        internal void Log(TraceLevel level, string eventName, params object[] args) {
            _log?.Log(level, eventName, args);
        }

        internal string FastRelativePath(string fullPath) {
            if (!fullPath.StartsWithOrdinal(Configuration.PrefixPath, ignoreCase: true)) {
                return fullPath;
            }
            var p = fullPath.Substring(Configuration.PrefixPath.Length);
            if (p.Length > 0 && p[0] == Path.DirectorySeparatorChar) {
                return p.Substring(1);
            }
            return p;
        }


        internal string GetCacheFilePath(string filePath) {
            var dbPath = _databasePath;
            if (!PathEqualityComparer.IsValidPath(dbPath)) {
                if (!_loggedBadDbPath) {
                    _loggedBadDbPath = true;
                    _log?.Log(TraceLevel.Warning, "InvalidDatabasePath", dbPath);
                }
                return null;
            }

            var name = PathUtils.GetFileName(filePath);
            if (!PathEqualityComparer.IsValidPath(name)) {
                _log?.Log(TraceLevel.Warning, "InvalidCacheName", name);
                return null;
            }
            try {
                var candidate = Path.ChangeExtension(Path.Combine(dbPath, name), ".pyi");
                if (File.Exists(candidate)) {
                    return candidate;
                }
            } catch (ArgumentException) {
                return null;
            }

            var hash = SHA256.Create();
            var dir = Path.GetDirectoryName(filePath);
            var dirHash = Convert.ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(dir)))
                .Replace('/', '_').Replace('+', '-');

            return Path.ChangeExtension(Path.Combine(
                _databasePath,
                Path.Combine(dirHash, name)
            ), ".pyi");
        }

        #region Cache File Management

        internal Stream ReadCachedModule(string filePath) {
            if (_skipCache) {
                return null;
            }

            var path = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            var file = PathUtils.OpenWithRetry(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (file == null || _useDefaultDatabase) {
                return file;
            }

            bool fileIsOkay = false;
            try {
                var cacheTime = File.GetLastWriteTimeUtc(path);
                var sourceTime = File.GetLastWriteTimeUtc(filePath);
                if (sourceTime <= cacheTime) {
                    var assemblyTime = File.GetLastWriteTimeUtc(typeof(AstPythonInterpreterFactory).Assembly.Location);
                    if (assemblyTime <= cacheTime) {
                        fileIsOkay = true;
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
            }

            if (fileIsOkay) {
                return file;
            }

            file.Dispose();
            file = null;

            Log(TraceLevel.Info, "InvalidateCachedModule", path);

            PathUtils.DeleteFile(path);
            return null;
        }

        internal void WriteCachedModule(string filePath, Stream code) {
            if (_useDefaultDatabase) {
                return;
            }

            var cache = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(cache)) {
                return;
            }

            Log(TraceLevel.Info, "WriteCachedModule", cache);

            try {
                using (var stream = PathUtils.OpenWithRetry(cache, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                    if (stream == null) {
                        return;
                    }

                    code.CopyTo(stream);
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                try {
                    File.Delete(cache);
                } catch (Exception) {
                }
            }
        }

        #endregion

        internal async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync() {
            var spp = _searchPathPackages;
            if (spp != null) {
                return spp;
            }

            var sp = await GetSearchPathsAsync().ConfigureAwait(false);
            if (sp == null) {
                return null;
            }

            var packageDict = await GetImportableModulesAsync(sp).ConfigureAwait(false);
            if (!packageDict.Any()) {
                return null;
            }

            lock (_searchPathsLock) {
                if (_searchPathPackages != null) {
                    return _searchPathPackages;
                }

                _searchPathPackages = packageDict;
                return packageDict;
            }
        }

        internal static async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(IEnumerable<string> searchPaths) {
            var packageDict = new Dictionary<string, string>();

            foreach (var searchPath in searchPaths.MaybeEnumerate()) {
                IReadOnlyCollection<string> packages = null;
                if (File.Exists(searchPath)) {
                    packages = GetPackagesFromZipFile(searchPath);
                } else if (Directory.Exists(searchPath)) {
                    packages = await Task.Run(() => GetPackagesFromDirectory(searchPath)).ConfigureAwait(false);
                }
                foreach (var package in packages.MaybeEnumerate()) {
                    packageDict[package] = searchPath;
                }
            }

            return packageDict;
        }


        private static IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath) {
            return ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n)).ToList();
        }

        private static IReadOnlyCollection<string> GetPackagesFromZipFile(string searchPath) {
            // TODO: Search zip files for packages
            return new string[0];
        }


        /// <summary>
        /// For test use only
        /// </summary>
        internal void SetCurrentSearchPaths(IEnumerable<string> paths) {
            lock (_searchPathsLock) {
                _searchPaths = paths.ToArray();
                _searchPathPackages = null;
            }
            ImportableModulesChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual async Task<IReadOnlyList<string>> GetCurrentSearchPathsAsync() {
            if (Configuration.SearchPaths.Any()) {
                return Configuration.SearchPaths;
            }

            if (!File.Exists(Configuration.InterpreterPath)) {
                return Array.Empty<string>();
            }

            Log(TraceLevel.Info, "GetCurrentSearchPaths", Configuration.InterpreterPath, _searchPathCachePath);
            try {
                var paths = await PythonLibraryPath.GetDatabaseSearchPathsAsync(Configuration, _searchPathCachePath).ConfigureAwait(false);
                return paths.MaybeEnumerate().Select(p => p.Path).ToArray();
            } catch (InvalidOperationException) {
                return Array.Empty<string>();
            }
        }

        public async Task<IReadOnlyList<string>> GetSearchPathsAsync() {
            var sp = _searchPaths;
            if (sp == null) {
                sp = await GetCurrentSearchPathsAsync().ConfigureAwait(false);
                lock (_searchPathsLock) {
                    if (_searchPaths == null) {
                        _searchPaths = sp;
                    } else {
                        sp = _searchPaths;
                    }
                }
                Debug.Assert(sp != null, "Should have search paths");
                Log(TraceLevel.Info, "SearchPaths", sp.Cast<object>().ToArray());
            }
            return sp;
        }

        private async Task<ModulePath> FindModuleAsync(string filePath) {
            var sp = await GetSearchPathsAsync();

            string bestLibraryPath = "";

            foreach (var p in sp) {
                if (PathEqualityComparer.Instance.StartsWith(filePath, p)) {
                    if (p.Length > bestLibraryPath.Length) {
                        bestLibraryPath = p;
                    }
                }
            }

            var mp = ModulePath.FromFullPath(filePath, bestLibraryPath);
            return mp;
        }

        internal static async Task<ModulePath> FindModuleAsync(IPythonInterpreterFactory factory, string filePath) {
            try {
                var apif = factory as AstPythonInterpreterFactory;
                if (apif != null) {
                    return await apif.FindModuleAsync(filePath);
                }

                return ModulePath.FromFullPath(filePath);
            } catch (ArgumentException) {
                return default(ModulePath);
            }
        }

        public string GetAnalysisLogContent(IFormatProvider culture) {
            _log?.Flush(synchronous: true);
            var logfile = _log?.OutputFile;
            if (!File.Exists(logfile)) {
                return null;
            }

            try {
                return File.ReadAllText(logfile);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                return ex.ToString();
            }
        }

        #region Module Imports

        public enum TryImportModuleResult {
            Success,
            ModuleNotFound,
            NeedRetry,
            NotSupported,
            Timeout
        }

        public sealed class TryImportModuleContext {
            public IPythonInterpreter Interpreter { get; set; }
            public ConcurrentDictionary<string, IPythonModule> ModuleCache { get; set; }
            public int Timeout { get; set; } = 5000;
            public IPythonModule BuiltinModule { get; set; }
            public Func<string, Task<ModulePath?>> FindModuleInUserSearchPathAsync { get; set; }
            public IReadOnlyList<string> TypeStubPaths { get; set; }
            public bool MergeTypeStubPackages { get; set; }
        }

        public TryImportModuleResult TryImportModule(
            string name,
            out IPythonModule module,
            TryImportModuleContext context
        ) {
            module = null;
            if (string.IsNullOrEmpty(name)) {
                return TryImportModuleResult.ModuleNotFound;
            }

            Debug.Assert(!name.EndsWithOrdinal("."), $"{name} should not end with '.'");

            // Handle builtins explicitly
            if (name == BuiltinModuleName) {
                Debug.Fail($"Interpreters must handle import {name} explicitly");
                return TryImportModuleResult.NotSupported;
            }

            var modules = context?.ModuleCache;
            int importTimeout = context?.Timeout ?? 5000;
            SentinelModule sentinalValue = null;

            if (modules != null) {
                // Return any existing module
                if (modules.TryGetValue(name, out module) && module != null) {
                    if (module is SentinelModule smod) {
                        // If we are importing this module on another thread, allow
                        // time for it to complete. This does not block if we are
                        // importing on the current thread or the module is not
                        // really being imported.
                        var newMod = smod.WaitForImport(importTimeout);
                        if (newMod is SentinelModule) {
                            _log?.Log(TraceLevel.Warning, "RecursiveImport", name);
                            module = newMod;
                        } else if (newMod == null) {
                            _log?.Log(TraceLevel.Warning, "ImportTimeout", name);
                            module = null;
                            return TryImportModuleResult.Timeout;
                        } else {
                            module = newMod;
                        }
                    }
                    return TryImportModuleResult.Success;
                }

                // Set up a sentinel so we can detect recursive imports
                sentinalValue = new SentinelModule(name, true);
                if (!modules.TryAdd(name, sentinalValue)) {
                    // Try to get the new module, in case we raced with a .Clear()
                    if (modules.TryGetValue(name, out module) && !(module is SentinelModule)) {
                        return module == null ? TryImportModuleResult.ModuleNotFound : TryImportModuleResult.Success;
                    }
                    // If we reach here, the race is too complicated to recover
                    // from. Signal the caller to try importing again.
                    _log?.Log(TraceLevel.Warning, "RetryImport", name);
                    return TryImportModuleResult.NeedRetry;
                }
            }

            // Do normal searches
            if (!string.IsNullOrEmpty(Configuration?.InterpreterPath)) {
                var importTask = ImportFromSearchPathsAsync(name, context);
                if (importTask.Wait(importTimeout)) {
                    module = importTask.Result;
                } else {
                    _log?.Log(TraceLevel.Error, "ImportTimeout", name, "ImportFromSearchPaths");
                    module = null;
                }
                module = module ?? ImportFromBuiltins(name, context.BuiltinModule as AstBuiltinsPythonModule);
            }
            if (module == null) {
                module = ImportFromCache(name);
            }

            if (modules != null) {
                // Replace our sentinel, or if we raced, get the current
                // value and abandon the one we just created.
                if (!modules.TryUpdate(name, module, sentinalValue)) {
                    // Try to get the new module, in case we raced
                    if (modules.TryGetValue(name, out module) && !(module is SentinelModule)) {
                        return module == null ? TryImportModuleResult.ModuleNotFound : TryImportModuleResult.Success;
                    }
                    // If we reach here, the race is too complicated to recover
                    // from. Signal the caller to try importing again.
                    _log?.Log(TraceLevel.Warning, "RetryImport", name);
                    return TryImportModuleResult.NeedRetry;
                }
                sentinalValue.Complete(module);
                sentinalValue.Dispose();
            }

            // Also search for type stub packages if enabled and we are not a blacklisted module
            if (module != null && module.Name != "typing") {
                // Note that this currently only looks in the typeshed package, as type stub
                // packages are not yet standardised so we don't know where to look.
                // The details will be in PEP 561.
                if (context.TypeStubPaths?.Any() == true) {
                    var mtsp = FindModuleInSearchPath(context.TypeStubPaths, null, module.Name);
                    if (mtsp.HasValue) {
                        var mp = mtsp.Value;
                        if (mp.IsCompiled) {
                            Debug.Fail("Unsupported native module in typeshed");
                        } else {
                            _log?.Log(TraceLevel.Verbose, "ImportTypeShed", mp.FullName, FastRelativePath(mp.SourceFile));
                            var tsModule = PythonModuleLoader.FromFile(context.Interpreter, mp.SourceFile, LanguageVersion, mp.FullName);

                            if (tsModule != null) {
                                if (context.MergeTypeStubPackages) {
                                    module = AstPythonMultipleModules.Combine(module, tsModule);
                                } else {
                                    module = tsModule;
                                }
                            }
                        }
                    }
                }
            }

            return module == null ? TryImportModuleResult.ModuleNotFound : TryImportModuleResult.Success;
        }

        private IPythonModule ImportFromCache(string name) {
            if (string.IsNullOrEmpty(CreationOptions.DatabasePath)) {
                return null;
            }

            if (File.Exists(GetCacheFilePath("python.{0}.pyi".FormatInvariant(name)))) {
                return new AstCachedPythonModule(name, "python.{0}".FormatInvariant(name));
            }
            if (File.Exists(GetCacheFilePath("python._{0}.pyi".FormatInvariant(name)))) {
                return new AstCachedPythonModule(name, "python._{0}".FormatInvariant(name));
            }
            if (File.Exists(GetCacheFilePath("{0}.pyi".FormatInvariant(name)))) {
                return new AstCachedPythonModule(name, name);
            }

            return null;
        }

        private IPythonModule ImportFromBuiltins(string name, AstBuiltinsPythonModule builtinModule) {
            if (builtinModule == null) {
                return null;
            }
            var bmn = builtinModule.GetAnyMember("__builtin_module_names__") as AstPythonStringLiteral;
            var names = bmn?.Value ?? string.Empty;
            // Quick substring check
            if (!names.Contains(name)) {
                return null;
            }
            // Proper split/trim check
            if (!names.Split(',').Select(n => n.Trim()).Contains(name)) {
                return null;
            }

            _log?.Log(TraceLevel.Info, "ImportBuiltins", name, FastRelativePath(Configuration.InterpreterPath));

            try {
                return new AstBuiltinPythonModule(name, Configuration.InterpreterPath);
            } catch (ArgumentNullException) {
                Debug.Fail("No factory means cannot import builtin modules");
                return null;
            }
        }

        private async Task<IPythonModule> ImportFromSearchPathsAsync(string name, TryImportModuleContext context) {
            try {
                return await ImportFromSearchPathsAsyncWorker(name, context).ConfigureAwait(false);
            } catch (Exception ex) {
                _log?.Log(TraceLevel.Error, "ImportFromSearchPathsAsync", ex.ToString());
                throw;
            }
        }

        protected async Task<ModulePath?> FindModuleInSearchPathAsync(string name) {
            var searchPaths = await GetSearchPathsAsync().ConfigureAwait(false);
            var packages = await GetImportableModulesAsync().ConfigureAwait(false);
            return FindModuleInSearchPath(searchPaths, packages, name);
        }

        protected virtual ModulePath? FindModuleInSearchPath(IReadOnlyList<string> searchPaths, IReadOnlyDictionary<string, string> packages, string name) {
            _log?.Log(TraceLevel.Verbose, "FindModule", name, "system", string.Join(", ", searchPaths));

            if (searchPaths == null) {
                return null;
            }

            int i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);
            string searchPath;

            ModulePath mp;

            if (packages != null && packages.TryGetValue(firstBit, out searchPath) && !string.IsNullOrEmpty(searchPath)) {
                if (ModulePath.FromBasePathAndName_NoThrow(searchPath, name, out mp)) {
                    return mp;
                }
            }

            foreach (var sp in searchPaths.MaybeEnumerate()) {
                if (ModulePath.FromBasePathAndName_NoThrow(sp, name, out mp)) {
                    return mp;
                }
            }

            return null;
        }

        private async Task<IPythonModule> ImportFromSearchPathsAsyncWorker(string name, TryImportModuleContext context) {
            ModulePath? mmp = null;
            if (context.FindModuleInUserSearchPathAsync != null) {
                try {
                    mmp = await context.FindModuleInUserSearchPathAsync(name);
                } catch (Exception ex) {
                    _log?.Log(TraceLevel.Error, "Exception", ex.ToString());
                    _log?.Flush();
                    return null;
                }
            }

            if (!mmp.HasValue) {
                mmp = await FindModuleInSearchPathAsync(name);
            }

            if (!mmp.HasValue) {
                _log?.Log(TraceLevel.Verbose, "ImportNotFound", name);
                return null;
            }

            var mp = mmp.Value;

            IPythonModule module;

            if (mp.IsCompiled) {
                _log?.Log(TraceLevel.Verbose, "ImportScraped", mp.FullName, FastRelativePath(mp.SourceFile));
                module = new AstScrapedPythonModule(mp.FullName, mp.SourceFile);
            } else {
                _log?.Log(TraceLevel.Verbose, "Import", mp.FullName, FastRelativePath(mp.SourceFile));
                module = PythonModuleLoader.FromFile(context.Interpreter, mp.SourceFile, LanguageVersion, mp.FullName);
            }

            return module;
        }

        #endregion
    }
}
