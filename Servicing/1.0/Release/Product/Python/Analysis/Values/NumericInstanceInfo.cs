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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class NumericInstanceInfo : BuiltinInstanceInfo {

        public NumericInstanceInfo(BuiltinClassInfo klass)
            : base(klass) {
        }

        public override ISet<Namespace> BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {
            switch (operation) {
                case PythonOperator.GreaterThan:
                case PythonOperator.LessThan:
                case PythonOperator.LessThanOrEqual:
                case PythonOperator.GreaterThanOrEqual:
                case PythonOperator.Equal:
                case PythonOperator.NotEqual:
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                    return ProjectState._boolType.Instance;
                case PythonOperator.TrueDivide:
                    if (ClassInfo == unit.ProjectState._intType || ClassInfo == unit.ProjectState._longType) {
                        bool intsOnly = true, rhsInt = false;
                        foreach (var type in rhs) {
                            if (type.IsOfType(unit.ProjectState._intType) || type.IsOfType(unit.ProjectState._longType)) {
                                rhsInt = true;
                            } else {
                                intsOnly = false;
                            }
                        }

                        if (rhsInt) {
                            if (intsOnly) {
                                return unit.ProjectState._floatType;
                            }
                            return base.BinaryOperation(node, unit, operation, rhs).Union(unit.ProjectState._floatType);
                        }
                    }
                    break;
            }
            return base.BinaryOperation(node, unit, operation, rhs);
        }
    }
}
