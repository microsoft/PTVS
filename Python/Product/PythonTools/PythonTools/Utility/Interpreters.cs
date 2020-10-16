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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Utility {
    internal static class Interpreters {
        /// <summary>
        /// Retrieves Python interpreter configuration for a given text document.
        /// </summary>
        public static InterpreterConfiguration GetInterpreterConfiguration(this ITextBuffer buffer, IServiceProvider sp) {
            var cm = sp.GetComponentModel();

            var wscp = cm.GetService<IPythonWorkspaceContextProvider>();
            var configuration = wscp.Workspace?.CurrentFactory?.Configuration;
            if (configuration != null) {
                return configuration;
            }

            var doc = buffer as ITextDocument;
            if (!string.IsNullOrEmpty(doc?.FilePath)) {
                var project = sp.GetProjectFromOpenFile(doc.FilePath) ?? sp.GetProjectContainingFile(doc.FilePath);
                configuration = project.ActiveInterpreter?.Configuration;
            }

            return configuration ?? cm.GetService<IInterpreterOptionsService>().DefaultInterpreter.Configuration;
        }
    }
}
