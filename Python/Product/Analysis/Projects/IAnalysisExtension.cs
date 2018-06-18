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

namespace Microsoft.PythonTools.Projects {
    /// <summary>
    /// Provides an extension which registers against a given analyzer.
    /// </summary>
    public interface IAnalysisExtension {
        /// <summary>
        /// Called when the extension is registered for an analyzer.
        /// </summary>
        /// <param name="analyzer"></param>
        void Register(PythonAnalyzer analyzer);

        /// <summary>
        /// Handles an extension command.  The extension receives the command body and
        /// returns a response.
        /// </summary>
        string HandleCommand(string commandId, string body);
    }
}
