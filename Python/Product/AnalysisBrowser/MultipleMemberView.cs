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
