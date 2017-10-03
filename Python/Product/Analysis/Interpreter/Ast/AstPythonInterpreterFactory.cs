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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonInterpreterFactory : IPythonInterpreterFactory, IDisposable {
        private readonly string _databasePath;
        private readonly object _searchPathsLock = new object();
        private PythonLibraryPath[] _searchPaths;
        private IReadOnlyDictionary<string, string> _searchPathPackages;

        private bool _disposed;
        private readonly bool _skipCache;

        public AstPythonInterpreterFactory(
            InterpreterConfiguration config,
            InterpreterFactoryCreationOptions options
        ) {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));
            LanguageVersion = Configuration.Version.ToLanguageVersion();

            options = options ?? new InterpreterFactoryCreationOptions();
            _databasePath = options.DatabasePath;
            _skipCache = !options.UseExistingCache;

            if (!GlobalInterpreterOptions.SuppressPackageManagers) {
                try {
                    var pm = options.PackageManager;
                    if (pm != null) {
                        pm.SetInterpreterFactory(this);
                        pm.InstalledFilesChanged += PackageManager_InstalledFilesChanged;
                        PackageManager = pm;
                    }
                } catch (NotSupportedException) {
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AstPythonInterpreterFactory() {
            Dispose(false);
        }

        protected void Dispose(bool disposing) {
            if (!_disposed) {
                _disposed = true;

                if (disposing) {
                    if (PackageManager != null) {
                        PackageManager.InstalledPackagesChanged -= PackageManager_InstalledFilesChanged;
                    }
                }
            }
        }

        public InterpreterConfiguration Configuration { get; }

        public PythonLanguageVersion LanguageVersion { get; }

        public IPackageManager PackageManager { get; }

        public event EventHandler ImportableModulesChanged;

        private void PackageManager_InstalledFilesChanged(object sender, EventArgs e) {
            lock (_searchPathsLock) {
                _searchPaths = null;
                _searchPathPackages = null;
            }
            ImportableModulesChanged?.Invoke(this, EventArgs.Empty);
        }

        public IPythonInterpreter CreateInterpreter() {
            return new AstPythonInterpreter(this);
        }

        internal string GetCacheFilePath(string filePath) {
            if (string.IsNullOrEmpty(_databasePath)) {
                return null;
            }

            var hash = SHA256.Create();
            var dir = PathUtils.GetParent(filePath);
            var dirHash = Convert.ToBase64String(hash.ComputeHash(new UTF8Encoding(false).GetBytes(dir)))
                .Replace('/', '_').Replace('+', '-');

            return Path.ChangeExtension(PathUtils.GetAbsoluteFilePath(
                _databasePath,
                Path.Combine(dirHash, PathUtils.GetFileOrDirectoryName(filePath))
            ), ".pyi");
        }

        #region Cache File Management

        public Stream ReadCachedModule(string filePath) {
            if (_skipCache) {
                return null;
            }

            FileStream file = null;
            var path = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            for (int retries = 5; retries > 0; --retries) {
                try {
                    file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                } catch (DirectoryNotFoundException) {
                    return null;
                } catch (FileNotFoundException) {
                    return null;
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Thread.Sleep(10);
                }
            }

            if (file != null) {
                bool fileIsOkay = false;
                try {
                    var cacheTime = File.GetLastWriteTimeUtc(path);
                    var sourceTime = File.GetLastWriteTimeUtc(filePath);
                    if (sourceTime <= cacheTime) {
                        fileIsOkay = true;
                    }
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                }

                if (!fileIsOkay) {
                    file.Dispose();
                    file = null;
                    try {
                        File.Delete(path);
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                    }
                }
            }

            return file;
        }

        private static FileStream OpenAndOverwrite(string path) {
            for (int retries = 5; retries > 0; --retries) {
                try {
                    return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                } catch (DirectoryNotFoundException) {
                    var dir = PathUtils.GetParent(path);
                    if (Directory.Exists(dir)) {
                        break;
                    }

                    // Directory does not exist, so try to create it.
                    try {
                        Directory.CreateDirectory(dir);
                    } catch (Exception ex) when (!ex.IsCriticalException()) {
                        Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(AstPythonInterpreterFactory)));
                        break;
                    }
                } catch (UnauthorizedAccessException) {
                    if (!File.Exists(path)) {
                        break;
                    }

                    // File exists, so may be marked readonly. Try and delete it
                    File_ReallyDelete(path);
                } catch (IOException) {
                    break;
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(AstPythonInterpreterFactory)));
                    break;
                }

                Thread.Sleep(10);
            }
            return null;
        }

        private static void File_ReallyDelete(string path) {
            for (int retries = 5; retries > 0 && File.Exists(path); --retries) {
                try {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(AstPythonInterpreterFactory)));
                    break;
                }
            }
        }

        public void WriteCachedModule(string filePath, Stream code) {
            var cache = GetCacheFilePath(filePath);
            if (string.IsNullOrEmpty(cache)) {
                return;
            }

            try {
                using (var stream = OpenAndOverwrite(cache)) {
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

        public IReadOnlyDictionary<string, string> GetImportableModules() {
            var spp = _searchPathPackages;
            if (spp != null) {
                return spp;
            }
            var sp = GetSearchPaths();
            if (sp == null) {
                return null;
            }

            lock (_searchPathsLock) {
                spp = _searchPathPackages;
                if (spp != null) {
                    return spp;
                }

                var packageDict = GetImportableModules(sp.Select(p => p.Path));

                if (!packageDict.Any()) {
                    return null;
                }

                _searchPathPackages = packageDict;
                return packageDict;
            }
        }

        public static IReadOnlyDictionary<string, string> GetImportableModules(IEnumerable<string> searchPaths) {
            var packageDict = new Dictionary<string, string>();

            foreach (var searchPath in searchPaths.MaybeEnumerate()) {
                IReadOnlyCollection<string> packages = null;
                if (File.Exists(searchPath)) {
                    packages = GetPackagesFromZipFile(searchPath);
                } else if (Directory.Exists(searchPath)) {
                    packages = GetPackagesFromDirectory(searchPath);
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
        internal void SetCurrentSearchPaths(IEnumerable<PythonLibraryPath> paths) {
            lock (_searchPathsLock) {
                _searchPaths = paths.ToArray();
                _searchPathPackages = null;
            }
            ImportableModulesChanged?.Invoke(this, EventArgs.Empty);
        }

        private IEnumerable<PythonLibraryPath> GetCurrentSearchPaths() {
            if (!File.Exists(Configuration?.InterpreterPath)) {
                return null;
            }

            try {
                return PythonTypeDatabase.GetUncachedDatabaseSearchPathsAsync(Configuration.InterpreterPath).WaitAndUnwrapExceptions();
            } catch (InvalidOperationException) {
                return null;
            }
        }

        public IEnumerable<PythonLibraryPath> GetSearchPaths() {
            var sp = _searchPaths;
            if (sp == null) {
                lock (_searchPathsLock) {
                    sp = _searchPaths;
                    if (sp == null) {
                        _searchPaths = sp = GetCurrentSearchPaths().MaybeEnumerate().ToArray();
                    }
                }
            }
            return sp;
        }

        private ModulePath FindModule(string filePath) {
            var sp = GetSearchPaths();

            string bestLibraryPath = "";

            foreach (var p in sp) {
                if (PathUtils.IsSubpathOf(p.Path, filePath)) {
                    if (p.Path.Length > bestLibraryPath.Length) {
                        bestLibraryPath = p.Path;
                    }
                }
            }

            var mp = ModulePath.FromFullPath(filePath, bestLibraryPath);
            return mp;
        }

        public static ModulePath FindModule(IPythonInterpreterFactory factory, string filePath) {
            try {
                var apif = factory as AstPythonInterpreterFactory;
                if (apif != null) {
                    return apif.FindModule(filePath);
                }

                return ModulePath.FromFullPath(filePath);
            } catch (ArgumentException) {
                return default(ModulePath);
            }
        }
    }
}
