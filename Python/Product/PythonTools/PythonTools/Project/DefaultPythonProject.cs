/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Diagnostics;
using System.IO;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class DefaultPythonProject : IPythonProject {
        private readonly string _filePath;

        public DefaultPythonProject(string filePath) {
            Debug.Assert((filePath != null), "Unexpected null filePath passed to DefaultPythonProject.DefaultPythonProject()");
            _filePath = filePath;
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
            return PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>().DefaultInterpreter;
        }

        bool IPythonProject.Publish(PublishProjectOptions options) {
            Debug.Assert(false, "Unexpected DefaultPythonProject.Publish() call");
            return false;
        }

        string IPythonProject.GetUnevaluatedProperty(string name) {
            return null;
        }

        VsProjectAnalyzer IPythonProject.GetProjectAnalyzer() {
            return PythonToolsPackage.Instance.DefaultAnalyzer;
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
