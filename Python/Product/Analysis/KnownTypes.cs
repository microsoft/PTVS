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

using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.PyAnalysis {
    internal interface IKnownPythonTypes {
        IPythonType this[BuiltinTypeId id] { get; }
    }

    internal interface IKnownClasses {
        BuiltinClassInfo this[BuiltinTypeId id] { get; }
    }

    internal class KnownTypes : IKnownPythonTypes, IKnownClasses {
        internal readonly IPythonType[] _types;
        internal readonly BuiltinClassInfo[] _classInfos;

        public static KnownTypes CreateDefault(PythonAnalyzer state, PythonTypeDatabase fallbackDb) {
            var res = new KnownTypes();

            var fallback = fallbackDb.BuiltinModule;

            for (int value = 0; value < res._types.Length; ++value) {
                res._types[value] = (IPythonType)fallback.GetAnyMember(
                    ((ITypeDatabaseReader)fallbackDb).GetBuiltinTypeName((BuiltinTypeId)value)
                );
            }

            res.SetClassInfo(state);
            return res;
        }

        public static KnownTypes Create(PythonAnalyzer state, PythonTypeDatabase fallbackDb) {
            var res = new KnownTypes();

            var interpreter = state.Interpreter;
            var fallback = fallbackDb.BuiltinModule;

            for (int value = 0; value < res._types.Length; ++value) {
                try {
                    res._types[value] = interpreter.GetBuiltinType((BuiltinTypeId)value);
                } catch (KeyNotFoundException) {
                    res._types[value] = (IPythonType)fallback.GetAnyMember(
                        ((ITypeDatabaseReader)fallbackDb).GetBuiltinTypeName((BuiltinTypeId)value)
                    );
                }
            }

            res.SetClassInfo(state);
            return res;
        }

        private KnownTypes() {
            int count = (int)BuiltinTypeIdExtensions.LastTypeId + 1;
            _types = new IPythonType[count];
            _classInfos = new BuiltinClassInfo[count];
        }

        private void SetClassInfo(PythonAnalyzer state) {
            for (int value = 0; value < _types.Length; ++value) {
                if (_types[value] != null) {
                    _classInfos[value] = state.GetBuiltinType(_types[value]);
                }
            }
        }

        IPythonType IKnownPythonTypes.this[BuiltinTypeId id] {
            get {
                return _types[(int)id];
            }
        }

        BuiltinClassInfo IKnownClasses.this[BuiltinTypeId id] {
            get {
                return _classInfos[(int)id];
            }
        }
    }
}
