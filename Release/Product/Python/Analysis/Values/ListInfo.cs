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
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a list object with tracked type information.
    /// </summary>
    class ListInfo : SequenceInfo {
        private ListAppendBoundBuiltinMethodInfo _appendMethod;
        private ListPopBoundBuiltinMethodInfo _popMethod;
        private ListInsertBoundBuiltinMethodInfo _insertMethod;
        private ListExtendBoundBuiltinMethodInfo _extendMethod;

        public ListInfo(ISet<Namespace>[] indexTypes, BuiltinClassInfo seqType)
            : base(indexTypes, seqType) {
                EnsureAppend();
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            switch (name) {
                case "append":
                    EnsureAppend();
                    if (_appendMethod != null) {
                        return _appendMethod.SelfSet;
                    }
                    break;
                case "pop":
                    EnsurePop();
                    if (_popMethod != null) {
                        return _popMethod.SelfSet;
                    }
                    break;
                case "insert":
                    EnsureInsert();
                    if (_insertMethod != null) {
                        return _insertMethod.SelfSet;
                    }
                    break;
                case "extend":
                    EnsureExtend();
                    if (_extendMethod != null) {
                        return _extendMethod.SelfSet;
                    }
                    break;
            }

            return base.GetMember(node, unit, name);
        }

        internal void AppendItem(ISet<Namespace> set) {
            ISet<Namespace> newTypes = set;
            bool madeSet = false;
            foreach (var type in IndexTypes) {
                newTypes = newTypes.Union(type, ref madeSet);
            }
            
            if (IndexTypes.Length < 1 || IndexTypes[0].Count != newTypes.Count) {
                ReturnValue.EnqueueDependents();
            }

            UnionType = newTypes;
            AddTypes(new[] { newTypes });
        }

        private void EnsureAppend() {
            if (_appendMethod == null) {
                ISet<Namespace> value;
                if (TryGetMember("append", out value)) {                    
                    _appendMethod = new ListAppendBoundBuiltinMethodInfo(this, (BuiltinMethodInfo)value.First());
                }
            }
        }

        private void EnsurePop() {
            if (_popMethod == null) {
                ISet<Namespace> value;
                if (TryGetMember("pop", out value)) {
                    _popMethod = new ListPopBoundBuiltinMethodInfo(this, (BuiltinMethodInfo)value.First());
                }
            }
        }

        private void EnsureInsert() {
            if (_insertMethod == null) {
                ISet<Namespace> value;
                if (TryGetMember("insert", out value)) {
                    _insertMethod = new ListInsertBoundBuiltinMethodInfo(this, (BuiltinMethodInfo)value.First());
                }
            }
        }

        private void EnsureExtend() {
            if (_extendMethod == null) {
                ISet<Namespace> value;
                if (TryGetMember("extend", out value)) {
                    _extendMethod = new ListExtendBoundBuiltinMethodInfo(this, (BuiltinMethodInfo)value.First());
                }
            }
        }
    }
}
