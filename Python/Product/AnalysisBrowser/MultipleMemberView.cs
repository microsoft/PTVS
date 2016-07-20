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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class MultipleMemberView : MemberView {
        readonly IPythonMultipleMembers _members;
        List<IAnalysisItemView> _children;
        
        public MultipleMemberView(IModuleContext context, string name, IPythonMultipleMembers member) :
            base(context, name, member) {
            _members = member;
        }
        
        public override string SortKey {
            get { return "6"; }
        }

        public override string DisplayType {
            get { return "Multiple values"; }
        }

        public override IEnumerable<IAnalysisItemView> Children {
            get {
                if (_children == null) {
                    _children = _members.Members.Select(m => MemberView.Make(_context, Name, m)).ToList();
                }
                return _children;
            }
        }

        public override void ExportToDiffable(
            TextWriter writer,
            string currentIndent,
            string indent,
            Stack<IAnalysisItemView> exportStack,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            var children = new HashSet<string>();
            foreach (var c in Children) {
                string displayValue = "";
                if (c is ClassView) {
                    displayValue = ((ClassView)c).OriginalName;
                } else if (c is FunctionView) {
                    displayValue = ((FunctionView)c).OverloadSummary;
                } else if (c is ValueView) {
                    displayValue = ((ValueView)c).Type.Name;
                }
                children.Add(string.Format("{0}{1}({2})", displayValue, string.IsNullOrEmpty(displayValue) ? "" : " ", c.DisplayType));
            }

            if (children.Count == 0) {
                writer.WriteLine("{0}{2} ({1})", currentIndent, DisplayType, Name);
            } else if (children.Count == 1) {
                writer.WriteLine("{0}{1} = {2}", currentIndent, Name, children.First());
            } else {
                writer.WriteLine("{0}{2} ({1})", currentIndent, DisplayType, Name);
                foreach (var c in children.Ordered()) {
                    writer.WriteLine(currentIndent + indent + c);
                }
            }
            exportChildren = null;
        }
    }
}
