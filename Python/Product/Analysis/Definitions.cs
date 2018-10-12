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

// This is a temporary partial port of interfaces
using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis {
    public interface IScope {
        bool TryGetVariable(string name, out IVariableDefinition value);
    }

    public interface IClassScope: IScope {

    }
    
    public interface IVariableDefinition {
        bool AddReference(EncodedLocation location, IVersioned module);
        bool AddAssignment(EncodedLocation location, IVersioned entry);
    }
}

namespace Microsoft.PythonTools.Analysis.Values {
    public interface IBuiltinClassInfo {
        IBuiltinInstanceInfo Instance { get; }
    }

    public interface IBuiltinInstanceInfo: IAnalysisValue {
        IBuiltinClassInfo ClassInfo { get; }
    }

    public interface IAnalysisValue : IAnalysisSet {
        /// <summary>
        /// Returns an immutable set which contains just this AnalysisValue.
        /// Currently implemented as returning the AnalysisValue object directly which implements ISet{AnalysisValue}.
        /// </summary>
        IAnalysisSet SelfSet { get; }
    }

    public interface IClassInfo : IAnalysisValue {
        IClassScope Scope { get; }
    }

    public interface IInstanceInfo : IAnalysisValue {
        IClassInfo ClassInfo { get; }
        IReadOnlyDictionary<string, IVariableDefinition> InstanceAttributes { get; }
    }
}
