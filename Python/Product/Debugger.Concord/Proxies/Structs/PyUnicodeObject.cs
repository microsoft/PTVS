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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.PythonTools.Debugger.Concord.Proxies.Structs {
    internal abstract class PyUnicodeObject : PyVarObject, IPyBaseStringObject {
        protected static readonly Encoding _latin1 = Encoding.GetEncoding("Latin1");

        public PyUnicodeObject(DkmProcess process, ulong address)
            : base(process, address) {
        }

        public override void Repr(ReprBuilder builder) {
            builder.AppendLiteral(ToString());
        }

        public override IEnumerable<PythonEvaluationResult> GetDebugChildren(ReprOptions reprOptions) {
            string s = ToString();

            yield return new PythonEvaluationResult(new ValueStore<long>(s.Length), "len()") {
                Category = DkmEvaluationResultCategory.Method
            };

            foreach (char c in s) {
                yield return new PythonEvaluationResult(new ValueStore<string>(c.ToString()));
            }
        }

        public static explicit operator string(PyUnicodeObject obj) {
            return (object)obj == null ? null : obj.ToString();
        }

        public static PyUnicodeObject Create(DkmProcess process, string value) {
            if (process.GetPythonRuntimeInfo().LanguageVersion <= PythonLanguageVersion.V311) {
                return PyUnicodeObject311.Create311(process, value);
            } else {
                return PyUnicodeObject312.Create312(process, value);
            }
        }
    }


}
