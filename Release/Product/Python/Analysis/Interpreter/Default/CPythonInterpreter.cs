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
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonInterpreter : IPythonInterpreter {
        private PythonTypeDatabase _typeDb;

        public CPythonInterpreter(PythonTypeDatabase typeDb) {
            _typeDb = typeDb;
        }

        #region IPythonInterpreter Members

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            string name = _typeDb.GetBuiltinTypeName(id);
            if (name == null) {
                return null;
            }

            var res = _typeDb.BuiltinModule.GetAnyMember(name) as IPythonType;
            Debug.Assert(res != null);
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

        public void Initialize(IInterpreterState state) {
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
    }
}
