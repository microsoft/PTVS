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

namespace Microsoft.PythonTools.Refactoring
{
    /// <summary>
    /// Encapsulates all of the possible knobs which can be flipped when extracting a method.
    /// </summary>
    class ExtractMethodRequest
    {
        private readonly string _name;
        private readonly string[] _parameters;
        private readonly ScopeWrapper _targetScope;

        public ExtractMethodRequest(ScopeWrapper targetScope, string name, string[] parameters)
        {
            _name = name;
            _parameters = parameters;
            _targetScope = targetScope;
        }

        /// <summary>
        /// The name of the new method which should be created
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
        }

        /// <summary>
        /// The variables which are consumed by the method but which should be passed in as parameters
        /// (versus closing over the variables)
        /// </summary>
        public string[] Parameters
        {
            get
            {
                return _parameters;
            }
        }

        /// <summary>
        /// The target scope to extract the method to.
        /// </summary>
        public ScopeWrapper TargetScope
        {
            get
            {
                return _targetScope;
            }
        }
    }
}
