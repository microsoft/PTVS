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
            if (string.IsNullOrEmpty(fullPath)) {
                return fullPath;
            }
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
            if (IsWindows()) {
                dir = dir.ToLowerInvariant();
            }

            var dirHash = Convert.ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(dir)))
                .Replace('/', '_').Replace('+', '-');

            return Path.ChangeExtension(Path.Combine(
                _databasePath,
                Path.Combine(dirHash, name)
            ), ".pyi");
        }

        private static bool IsWindows() {
#if DESKTOP
            return true;
#else
            return System.Runtime.InteropServices.RuntimeInformation
                .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif
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

        internal async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(CancellationToken cancellationToken) {
            var spp = _searchPathPackages;
            if (spp != null) {
                return spp;
            }

            var sp = await GetSearchPathsAsync(cancellationToken).ConfigureAwait(false);
            if (sp == null) {
                return null;
            }

            var requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(Configuration.Version);
            var packageDict = await GetImportableModulesAsync(sp, requireInitPy, cancellationToken).ConfigureAwait(false);
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

        internal static async Task<IReadOnlyDictionary<string, string>> GetImportableModulesAsync(IEnumerable<string> searchPaths, bool requireInitPy, CancellationToken cancellationToken) {
            var packageDict = new Dictionary<string, string>();

            foreach (var searchPath in searchPaths.MaybeEnumerate()) {
                IReadOnlyCollection<string> packages = null;
                if (File.Exists(searchPath)) {
                    packages = GetPackagesFromZipFile(searchPath, cancellationToken);
                } else if (Directory.Exists(searchPath)) {
                    packages = await Task.Run(() => GetPackagesFromDirectory(searchPath, requireInitPy, cancellationToken)).ConfigureAwait(false);
                }
                foreach (var package in packages.MaybeEnumerate()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    packageDict[package] = searchPath;
                }
            }

            return packageDict;
        }


        private static IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath, bool requireInitPy, CancellationToken cancellationToken) {
            return ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true,
                requireInitPy: requireInitPy
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n)).TakeWhile(_ => !cancellationToken.IsCancellationRequested).ToList();
        }

        private static IReadOnlyCollection<string> GetPackagesFromZipFile(string searchPath, CancellationToken cancellationToken) {
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

        protected virtual async Task<IReadOnlyList<string>> GetCurrentSearchPathsAsync(CancellationToken cancellationToken) {
            if (Configuration.SearchPaths.Any()) {
                return Configuration.SearchPaths;
            }

            if (!File.Exists(Configuration.InterpreterPath)) {
                return Array.Empty<string>();
            }

            Log(TraceLevel.Info, "GetCurrentSearchPaths", Configuration.InterpreterPath, _searchPathCachePath);
            try {
                var paths = await PythonLibraryPath.GetDatabaseSearchPathsAsync(Configuration, _searchPathCachePath).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return paths.MaybeEnumerate().Select(p => p.Path).ToArray();
            } catch (InvalidOperationException) {
                return Array.Empty<string>();
            }
        }

        public async Task<IReadOnlyList<string>> GetSearchPathsAsync(CancellationToken cancellationToken) {
            var sp = _searchPaths;
            if (sp == null) {
                sp = await GetCurrentSearchPathsAsync(cancellationToken).ConfigureAwait(false);
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

        private async Task<ModulePath> FindModuleAsync(string filePath, CancellationToken cancellationToken) {
            var sp = await GetSearchPathsAsync(cancellationToken);

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

        internal static async Task<ModulePath> FindModuleAsync(IPythonInterpreterFactory factory, string filePath, CancellationToken cancellationToken) {
            try {
                var apif = factory as AstPythonInterpreterFactory;
                if (apif != null) {
                    return await apif.FindModuleAsync(filePath, cancellationToken);
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

        /// <summary>
        /// Determines whether the specified directory is an importable package.
        /// </summary>
        public bool IsPackage(string directory) {
            return ModulePath.PythonVersionRequiresInitPyFiles(Configuration.Version) ?
                !string.IsNullOrEmpty(ModulePath.GetPackageInitPy(directory)) :
                Directory.Exists(directory);
        }

        public enum TryImportModuleResultCode {
            Success,
            ModuleNotFound,
            NeedRetry,
            NotSupported,
            Timeout
        }

        public struct TryImportModuleResult {
            public readonly TryImportModuleResultCode Status;
            public readonly IPythonModule Module;

            public TryImportModuleResult(IPythonModule module) {
                Status = module == null ? TryImportModuleResultCode.ModuleNotFound : TryImportModuleResultCode.Success;
                Module = module;
            }

            public TryImportModuleResult(TryImportModuleResultCode status) {
                Status = status;
                Module = null;
            }

            public static TryImportModuleResult ModuleNotFound => new TryImportModuleResult(TryImportModuleResultCode.ModuleNotFound);
            public static TryImportModuleResult NeedRetry => new TryImportModuleResult(TryImportModuleResultCode.NeedRetry);
            public static TryImportModuleResult NotSupported => new TryImportModuleResult(TryImportModuleResultCode.NotSupported);
            public static TryImportModuleResult Timeout => new TryImportModuleResult(TryImportModuleResultCode.Timeout);
        }

        public sealed class TryImportModuleContext {
            public IPythonInterpreter Interpreter { get; set; }
            public ConcurrentDictionary<string, IPythonModule> ModuleCache { get; set; }
            public IPythonModule BuiltinModule { get; set; }
            public Func<string, CancellationToken, Task<ModulePath?>> FindModuleInUserSearchPathAsync { get; set; }
            public IReadOnlyList<string> TypeStubPaths { get; set; }
            public bool MergeTypeStubPackages { get; set; }
        }

        public async Task<TryImportModuleResult> TryImportModuleAsync(
            string name,
            TryImportModuleContext context,
            CancellationToken cancellationToken
        ) {
            IPythonModule module = null;
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
            SentinelModule sentinalValue = null;

            if (modules != null) {
                // Return any existing module
                if (modules.TryGetValue(name, out module) && module != null) {
                    if (module is SentinelModule smod) {
                        // If we are importing this module on another thread, allow
                        // time for it to complete. This does not block if we are
                        // importing on the current thread or the module is not
                        // really being imported.
                        try {
                            module = await smod.WaitForImportAsync(cancellationToken);
                        } catch (OperationCanceledException) {
                            _log?.Log(TraceLevel.Warning, "ImportTimeout", name);
                            return TryImportModuleResult.Timeout;
                        }

                        if (module is SentinelModule) {
                            _log?.Log(TraceLevel.Warning, "RecursiveImport", name);
                        }
                    }
                    return new TryImportModuleResult(module);
                }

                // Set up a sentinel so we can detect recursive imports
                sentinalValue = new SentinelModule(name, true);
                if (!modules.TryAdd(name, sentinalValue)) {
                    // Try to get the new module, in case we raced with a .Clear()
                    if (modules.TryGetValue(name, out module) && !(module is SentinelModule)) {
                        return new TryImportModuleResult(module);
                    }
                    // If we reach here, the race is too complicated to recover
                    // from. Signal the caller to try importing again.
                    _log?.Log(TraceLevel.Warning, "RetryImport", name);
                    return TryImportModuleResult.NeedRetry;
                }
            }

            // Do normal searches
            if (!string.IsNullOrEmpty(Configuration?.InterpreterPath)) {
                try {
                    module = await ImportFromSearchPathsAsync(name, context, cancellationToken);
                } catch (OperationCanceledException) {
                    _log?.Log(TraceLevel.Error, "ImportTimeout", name, "ImportFromSearchPaths");
                    return TryImportModuleResult.Timeout;
                }

                if (module == null) {
                    module = ImportFromBuiltins(name, context.BuiltinModule as AstBuiltinsPythonModule);
                }
            }
            if (module == null) {
                module = ImportFromCache(name, context);
            }

            // Also search for type stub packages if enabled and we are not a blacklisted module
            if (module != null && context?.TypeStubPaths != null && module.Name != "typing") {
                var tsModule = await ImportFromTypeStubsAsync(module.Name, context, cancellationToken);
                if (tsModule != null) {
                    if (context.MergeTypeStubPackages) {
                        module = AstPythonMultipleMembers.CombineAs<IPythonModule>(module, tsModule);
                    } else {
                        module = tsModule;
                    }
                }
            }

            if (modules != null) {
                if (sentinalValue == null) {
                    _log?.Log(TraceLevel.Error, "RetryImport", name, "sentinalValue==null");
                    Debug.Fail("Sentinal module was never created");
                    return TryImportModuleResult.NeedRetry;
                }

                // Replace our sentinel, or if we raced, get the current
                // value and abandon the one we just created.
                if (!modules.TryUpdate(name, module, sentinalValue)) {
                    // Try to get the new module, in case we raced
                    if (modules.TryGetValue(name, out module) && !(module is SentinelModule)) {
                        return new TryImportModuleResult(module);
                    }
                    // If we reach here, the race is too complicated to recover
                    // from. Signal the caller to try importing again.
                    _log?.Log(TraceLevel.Warning, "RetryImport", name);
                    return TryImportModuleResult.NeedRetry;
                }
                sentinalValue.Complete(module);
            }

            return new TryImportModuleResult(module);
        }

        private async Task<IPythonModule> ImportFromTypeStubsAsync(string name, TryImportModuleContext context, CancellationToken cancellationToken) {
            var mp = FindModuleInSearchPath(context.TypeStubPaths, null, name);

            if (mp == null) {
                int i = name.IndexOf('.');
                if (i == 0) {
                    Debug.Fail("Invalid module name");
                    return null;
                }
                var stubName = i < 0 ? (name + "-stubs") : (name.Remove(i)) + "-stubs" + name.Substring(i);
                ModulePath? stubMp = null;
                if (context.FindModuleInUserSearchPathAsync != null) {
                    try {
                        stubMp = await context.FindModuleInUserSearchPathAsync(stubName, cancellationToken);
                    } catch (Exception ex) {
                        _log?.Log(TraceLevel.Error, "Exception", ex.ToString());
                        _log?.Flush();
                        return null;
                    }
                }
                if (stubMp == null) {
                    stubMp = await FindModuleInSearchPathAsync(stubName, cancellationToken);
                }

                if (stubMp != null) {
                    mp = new ModulePath(name, stubMp?.SourceFile, stubMp?.LibraryPath);
                }
            }

            if (mp == null && context.TypeStubPaths != null && context.TypeStubPaths.Count > 0) {
                mp = FindModuleInSearchPath(context.TypeStubPaths.SelectMany(GetTypeShedPaths).ToArray(), null, name);
            }

            if (mp == null) {
                return null;
            }

            if (mp.Value.IsCompiled) {
                Debug.Fail("Unsupported native module in typeshed");
                return null;
            }

            _log?.Log(TraceLevel.Verbose, "ImportTypeStub", mp?.FullName, FastRelativePath(mp?.SourceFile));
            return PythonModuleLoader.FromTypeStub(context.Interpreter, mp?.SourceFile, LanguageVersion, mp?.FullName);
        }

        private IEnumerable<string> GetTypeShedPaths(string path) {
            var stdlib = Path.Combine(path, "stdlib");
            var thirdParty = Path.Combine(path, "third_party");

            var v = Configuration.Version;
            foreach (var subdir in new[] { v.ToString(), v.Major.ToString(), "2and3" }) {
                yield return Path.Combine(stdlib, subdir);
            }

            foreach (var subdir in new[] { v.ToString(), v.Major.ToString(), "2and3" }) {
                yield return Path.Combine(thirdParty, subdir);
            }
        }


        private IPythonModule ImportFromCache(string name, TryImportModuleContext context) {
            if (string.IsNullOrEmpty(CreationOptions.DatabasePath)) {
                return null;
            }

            var cache = GetCacheFilePath("python.{0}.pyi".FormatInvariant(name));
            if (!File.Exists(cache)) {
                cache = GetCacheFilePath("python._{0}.pyi".FormatInvariant(name));
                if (!File.Exists(cache)) {
                    cache = GetCacheFilePath("{0}.pyi".FormatInvariant(name));
                    if (!File.Exists(cache)) {
                        return null;
                    }
                }
            }

            return PythonModuleLoader.FromTypeStub(context.Interpreter, cache, LanguageVersion, name);
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

        protected async Task<ModulePath?> FindModuleInSearchPathAsync(string name, CancellationToken cancellationToken) {
            var searchPaths = await GetSearchPathsAsync(cancellationToken).ConfigureAwait(false);
            var packages = await GetImportableModulesAsync(cancellationToken).ConfigureAwait(false);
            return FindModuleInSearchPath(searchPaths, packages, name);
        }

        protected virtual ModulePath? FindModuleInSearchPath(IReadOnlyList<string> searchPaths, IReadOnlyDictionary<string, string> packages, string name) {
            if (searchPaths == null || searchPaths.Count == 0) {
                return null;
            }

            _log?.Log(TraceLevel.Verbose, "FindModule", name, "system", string.Join(", ", searchPaths));

            int i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);
            string searchPath;

            ModulePath mp;
            Func<string, bool> isPackage = IsPackage;
            if (firstBit.EndsWithOrdinal("-stubs", ignoreCase: true)) {
                isPackage = Directory.Exists;
            }

            var requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(Configuration.Version);
            if (packages != null && packages.TryGetValue(firstBit, out searchPath) && !string.IsNullOrEmpty(searchPath)) {
                if (ModulePath.FromBasePathAndName_NoThrow(searchPath, name, isPackage, null, requireInitPy, out mp, out _, out _, out _)) {
                    return mp;
                }
            }

            foreach (var sp in searchPaths.MaybeEnumerate()) {
                if (ModulePath.FromBasePathAndName_NoThrow(sp, name, isPackage, null, requireInitPy, out mp, out _, out _, out _)) {
                    return mp;
                }
            }

            return null;
        }

        private async Task<IPythonModule> ImportFromSearchPathsAsync(string name, TryImportModuleContext context, CancellationToken cancellationToken) {
            ModulePath? mmp = null;
            if (context.FindModuleInUserSearchPathAsync != null) {
                try {
                    mmp = await context.FindModuleInUserSearchPathAsync(name, cancellationToken);
                } catch (Exception ex) {
                    _log?.Log(TraceLevel.Error, "Exception", ex.ToString());
                    _log?.Flush();
                    return null;
                }
            }

            if (!mmp.HasValue) {
                mmp = await FindModuleInSearchPathAsync(name, cancellationToken);
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
