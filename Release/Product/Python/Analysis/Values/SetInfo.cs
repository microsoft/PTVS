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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class SetInfo : BuiltinInstanceInfo {
        private VariableDef _valueTypes;

        public SetInfo(PythonAnalyzer projectState)
            : base(projectState._setType) {
            _valueTypes = new VariableDef();
        }

        public void AddTypes(Node node, AnalysisUnit unit, ISet<Namespace> types) {
            _valueTypes.AddTypes(node, unit, types);
        }

        public override ISet<Namespace> GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            _valueTypes.AddDependency(unit);
            return _valueTypes.Types;
        }

        public override string ShortDescription {
            get {
                return "set";
            }
        }

        public override string Description {
            get {
                // set({k})
                Namespace valueType = _valueTypes.Types.GetUnionType();
                string valueName = valueType == null ? null : valueType.ShortDescription;

                if (valueName != null) {
                    return "set({" + valueName + "})";
                }

                return "set";
            }
        }

        public override bool UnionEquals(Namespace ns) {
            return ns is DictionaryInfo;
        }

        public override int UnionHashCode() {
            return 2;
        }

        public override PythonMemberType ResultType {
            get {
                return PythonMemberType.Field;
            }
        }
    }
}
