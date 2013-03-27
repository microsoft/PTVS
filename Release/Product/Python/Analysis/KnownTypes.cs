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
using System.Linq;
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

        public KnownTypes(PythonAnalyzer state) {
            ITypeDatabaseReader fallbackDb = null;
            IBuiltinPythonModule fallback = null;

            int count = (int)BuiltinTypeIdExtensions.LastTypeId + 1;
            _types = new IPythonType[count];
            _classInfos = new BuiltinClassInfo[count];

            var interpreter = state.Interpreter;

            for (int value = 0; value < count; ++value) {
                try {
                    _types[value] = interpreter.GetBuiltinType((BuiltinTypeId)value);
                } catch (KeyNotFoundException) {
                    if (fallback == null) {
                        var tempDb = PythonTypeDatabase.CreateDefaultTypeDatabase(state.LanguageVersion.ToVersion());
                        fallbackDb = (ITypeDatabaseReader)tempDb;
                        fallback = tempDb.BuiltinModule;
                    }
                    
                    _types[value] = fallback.GetAnyMember(fallbackDb.GetBuiltinTypeName((BuiltinTypeId)value)) as IPythonType;
                }
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
