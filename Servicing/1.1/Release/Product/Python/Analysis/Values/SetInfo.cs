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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class SetInfo : BuiltinInstanceInfo {
        private ISet<Namespace> _valueTypes;

        public SetInfo(ISet<Namespace> valueTypes, PythonAnalyzer projectState)
            : base(projectState._setType) {
            _valueTypes = valueTypes;
        }

        public void AddTypes(ISet<Namespace> types) {
            _valueTypes = _valueTypes.Union(types);
        }

        public override string ShortDescription {
            get {
                return "set";
            }
        }

        public override string Description {
            get {
                // set({k})
                Namespace valueType = _valueTypes.GetUnionType();
                string valueName = valueType == null ? null : valueType.ShortDescription;

                if (valueName != null) {
                    return "{" +
                        (valueName ?? "unknown") +
                        "}";
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
