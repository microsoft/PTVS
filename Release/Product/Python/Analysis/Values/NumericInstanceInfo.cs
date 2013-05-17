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

using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class NumericInstanceInfo : BuiltinInstanceInfo {
        public NumericInstanceInfo(BuiltinClassInfo klass)
            : base(klass) {
        }

        public override INamespaceSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, INamespaceSet rhs) {
            switch (operation) {
                case PythonOperator.GreaterThan:
                case PythonOperator.LessThan:
                case PythonOperator.LessThanOrEqual:
                case PythonOperator.GreaterThanOrEqual:
                case PythonOperator.Equal:
                case PythonOperator.NotEqual:
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                    return ProjectState.ClassInfos[BuiltinTypeId.Bool].Instance;
                case PythonOperator.TrueDivide:
                case PythonOperator.Add:
                case PythonOperator.Subtract:
                case PythonOperator.Multiply:
                case PythonOperator.Divide:
                case PythonOperator.Mod:
                case PythonOperator.BitwiseAnd:
                case PythonOperator.BitwiseOr:
                case PythonOperator.Xor:
                case PythonOperator.LeftShift:
                case PythonOperator.RightShift:
                case PythonOperator.Power:
                case PythonOperator.FloorDivide:
                    return ConstantInfo.NumericOp(node, this, unit, operation, rhs) ?? base.BinaryOperation(node, unit, operation, rhs);
            }
            return base.BinaryOperation(node, unit, operation, rhs);
        }
    }
}
