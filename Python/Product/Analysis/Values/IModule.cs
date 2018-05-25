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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    interface IModule {
        IModule GetChildPackage(IModuleContext context, string name);
        IEnumerable<KeyValuePair<string, AnalysisValue>> GetChildrenPackages(IModuleContext context);

        void SpecializeFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis);

        IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext context, GetMemberOptions options = GetMemberOptions.None);
        IEnumerable<string> GetModuleMemberNames(IModuleContext context);
        IAnalysisSet GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef = true, InterpreterScope linkedScope = null, string linkedName = null);
        void Imported(AnalysisUnit unit);
        string Description { get; }
        string Documentation { get; }
    }
}
