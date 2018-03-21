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

            _databasePath = CreationOptions.DatabasePath;
            _useDefaultDatabase = useDefaultDatabase;
            if (_useDefaultDatabase) {
                if (InstallPath.TryGetFile($"DefaultDB\\v{Configuration.Version.Major}\\python.pyi", out string biPath)) {
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
            if (p.StartsWithOrdinal("\\")) {
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
    }
}
