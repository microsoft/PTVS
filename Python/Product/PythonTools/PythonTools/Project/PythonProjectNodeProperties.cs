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

using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using Microsoft.VisualStudioTools.Project.Automation;

namespace Microsoft.PythonTools.Project {
    [ComVisible(true)]

    [ClassInterface(ClassInterfaceType.AutoDual)]
    [Guid(CommonConstants.ProjectNodePropertiesGuid)]
    public class PythonProjectNodeProperties : CommonProjectNodeProperties {

        internal PythonProjectNodeProperties(PythonProjectNode node)
            : base(node) {
        }

        /// <summary>
        /// Returns/Sets the SearchPath project property
        /// </summary>
        [Browsable(false)]
        public string SearchPath {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(PythonConstants.SearchPathSetting, true);
            }
        }

        /// <summary>
        /// Gets the command line arguments for the project.
        /// </summary>
        [Browsable(false)]
        public string CommandLineArguments {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(CommonConstants.CommandLineArguments, true);
            }
            set {
                this.Node.ProjectMgr.SetProjectProperty(CommonConstants.CommandLineArguments, value);
            }
        }

        /// <summary>
        /// Gets the override for the interpreter path to used for launching the project.
        /// </summary>
        [Browsable(false)]
        public string InterpreterPath {
            get {
                var res = this.Node.ProjectMgr.GetProjectProperty(PythonConstants.InterpreterPathSetting, true);
                if (!string.IsNullOrEmpty(res)) {
                    var proj = Node.ProjectMgr as CommonProjectNode;
                    if (proj != null) {
                        res = PathUtils.GetAbsoluteFilePath(proj.GetWorkingDirectory(), res);
                    }
                }
                return res;
            }
        }

        [Browsable(false)]
        public string InterpreterId {
            get {
                var interpreter = ((PythonProjectNode)this.Node).ActiveInterpreter;
                return interpreter.IsRunnable() ? interpreter.Configuration.Id : null;
            }
        }

        [Browsable(false)]
        public string InterpreterDescription {
            get {
                var interpreter = ((PythonProjectNode)this.Node).ActiveInterpreter;
                return interpreter.IsRunnable() ? interpreter.Configuration.Description : null;
            }
        }

        [Browsable(false)]
        public string InterpreterVersion {
            get {
                var interpreter = ((PythonProjectNode)this.Node).ActiveInterpreter;
                return interpreter.IsRunnable() ? interpreter.Configuration.Version.ToString() : null;
            }
        }

        [Browsable(false)]
        public string Environment {
            get {
                return this.Node.ProjectMgr.GetProjectProperty(PythonConstants.EnvironmentSetting, true);
            }
        }

        [PropertyNameAttribute("WebApplication.AspNetDebugging")]
        [Browsable(false)]
        public bool AspNetDebugging {
            get {
                return true;
            }
        }

        [PropertyNameAttribute("WebApplication.NativeDebugging")]
        [Browsable(false)]
        public bool NativeDebugging {
            get {
                return false;
            }
        }

        [Browsable(false)]
        public uint TargetFramework {
            get {
                // Cloud Service projects inspect this value to determine which
                // OS to deploy.
                switch (HierarchyNode.ProjectMgr.Site.GetUIThread().Invoke(() => Node.GetProjectProperty("TargetFrameworkVersion"))) {
                    case "v4.0":
                        return 0x40000;
                    case "v4.5":
                        return 0x40005;
                    default:
                        return 0x40105;
                }
            }
        }

        [Browsable(false)]
        public override VSLangProj.prjOutputType OutputType {
            get {
                // This is probably not entirely true, but it helps us deal with
                // extensions like Azure Tools that try to figure out whether we
                // support WebForms.
                return VSLangProj.prjOutputType.prjOutputTypeExe;
            }
            set { }
        }
    }
}
