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
using System.IO;
using System.Linq;
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

        public AstPythonInterpreterFactory(
            InterpreterConfiguration config,
            InterpreterFactoryCreationOptions options
        ) {
            if (config == null) {
                throw new ArgumentNullException(nameof(config));
            }
            if (options == null) {
                options = new InterpreterFactoryCreationOptions();
            }
            Configuration = config;
            LanguageVersion = Configuration.Version.ToLanguageVersion();

            _databasePath = options.DatabasePath;

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
