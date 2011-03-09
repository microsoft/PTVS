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
        private TypeDatabase _typeDb;

        public CPythonInterpreter(TypeDatabase typeDb) {
            _typeDb = typeDb;
        }

        #region IPythonInterpreter Members

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            var builtinModule = _typeDb.BuiltinModule;
            string name;
            switch (id) {
                case BuiltinTypeId.Bool: name = "bool"; break;
                case BuiltinTypeId.Complex: name = "complex"; break;
                case BuiltinTypeId.Dict: name = "dict"; break;
                case BuiltinTypeId.Float: name = "float"; break;
                case BuiltinTypeId.Int: name = "int"; break;
                case BuiltinTypeId.List: name = "list"; break;
                case BuiltinTypeId.Long: name = "long"; break;
                case BuiltinTypeId.Object: name = "object"; break;
                case BuiltinTypeId.Set: name = "set"; break;
                case BuiltinTypeId.Str: name = "str"; break;
                case BuiltinTypeId.Tuple: name = "tuple"; break;
                case BuiltinTypeId.Type: name = "type"; break;

                case BuiltinTypeId.BuiltinFunction: name = "builtin_function"; break;
                case BuiltinTypeId.BuiltinMethodDescriptor: name = "builtin_method_descriptor"; break;
                case BuiltinTypeId.Function: name = "function"; break;
                case BuiltinTypeId.Generator: name = "generator"; break;
                case BuiltinTypeId.NoneType: name = "NoneType"; break;
                case BuiltinTypeId.Ellipsis: name = "ellipsis"; break;

                default: return null;
            }

            var res = builtinModule.GetAnyMember(name) as IPythonType;
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

        internal TypeDatabase TypeDb {
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
