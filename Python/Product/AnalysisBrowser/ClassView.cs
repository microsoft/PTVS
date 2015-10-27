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
using System.IO;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class ClassView : MemberView {
        readonly IPythonType _type;
        
        public ClassView(IModuleContext context, string name, IPythonType member)
            : base(context, name, member) {
            _type = member;
        }
        
        public string OriginalName {
            get { return _type.Name; }
        }

        public override string SortKey {
            get { return "1"; }
        }

        public override string DisplayType {
            get { return "Type"; }
        }

        public override IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                foreach (var p in base.Properties) {
                    yield return p;
                }

                yield return new KeyValuePair<string, object>("Original Name", OriginalName);

                int i = 1;
                var mro = _type.Mro;
                if (mro != null) {
                    foreach (var c in mro) {
                        yield return new KeyValuePair<string, object>(string.Format("MRO #{0}", i++), MemberView.Make(_context, c == null ? "(null)" : c.Name, c));
                    }
                }
            }
        }

        public override void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}class {1}", currentIndent, Name);
            exportChildren = SortedChildren;
        }
    }
}
