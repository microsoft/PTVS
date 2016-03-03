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
using System.Diagnostics;
using System.IO;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class DefaultPythonProject : IPythonProject {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _filePath;

        public DefaultPythonProject(IServiceProvider serviceProvider, string filePath) {
            Utilities.ArgumentNotNullOrEmpty("filePath", filePath);
            _filePath = filePath;
            _serviceProvider = serviceProvider;
        }

        private string FullPath {
            get {
                return Path.GetFullPath(_filePath);
            }
        }

        #region IPythonProject Members

        string IPythonProject.GetProperty(string name) {
            return null;
        }

        void IPythonProject.SetProperty(string name, string value) {
            Debug.Assert(false, "Unexpected DefaultPythonProject.SetProperty() call");
        }

        string IPythonProject.GetWorkingDirectory() {
            return Path.GetDirectoryName(FullPath);
        }

        string IPythonProject.GetStartupFile() {
            return FullPath;
        }

        string IPythonProject.ProjectDirectory {
            get {
                return Path.GetDirectoryName(_filePath);
            }
        }

        string IPythonProject.ProjectName {
            get {
                return Path.GetFileNameWithoutExtension(_filePath);
            }
        }

        IPythonInterpreterFactory IPythonProject.GetInterpreterFactory() {
            return _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>().DefaultInterpreter;
        }

        bool IPythonProject.Publish(PublishProjectOptions options) {
            Debug.Assert(false, "Unexpected DefaultPythonProject.Publish() call");
            return false;
        }

        string IPythonProject.GetUnevaluatedProperty(string name) {
            return null;
        }

        ProjectAnalyzer IPythonProject.GetProjectAnalyzer() {
            return _serviceProvider.GetPythonToolsService().DefaultAnalyzer;
        }

        public event System.EventHandler ProjectAnalyzerChanged {
            add {
            }
            remove {
            }
        }

        #endregion
    }
}
