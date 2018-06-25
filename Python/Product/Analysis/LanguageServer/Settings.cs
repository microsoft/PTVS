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

using System.Threading;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed class LanguageServerSettings {
        public class PythonAnalysisOptions {
            /// <summary>
            /// Paths to search when attempting to resolve module imports.
            /// </summary>
            public string[] searchPaths;
            /// <summary>
            /// Secondary paths to search when resolving modules. Not supported by all
            /// factories. In generaly, only source files will be discovered, and their
            /// contents will be merged with the initial module.
            /// </summary>
            public string[] typeStubSearchPaths;
        }
        public readonly PythonAnalysisOptions analysisOptions = new PythonAnalysisOptions();

        public class PythonDiagnosticOptions {
            public bool openFilesOnly;
        }
        public readonly PythonDiagnosticOptions diagnosticOptions = new PythonDiagnosticOptions();

        public class PythonCompletionOptions {
            public bool showAdvancedMembers;
        }
        public readonly PythonCompletionOptions completionOptions = new PythonCompletionOptions();
    }
}
