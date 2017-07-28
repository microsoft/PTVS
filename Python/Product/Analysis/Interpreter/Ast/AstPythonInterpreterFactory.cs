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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonInterpreterFactory : IPythonInterpreterFactory {
        private readonly string _databasePath;
        private readonly object _searchPathsLock = new object();
        private PythonLibraryPath[] _searchPaths;
        private IReadOnlyDictionary<string, string> _searchPathPackages;

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
            var sp = GetCurrentSearchPaths();
            lock (_searchPathsLock) {
                spp = _searchPathPackages;
                if (spp != null) {
                    return spp;
                }

                var packageDict = new Dictionary<string, string>();

                foreach (var searchPath in _searchPaths.MaybeEnumerate()) {
                    IReadOnlyCollection<string> packages = null;
                    if (File.Exists(searchPath.Path)) {
                        packages = GetPackagesFromZipFile(searchPath.Path);
                    } else if (Directory.Exists(searchPath.Path)) {
                        packages = GetPackagesFromDirectory(searchPath.Path);
                    }
                    foreach (var package in packages.MaybeEnumerate()) {
                        packageDict[package] = searchPath.Path;
                    }
                }

                if (packageDict.Any()) {
                    _searchPathPackages = packageDict;
                }
                return packageDict;
            }
        }

        private IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath) {
            return ModulePath.GetModulesInPath(
                searchPath,
                recurse: false
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n)).ToList();
        }

        private IReadOnlyCollection<string> GetPackagesFromZipFile(string searchPath) {
            // TODO: Search zip files for packages
            return new string[0];
        }


        private IEnumerable<PythonLibraryPath> GetCurrentSearchPaths() {
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
