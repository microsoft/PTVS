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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Repl {
    interface IPythonReplIntellisense {
        bool LiveCompletionsOnly { get; }
        IEnumerable<KeyValuePair<string, bool>> GetAvailableScopesAndKind();
        CompletionResult[] GetMemberNames(string text);
        OverloadDoc[] GetSignatureDocumentation(string text);
        VsProjectAnalyzer ReplAnalyzer {
            get;
        }
    }
}
