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
