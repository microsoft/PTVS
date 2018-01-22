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

using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Tracks a variable that came out of analysis.  Includes a location (file, line,
    /// column) as well as a variable type (definition, reference, or value).  
    /// 
    /// Used for find all references, goto def, etc...
    /// 
    /// You can get ahold of these by calling snapshot.AnalyzeExpression(...) which
    /// is an extension method defined in <see cref="Microsoft.PythonTools.Intellisense.PythonAnalysisExtensions"/>
    /// </summary>
    public sealed class AnalysisVariable {
        public AnalysisVariable(VariableType type, AnalysisLocation location, int version) {
            Location = location;
            Type = type;
            Version = version;
        }

        public AnalysisLocation Location { get; }

        public VariableType Type { get; }

        public int Version { get; }
    }
}
