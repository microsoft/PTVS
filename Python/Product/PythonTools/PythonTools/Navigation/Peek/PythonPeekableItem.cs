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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.PythonTools.Navigation.Peek {
    internal sealed class PythonPeekableItem : IPeekableItem {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly AnalysisVariable[] _variables;

        public PythonPeekableItem(IPeekResultFactory peekResultFactory, AnalysisVariable[] variables) {
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
            _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        }

        public string DisplayName => null; // Unused

        public IEnumerable<IPeekRelationship> Relationships =>
            new List<IPeekRelationship>() { PredefinedPeekRelationships.Definitions };

        public IPeekResultSource GetOrCreateResultSource(string relationshipName) {
            return new PythonPeekResultSource(_peekResultFactory, _variables);
        }
    }
}
