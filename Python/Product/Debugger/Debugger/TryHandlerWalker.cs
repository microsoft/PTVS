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

namespace Microsoft.PythonTools.Debugger
{
    /// <summary>
    /// Extracts a flat list of all the sections of code protected by exception
    /// handlers.
    /// </summary>
    class TryHandlerWalker : PythonWalker
    {
        private readonly List<TryStatement> _statements;

        public TryHandlerWalker()
        {
            _statements = new List<TryStatement>();
        }

        public ICollection<TryStatement> Statements
        {
            get
            {
                return _statements;
            }
        }

        public override bool Walk(TryStatement node)
        {
            _statements.Add(node);
            return base.Walk(node);
        }
    }
}
