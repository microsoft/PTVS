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
        private PythonAnalyzer _state;
        private IReadOnlyList<string> _searchPaths;
        //private readonly object _referencesLock = new object();

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

            fromDb.AddRange((_searchPathDb?.GetModuleNames()).MaybeEnumerate());
            
            // TODO: Return list of not-yet-imported modules from search paths?

            return fromDb;
        }

        public IPythonModule ImportModule(string name) {
            var mod = _typeDb?.GetModule(name);
            if (mod == null) {
                mod = _searchPathDb?.GetModule(name);
                if (mod == null) {
                    foreach (var searchPath in _searchPaths.MaybeEnumerate()) {
                        try {
                            if (File.Exists(searchPath)) {
                                mod = LoadModuleFromZipFile(searchPath, name);
                            } else if (Directory.Exists(searchPath)) {
                                mod = LoadModuleFromDirectory(searchPath, name);
                            }
                        } catch (ArgumentException) {
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

        private void EnsureSearchPathDB() {
            if (_searchPathDb == null) {
                _searchPathDb = new PythonTypeDatabase(_factory, innerDatabase: _typeDb);
            }
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

            EnsureSearchPathDB();
            if (package.IsNativeExtension || package.IsCompiled) {
                _searchPathDb.LoadExtensionModule(package);
            } else {
                _searchPathDb.AddModule(package.FullName, AstPythonModule.FromFile(
                    this,
                    package.SourceFile,
                    _factory.GetLanguageVersion()
                ));
            }

            var mod = _searchPathDb.GetModule(package.FullName);

            if (!package.IsSpecialName) {
                int i = package.FullName.LastIndexOf('.');
                if (i >= 1) {
                    var parent = package.FullName.Remove(i);
                    var parentMod = _searchPathDb.GetModule(parent) as AstPythonModule;
                    if (parentMod != null) {
                        parentMod.AddChildModule(package.Name, mod);
                    }
                }
            }
            
            return mod;
        }

        private IPythonModule LoadModuleFromZipFile(string zipFile, string moduleName) {
            using (var stream = new FileStream(zipFile, FileMode.Open, FileAccess.Read))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true)) {
                return null;
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
            _searchPaths = _state.GetSearchPaths();
            _searchPathDb = null;
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler ModuleNamesChanged;

        //public Task AddReferenceAsync(ProjectReference reference, CancellationToken cancellationToken = default(CancellationToken)) {
        //    if (reference == null) {
        //        return MakeExceptionTask(new ArgumentNullException("reference"));
        //    }

        //    bool cloneDb = false;
        //    lock (_referencesLock) {
        //        if (_references == null) {
        //            _references = new HashSet<ProjectReference>();
        //            cloneDb = true;
        //        }
        //    }

        //    if (cloneDb && _typeDb != null) {
        //        // If we needed to set _references, then we also need to clone
        //        // _typeDb to avoid adding modules to the shared database.
        //        _typeDb = _typeDb.Clone();
        //    }

        //    switch (reference.Kind) {
        //        case ProjectReferenceKind.ExtensionModule:
        //            lock (_referencesLock) {
        //                _references.Add(reference);
        //            }
        //            string filename;
        //            try {
        //                filename = Path.GetFileNameWithoutExtension(reference.Name);
        //            } catch (Exception e) {
        //                return MakeExceptionTask(e);
        //            }

        //            if (_typeDb != null) {
        //                return _typeDb.LoadExtensionModuleAsync(filename,
        //                    reference.Name,
        //                    cancellationToken).ContinueWith(RaiseModulesChanged);
        //            }
        //            break;
        //    }

        //    return Task.Factory.StartNew(EmptyTask);
        //}

        //public void RemoveReference(ProjectReference reference) {
        //    switch (reference.Kind) {
        //        case ProjectReferenceKind.ExtensionModule:
        //            bool removed = false;
        //            lock (_referencesLock) {
        //                removed = _references != null && _references.Remove(reference);
        //            }
        //            if (removed && _typeDb != null) {
        //                _typeDb.UnloadExtensionModule(Path.GetFileNameWithoutExtension(reference.Name));
        //                RaiseModulesChanged(null);
        //            }
        //            break;
        //    }
        //}

        //public IEnumerable<ProjectReference> GetReferences() {
        //    var references = _references;
        //    return references != null ?
        //        references.AsLockedEnumerable(_referencesLock) :
        //        Enumerable.Empty<ProjectReference>();
        //}

        //private static Task MakeExceptionTask(Exception e) {
        //    var res = new TaskCompletionSource<Task>();
        //    res.SetException(e);
        //    return res.Task;
        //}

        //private static void EmptyTask() {
        //}

        //private void RaiseModulesChanged(Task task) {
        //    if (task != null && task.Exception != null) {
        //        throw task.Exception;
        //    }
        //    var modNamesChanged = ModuleNamesChanged;
        //    if (modNamesChanged != null) {
        //        modNamesChanged(this, EventArgs.Empty);
        //    }
        //}

        #endregion


        public void Dispose() {
            _searchPathDb = null;
            _typeDb = null;

            var factory = _factory;
            _factory = null;
            if (factory != null) {
                factory.NewDatabaseAvailable -= OnNewDatabaseAvailable;
            }
        }
    }
}
