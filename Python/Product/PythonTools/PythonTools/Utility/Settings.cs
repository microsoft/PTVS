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

namespace Microsoft.PythonTools.Utility {
    internal static class UserSettings {
        public enum ValueSource {
            Global,
            Workspace,
            Project
        }

        public static string GetStringSetting(
            string settingName,
            string filePath,
            IServiceProvider site,
            IPythonWorkspaceContext workspace,
            out ValueSource source) {

            source = ValueSource.Global;
            string value = null;

            if (workspace != null) {
                // Try workspace file	
                value = workspace.GetStringProperty(settingName);
                if (value != null) {
                    source = ValueSource.Workspace;
                }
            } else {
                // Try project
                var project = site.GetProjectContainingFile(filePath);
                if (project != null) {
                    value = project.GetProjectProperty(settingName);
                    if (value != null) {
                        source = ValueSource.Project;
                    }
                }
            }

            return value;
        }
    }
}
