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


namespace Microsoft.PythonTools.Analysis {
    class AnalysisVariable : IAnalysisVariable {
        private readonly LocationInfo _loc;
        private readonly VariableType _type;

        public AnalysisVariable(VariableType type, LocationInfo location) {
            _loc = location;
            _type = type;
        }

        #region IAnalysisVariable Members

        public LocationInfo Location {
            get { return _loc; }
        }

        public VariableType Type {
            get { return _type; }
        }

        #endregion
    }

}
