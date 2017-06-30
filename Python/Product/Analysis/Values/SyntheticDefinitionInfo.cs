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

namespace Microsoft.PythonTools.Analysis.Values {
    class SyntheticDefinitionInfo : AnalysisValue {
        public SyntheticDefinitionInfo(
            string name,
            string doc,
            IEnumerable<LocationInfo> locations
        ) {
            Name = name;
            Documentation = doc;
            Locations = locations?.ToArray() ?? Enumerable.Empty<LocationInfo>();
        }

        public override string Name { get; }
        public override string Documentation { get; }
        public override IEnumerable<LocationInfo> Locations { get; }
    }
}
