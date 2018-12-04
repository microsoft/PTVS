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
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Projects {
    public abstract class ProjectAnalyzer {
        /// <summary>
        /// Registers the extension type with the analyzer. The extension must have a
        /// public default constructor, as it will be recreated in the out-of-process
        /// analyzer.
        /// </summary>
        public abstract Task RegisterExtensionAsync(Type extensionType);

        /// <summary>
        /// Sends a command to an analysis extension with the specified input and returns
        /// the result.
        /// </summary>
        /// <param name="extensionName">The name of the analysis extension, as attributed with
        /// AnalysisExtensionNameAttribute.</param>
        /// <param name="commandId">The command that the extension supports and will execute.</param>
        /// <param name="body">The input to the command.</param>
        /// <returns></returns>
        public abstract Task<string> SendExtensionCommandAsync(string extensionName, string commandId, string body);

        /// <summary>
        /// Raised when the analysis is complete for the specified file.
        /// </summary>
        public abstract event EventHandler<AnalysisCompleteEventArgs> AnalysisComplete;

        /// <summary>
        /// Gets the list of files which are being analyzed by this ProjectAnalyzer.
        /// </summary>
        public abstract IEnumerable<string> Files {
            get;
        }
    }
}
