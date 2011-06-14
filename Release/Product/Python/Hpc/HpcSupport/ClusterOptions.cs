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
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Hpc {    
    class ClusterOptions {
        private readonly IPythonProject _project;
        private ClusterEnvironment _env;
        internal const string RunEnvironmentSetting = "ClusterRunEnvironment";
        internal const string PublishBeforeRunSetting = "ClusterPublishBeforeRun";
        internal const string WorkingDirSetting = "ClusterWorkingDir";
        internal const string MpiExecPathSetting = "ClusterMpiExecCommand";
        internal const string AppCommandSetting = "ClusterAppCommand";
        internal const string AppArgumentsSetting = "ClusterAppArguments";
        internal const string DeploymentDirSetting = "ClusterDeploymentDir";
        internal const string TargetPlatformSetting = "ClusterTargetPlatform";

        public ClusterOptions(IPythonProject project) {
            _project = project;            
        }

        [DisplayName("Run Environment"),         
        Browsable(true), 
        Description("String that includes the head node, number of processes, and the allocation of processes to machines, if specified.")]
        public ClusterEnvironment RunEnvironment {
            get { return _env; }
            // updated from property grid
            set { 
                _env = value;
                if (_env.HeadNode != "localhost") {
                    if (String.IsNullOrWhiteSpace(DeploymentDirectory)) {
                        // automatically set the deployment dir to the spool dir on the server
                        DeploymentDirectory = "\\\\" + _env.HeadNode + "\\CcpSpoolDir\\$(UserName)\\$(Name)";
                    }
                } else {
                    DeploymentDirectory = "";
                }
            }
        }

        /// <summary>
        /// Called to initially set the environment
        /// </summary>
        /// <param name="env"></param>
        internal void LoadRunEnvironment(ClusterEnvironment env) {
            _env = env;
        }

        [DisplayName("Target platform"), Browsable(true), Description("Selects the target platform (X86 or X64).")]
        public Platform TargetPlatform {
            get;
            set;
        }


        [DisplayName("Publish before Run"),  Browsable(true),  Description("True if the project should be published (to location in publish settings) before run.")]
        public bool PublishBeforeRun {
            get; set;
        }

        [DisplayName("Python Interpreter"), Browsable(true), Description("Path to the python interpreter on the cluster nodes.")]
        public string PythonInterpreter {
            get;
            set;
        }

        [DisplayName("Application Arguments"), Browsable(true), Description("Extra arguments to be passed to the Python script.")]
        public string ApplicationArguments {
            get;
            set;
        }

        [DisplayName("Interpreter Arguments"), Browsable(true), Description("Extra arguments to be passed to the interpreter such as -O.")]
        public string InterpreterArguments {
            get;
            set;
        }

        [DisplayName("Deployment Directory"), Browsable(true), Description("Path to deployed files accessible on the cluster.  Usually empty, this overrides the publishing path when publishing to something other than a file share.  For example if publishing via FTP this is a path which is accessible by all of the nodes of the cluster locally.")]
        public string DeploymentDirectory {
            get;
            set;
        }

        [DisplayName("Working Directory"), Browsable(true), Description("Working directory used by each process.  If not specified the default is a directory in %TEMP%.")]
        public string WorkingDir {
            get;
            set;
        }

        [DisplayName("MPIExec Command"), Browsable(true), Description("Path to mpiexec.exe on the cluster nodes for starting the program under MPI.")]
        public string MpiExecPath {
            get;
            set;
        }
    }
}
