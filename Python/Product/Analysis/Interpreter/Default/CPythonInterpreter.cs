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
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter.Ast;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreter : IPythonInterpreter {
        readonly Version _langVersion;
        private PythonInterpreterFactoryWithDatabase _factory;
        private PythonTypeDatabase _typeDb, _searchPathDb;
        private readonly object _searchPathDbLock = new object();
        private PythonAnalyzer _state;
        private IReadOnlyList<string> _searchPaths;
        private IReadOnlyDictionary<string, string> _searchPathPackages;
        private CancellationTokenSource _searchPathPackagesCancellation;
        private Dictionary<string, HashSet<string>> _zipPackageCache;

        public CPythonInterpreter(PythonInterpreterFactoryWithDatabase factory) {
            _langVersion = factory.Configuration.Version;
            _factory = factory;
            _typeDb = _factory.GetCurrentDatabase();
            _factory.NewDatabaseAvailable += OnNewDatabaseAvailable;
        }

        private async void OnNewDatabaseAvailable(object sender, EventArgs e) {
            var factory = _factory;
            if (factory == null) {
                // We have been disposed already, so ignore this event
                return;
            }

            _typeDb = factory.GetCurrentDatabase();
            _searchPathDb = null;
            _searchPathPackages = null;
            _zipPackageCache = null;

            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        #region IPythonInterpreter Members

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            if (id == BuiltinTypeId.Unknown) {
                return null;
            }

            if (_typeDb == null) {
                throw new KeyNotFoundException(string.Format("{0} ({1})", id, (int)id));
            }

            var name = SharedDatabaseState.GetBuiltinTypeName(id, _typeDb.LanguageVersion);
            var res = _typeDb.BuiltinModule.GetAnyMember(name) as IPythonType;
            if (res == null) {
                throw new KeyNotFoundException(string.Format("{0} ({1})", id, (int)id));
            }
            return res;
        }


        public IList<string> GetModuleNames() {
            var fromDb = (_typeDb?.GetModuleNames()).MaybeEnumerate().ToList();

            PythonTypeDatabase db;
            lock (_searchPathDbLock) {
                db = _searchPathDb;
            }
            fromDb.AddRange((db?.GetModuleNames()).MaybeEnumerate());

            fromDb.AddRange((_searchPathPackages?.Keys).MaybeEnumerate());

            return fromDb;
        }

        public IPythonModule ImportModule(string name) {
            var mod = _typeDb?.GetModule(name);
            if (mod == null) {
                PythonTypeDatabase db;
                lock (_searchPathDbLock) {
                    db = _searchPathDb;
                }
                mod = db?.GetModule(name);
                if (mod == null) {
                    foreach (var searchPath in _searchPaths.MaybeEnumerate()) {
                        try {
                            if (File.Exists(searchPath)) {
                                mod = LoadModuleFromZipFile(searchPath, name);
                            } else if (Directory.Exists(searchPath)) {
                                mod = LoadModuleFromDirectory(searchPath, name);
                            }
                        } catch (ArgumentException ex) {
                            Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                            return null;
                        }

                        if (mod != null) {
                            break;
                        }
                    }
                }
            }
            return mod;
        }

        private PythonTypeDatabase EnsureSearchPathDB() {
            lock (_searchPathDbLock) {
                if (_searchPathDb == null) {
                    _searchPathDb = new PythonTypeDatabase(_factory, innerDatabase: _typeDb);
                }
                return _searchPathDb;
            }
        }

        private async void BeginUpdateSearchPathPackages() {
            var cts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _searchPathPackagesCancellation, cts);
            try {
                oldCts?.Cancel();
                oldCts?.Dispose();
            } catch (ObjectDisposedException) {
            }

            try {
                await Task.Run(() => UpdateSearchPathPackagesAsync(cts.Token));
            } catch (OperationCanceledException) {
            } catch (ObjectDisposedException) {
            } catch (Exception ex) {
                // Cannot do anything more useful with the exception message here
                Debug.Fail(ex.ToString());
            } finally {
                Interlocked.CompareExchange(ref _searchPathPackagesCancellation, null, cts)?.Dispose();
            }
        }

        private async Task UpdateSearchPathPackagesAsync(CancellationToken cancellationToken) {
            var packageDict = new Dictionary<string, string>();

            foreach(var searchPath in _searchPaths.MaybeEnumerate()) {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyCollection<string> packages = null;
                if (File.Exists(searchPath)) {
                    packages = await GetPackagesFromZipFileAsync(searchPath, cancellationToken);
                } else if (Directory.Exists(searchPath)) {
                    packages = await GetPackagesFromDirectoryAsync(searchPath, cancellationToken);
                }
                foreach(var package in packages.MaybeEnumerate()) {
                    packageDict[package] = searchPath;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Exchange(ref _searchPathPackages, packageDict);
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task<IReadOnlyCollection<string>> GetPackagesFromDirectoryAsync(string searchPath, CancellationToken cancellationToken) {
            return ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                requireInitPy: ModulePath.PythonVersionRequiresInitPyFiles(_langVersion),
                includePackages: true
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n)).ToList();
        }

        private async Task<IReadOnlyCollection<string>> GetPackagesFromZipFileAsync(string searchPath, CancellationToken cancellationToken) {
            // TODO: Search zip files for packages
            return new string[0];
        }

        private IPythonModule LoadModuleFromDirectory(string searchPath, string moduleName) {
            Func<string, bool> isPackage = null;
            if (!ModulePath.PythonVersionRequiresInitPyFiles(_langVersion)) {
                isPackage = Directory.Exists;
            }

            ModulePath package;
            try {
                package = ModulePath.FromBasePathAndName(searchPath, moduleName, isPackage);
            } catch (ArgumentException) {
                return null;
            }

            var db = EnsureSearchPathDB();
            if (package.IsNativeExtension || package.IsCompiled) {
                db.LoadExtensionModule(package);
            } else {
                db.AddModule(package.FullName, AstPythonModule.FromFile(
                    this,
                    package.SourceFile,
                    _factory.GetLanguageVersion(),
                    package.FullName
                ));
            }

            if (db != _searchPathDb) {
                // Racing with the DB being invalidated.
                // It's okay if we miss it here, so don't worry
                // about taking the lock.
                return null;
            }

            var mod = db.GetModule(package.FullName);

            if (!package.IsSpecialName) {
                int i = package.FullName.LastIndexOf('.');
                if (i >= 1) {
                    var parent = package.FullName.Remove(i);
                    var parentMod = db.GetModule(parent) as AstPythonModule;
                    if (parentMod != null) {
                        parentMod.AddChildModule(package.Name, mod);
                    }
                }
            }

            lock (_searchPathDbLock) {
                if (db != _searchPathDb) {
                    // Raced with the DB being invalidated
                    return null;
                }
            }

            return mod;
        }

        class GetModuleCallable {
            private readonly HashSet<string> _packages;

            public GetModuleCallable(HashSet<string> packages) {
                _packages = packages;
            }

            public string GetModule(string basePath, string lastBit) {
                var candidate = Path.Combine(basePath, lastBit, "__init__.py");
                if (_packages.Contains(candidate)) {
                    return candidate;
                }
                candidate = Path.Combine(basePath, Path.ChangeExtension(lastBit, ".py"));
                if (_packages.Contains(candidate)) {
                    return candidate;
                }
                candidate = Path.Combine(basePath, Path.ChangeExtension(lastBit, ".pyw"));
                if (_packages.Contains(candidate)) {
                    return candidate;
                }
                return null;
            }
        }

        private IPythonModule LoadModuleFromZipFile(string zipFile, string moduleName) {
            ModulePath name;
            HashSet<string> packages = null;

            var cache = _zipPackageCache;
            if (cache == null) {
                cache = _zipPackageCache = new Dictionary<string, HashSet<string>>();
            }

            if (!cache.TryGetValue(zipFile, out packages) || packages == null) {
                using (var stream = new FileStream(zipFile, FileMode.Open, FileAccess.Read))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true)) {
                    cache[zipFile] = packages = new HashSet<string>(
                        zip.Entries.Select(e => e.FullName.Replace('/', '\\'))
                    );
                }
            }

            try {
                name = ModulePath.FromBasePathAndName(
                    "",
                    moduleName,
                    packageName => packages.Contains(packageName + '\\'),
                    new GetModuleCallable(packages).GetModule
                );
            } catch (ArgumentException) {
                return null;
            }

            using (var stream = new FileStream(zipFile, FileMode.Open, FileAccess.Read))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
            using (var sourceStream = zip.GetEntry(name.SourceFile.Replace('\\', '/'))?.Open()) {
                if (sourceStream == null) {
                    return null;
                }
                return AstPythonModule.FromStream(
                    this,
                    sourceStream,
                    PathUtils.GetAbsoluteFilePath(zipFile, name.SourceFile),
                    _factory.GetLanguageVersion()
                );
            }
        }

        public IModuleContext CreateModuleContext() {
            return null;
        }

        public void Initialize(PythonAnalyzer state) {
            if (_state != null) {
                _state.SearchPathsChanged -= PythonAnalyzer_SearchPathsChanged;
            }

            _state = state;

            if (_state != null) {
                _state.SearchPathsChanged += PythonAnalyzer_SearchPathsChanged;
                PythonAnalyzer_SearchPathsChanged(_state, EventArgs.Empty);
            }
        }

        private void PythonAnalyzer_SearchPathsChanged(object sender, EventArgs e) {
            lock (_searchPathDbLock) {
                _searchPaths = _state.GetSearchPaths();
                _searchPathDb = null;
                _zipPackageCache = null;
            }

            BeginUpdateSearchPathPackages();
        }

        public event EventHandler ModuleNamesChanged;

        #endregion


        public void Dispose() {
            lock (_searchPathDbLock) {
                _searchPathDb = null;
            }
            _zipPackageCache = null;
            _typeDb = null;

            var factory = _factory;
            _factory = null;
            if (factory != null) {
                factory.NewDatabaseAvailable -= OnNewDatabaseAvailable;
            }
        }
    }
}
