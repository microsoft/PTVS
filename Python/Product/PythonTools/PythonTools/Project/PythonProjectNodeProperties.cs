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
                return this.Node.ProjectMgr.GetProjectProperty(CommonConstants.SearchPath, true);
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
                var res = this.Node.ProjectMgr.GetProjectProperty(CommonConstants.InterpreterPath, true);
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
                return ((PythonProjectNode)this.Node).GetInterpreterFactory().Id.ToString();
            }
        }

        [Browsable(false)]
        public string InterpreterVersion {
            get {
                return ((PythonProjectNode)this.Node).GetInterpreterFactory().Configuration.Version.ToString();
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
                return 0x40000;
            }
        }
    }
}
