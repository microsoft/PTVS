// Visual Studio Shared Project
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

using Microsoft.VisualStudio.Text.Editor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TestUtilities.Mocks
{
    class MockTextViewRoleSet : ITextViewRoleSet
    {
        private HashSet<string> _roles = new HashSet<string>();

        public bool Contains(string textViewRole) => _roles.Contains(textViewRole);
        public bool ContainsAll(IEnumerable<string> textViewRoles) => textViewRoles.All(r => _roles.Contains(r));
        public bool ContainsAny(IEnumerable<string> textViewRoles) => textViewRoles.Any(r => _roles.Contains(r));
        public IEnumerator<string> GetEnumerator() => _roles.GetEnumerator();
        public ITextViewRoleSet UnionWith(ITextViewRoleSet roleSet)
        {
            _roles.UnionWith(roleSet);
            return this;
        }
        IEnumerator IEnumerable.GetEnumerator() => _roles.GetEnumerator();
    }
}
