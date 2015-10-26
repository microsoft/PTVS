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
using System.Linq;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class ValueView : MemberView {
        readonly IPythonConstant _value;
        IAnalysisItemView _type;

        public ValueView(IModuleContext context, string name, IPythonConstant member)
            : base(context, name, member) {
            _value = member;
        }

        public override string SortKey {
            get { return "3"; }
        }

        public override string DisplayType {
            get { return "Constant"; }
        }

        public IAnalysisItemView Type {
            get {
                if (_value != null && _value.Type != null && _type == null) {
                    _type = MemberView.Make(_context, _value.Type.Name, _value.Type);
                }
                return _type;
            }
        }

        public override IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                foreach (var p in base.Properties) {
                    yield return p;
                }

                if (Type != null) {
                    yield return new KeyValuePair<string, object>("Type", Type);
                }
            }
        }

        public override void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}{1}: {2} = {3}", currentIndent, DisplayType, Name, _value.Type.Name);
            exportChildren = null;
        }
    }
}
