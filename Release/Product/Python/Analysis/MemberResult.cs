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
using System.Text;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public struct MemberResult {
        private readonly string _name;
        private string _completion;
        private readonly Func<IEnumerable<Namespace>> _vars;
        private readonly PythonMemberType? _type;

        internal MemberResult(string name, IEnumerable<Namespace> vars) {
            _name = _completion = name;
            _vars = () => vars;
            _type = null;
        }

        public MemberResult(string name, PythonMemberType type) {
            _name = _completion = name;
            _type = type;
            _vars = () => Empty;
        }

        internal MemberResult(string name, string completion, IEnumerable<Namespace> vars, PythonMemberType? type) {
            _name = name;
            _vars = () => vars;
            _completion = completion;
            _type = type;
        }

        internal MemberResult(string name, Func<IEnumerable<Namespace>> vars, PythonMemberType? type) {
            _name = _completion = name;
            _vars = vars;
            _type = type;
        }

        public MemberResult FilterCompletion(string completion) {
            return new MemberResult(Name, completion, Namespaces, MemberType);
        }

        private static Namespace[] Empty = new Namespace[0];

        public string Name {
            get { return _name; }
        }

        public string Completion {
            get { return _completion; }
        }

        public string Documentation {
            get {
                var docs = new HashSet<string>();
                var doc = new StringBuilder();
                foreach (var ns in _vars()) {
                    if (docs.Add(ns.Documentation)) {
                        doc.AppendLine(ns.Documentation);
                        doc.AppendLine();
                    }
                }
                return Utils.CleanDocumentation(doc.ToString());
            }
        }

        public PythonMemberType MemberType {
            get {
                return _type ?? GetMemberType();
            }
        }

        private PythonMemberType GetMemberType() {
            PythonMemberType result = PythonMemberType.Unknown;
            foreach (var ns in _vars()) {
                var nsType = ns.ResultType;
                if (result == PythonMemberType.Unknown) {
                    result = nsType;
                } else if (result != nsType) {
                    if ((nsType == PythonMemberType.Constant && result == PythonMemberType.Instance) ||
                        (nsType == PythonMemberType.Instance && result == PythonMemberType.Constant)) {
                        nsType = PythonMemberType.Instance;
                    } else {
                        return PythonMemberType.Multiple;
                    }
                }
            }
            if (result == PythonMemberType.Unknown) {
                return PythonMemberType.Instance;
            }
            return result;
        }

        internal IEnumerable<Namespace> Namespaces {
            get {
                return _vars();
            }
        }
    }
}
