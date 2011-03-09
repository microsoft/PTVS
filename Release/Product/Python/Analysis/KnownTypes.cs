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

using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.PyAnalysis {
    class KnownTypes {
        public readonly IPythonType Set, Function, Generator, Dict, Bool, List, Tuple, BuiltinFunction, BuiltinMethodDescriptor, Object, Float, Int, Str, None, Complex, Long, Ellipsis;

        public KnownTypes(PythonAnalyzer state) {
            var interpreter = state.Interpreter;

            None = interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
            Set = interpreter.GetBuiltinType(BuiltinTypeId.Set);
            Function = interpreter.GetBuiltinType(BuiltinTypeId.Function);
            Generator = interpreter.GetBuiltinType(BuiltinTypeId.Generator);
            Dict = interpreter.GetBuiltinType(BuiltinTypeId.Dict);
            Bool = interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            List = interpreter.GetBuiltinType(BuiltinTypeId.List);
            Tuple = interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
            BuiltinFunction = interpreter.GetBuiltinType(BuiltinTypeId.BuiltinFunction);
            BuiltinMethodDescriptor = interpreter.GetBuiltinType(BuiltinTypeId.BuiltinMethodDescriptor);
            Object = interpreter.GetBuiltinType(BuiltinTypeId.Object);
            Float = interpreter.GetBuiltinType(BuiltinTypeId.Float);
            Int = interpreter.GetBuiltinType(BuiltinTypeId.Int);
            Str = interpreter.GetBuiltinType(BuiltinTypeId.Str);
            Complex = interpreter.GetBuiltinType(BuiltinTypeId.Complex);
            if (!state.LanguageVersion.Is3x()) {
                Long = interpreter.GetBuiltinType(BuiltinTypeId.Long);
            }
            Ellipsis = interpreter.GetBuiltinType(BuiltinTypeId.Ellipsis);            
        }
    }
}
