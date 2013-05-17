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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreter : IPythonInterpreter, IPythonInterpreter2 {
        private PythonTypeDatabase _typeDb;
        private HashSet<ProjectReference> _references;
        private readonly IPythonInterpreterFactory _factory;

        public CPythonInterpreter(IPythonInterpreterFactory interpFactory, PythonTypeDatabase typeDb) {
            _typeDb = typeDb;
            _factory = interpFactory;
        }

        #region IPythonInterpreter Members

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            if (id == BuiltinTypeId.Unknown) {
                return null;
            }
            
            if (id == BuiltinTypeId.Str) {
                if (_factory.Configuration.Version.Major == 3) {
                    id = BuiltinTypeId.Unicode;
                } else {
                    id = BuiltinTypeId.Bytes;
                }
            } else if (id == BuiltinTypeId.StrIterator) {
                if (_factory.Configuration.Version.Major == 2) {
                    id = BuiltinTypeId.UnicodeIterator;
                } else {
                    id = BuiltinTypeId.BytesIterator;
                }
            } else if (id == BuiltinTypeId.Long) {
                if (_factory.Configuration.Version.Major == 3) {
                    id = BuiltinTypeId.Int;
                }
            }

            var name = SharedDatabaseState.GetBuiltinTypeName(id, _factory.GetLanguageVersion().Is3x());
            var res = _typeDb.BuiltinModule.GetAnyMember(name) as IPythonType;
            if (res == null) {
                throw new KeyNotFoundException(string.Format("{0} ({1})", id, (int)id));
            }
            return res;
        }


        public IList<string> GetModuleNames() {
            return new List<string>(_typeDb.GetModuleNames());
        }

        public IPythonModule ImportModule(string name) {
            return _typeDb.GetModule(name);
        }

        public IModuleContext CreateModuleContext() {
            return null;
        }

        public void Initialize(PythonAnalyzer state) {
        }

        public void NotifyInvalidDatabase() {
            var withDb = _factory as IInterpreterWithCompletionDatabase;
            if (withDb != null) {
                withDb.NotifyInvalidDatabase();
            }
        }

        internal PythonTypeDatabase TypeDb {
            get {
                return _typeDb;
            }
            set {
                _typeDb = value;
                var modsChanged = ModuleNamesChanged;
                if (modsChanged != null) {
                    modsChanged(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler ModuleNamesChanged;

        #endregion

        #region IPythonInterpreter2 Members

        public Task AddReferenceAsync(ProjectReference reference, CancellationToken cancellationToken = default(CancellationToken)) {
            if (reference == null) {
                return MakeExceptionTask(new ArgumentNullException("reference"));
            }

            EnsureInstanceDb();

            switch (reference.Kind) {
                case ProjectReferenceKind.ExtensionModule:
                    if (_references == null) {
                        _references = new HashSet<ProjectReference>();
                    }
                    _references.Add(reference);
                    string filename;
                    try {
                        filename = Path.GetFileNameWithoutExtension(reference.Name);
                    } catch (Exception e) {
                        return MakeExceptionTask(e);
                    }

                    return _typeDb.LoadExtensionModuleAsync(_factory,
                        filename,
                        reference.Name,
                        cancellationToken).ContinueWith(RaiseModulesChanged);
            }

            return Task.Factory.StartNew(EmptyTask);
        }

        public void RemoveReference(ProjectReference reference) {
            switch (reference.Kind) {
                case ProjectReferenceKind.ExtensionModule:
                    if (_references != null && _references.Remove(reference)) {
                        _typeDb.UnloadExtensionModule(Path.GetFileNameWithoutExtension(reference.Name));
                        RaiseModulesChanged(null);
                    }
                    break;
            }
        }

        private static Task MakeExceptionTask(Exception e) {
            var res = new TaskCompletionSource<Task>();
            res.SetException(e);
            return res.Task;
        }

        private static void EmptyTask() {
        }

        private void RaiseModulesChanged(Task task) {
            if (task != null && task.Exception != null) {
                throw task.Exception;
            }
            var modNamesChanged = ModuleNamesChanged;
            if (modNamesChanged != null) {
                modNamesChanged(this, EventArgs.Empty);
            }
        }

        private void EnsureInstanceDb() {
            if (!_typeDb.CanLoadModules) {
                _typeDb = _typeDb.Clone();
            }
        }

        #endregion
    }
}
