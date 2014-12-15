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

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project.Automation;
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
                        res = CommonUtils.GetAbsoluteFilePath(proj.GetWorkingDirectory(), res);
                    }
                }
                return res;
            }
        }

        [Browsable(false)]
        public string InterpreterId {
            get {
                var interpreter = ((PythonProjectNode)this.Node).Interpreters.ActiveInterpreter;
                return interpreter.IsRunnable() ? interpreter.Id.ToString() : null;
            }
        }

        [Browsable(false)]
        public string InterpreterDescription {
            get {
                var interpreter = ((PythonProjectNode)this.Node).Interpreters.ActiveInterpreter;
                return interpreter.IsRunnable() ? interpreter.Description : null;
            }
        }

        [Browsable(false)]
        public MSBuildProjectInterpreterFactoryProvider InterpreterFactoryProvider {
            get {
                return ((PythonProjectNode)this.Node).Interpreters;
            }
        }

        [Browsable(false)]
        public string InterpreterVersion {
            get {
                var interpreter = ((PythonProjectNode)this.Node).Interpreters.ActiveInterpreter;
                return interpreter.IsRunnable() ? interpreter.Configuration.Version.ToString() : null;
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
                switch(HierarchyNode.ProjectMgr.Site.GetUIThread().Invoke(() => Node.GetProjectProperty("TargetFrameworkVersion"))) {
                    case "v4.0":
                        return 0x40000;
                    case "v4.5":
                        return 0x40005;
                    default:
                        return 0x40105;
                }
            }
        }
    }
}
