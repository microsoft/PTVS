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
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonInterpreterFactory : IPythonInterpreterFactory {
        private readonly string _databasePath;
        private readonly object _searchPathsLock = new object();
        private PythonLibraryPath[] _searchPaths;
        private string[] _importableModules;

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

        public IPackageManager PackageManager { get; }

        private void PackageManager_InstalledFilesChanged(object sender, EventArgs e) {
            _searchPaths = null;
        }

        public IPythonInterpreter CreateInterpreter() {
            return new AstPythonInterpreter(this);
        }

        public IList<string> GetImportableModules() {
            var im = _importableModules;
            if (im != null) {
                return im;
            }
            var sp = GetCurrentSearchPaths();
            lock (_searchPathsLock) {
                im = _importableModules;
                if (im != null) {
                    return im;
                }

                // TODO: Find all importable modules
                im = new string[0];

                _importableModules = im;
                return im;
            }
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
    }
}
