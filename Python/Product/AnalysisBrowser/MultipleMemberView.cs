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
using System.Linq;
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
    }
}
