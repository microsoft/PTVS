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

using Microsoft.PythonTools.Debugger;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project
{
    /// <summary>
    /// Implements functionality of starting a project or a file with or without debugging.
    /// </summary>
    sealed class DefaultPythonLauncher : IProjectLauncher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly LaunchConfiguration _config;

        public DefaultPythonLauncher(IServiceProvider serviceProvider, LaunchConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _config = config;
        }

        public int LaunchProject(bool debug)
        {
            return Launch(_config, debug);
        }

        public int LaunchFile(string/*!*/ file, bool debug)
        {
            var config = _config.Clone();
            config.ScriptName = file;
            return Launch(config, debug);
        }

        private int Launch(LaunchConfiguration config, bool debug)
        {
            DebugLaunchHelper.RequireStartupFile(config);

            if (debug)
            {
                StartWithDebugger(config);
            }
            else
            {
                StartWithoutDebugger(config).Dispose();
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Default implementation of the "Start without Debugging" command.
        /// </summary>
        private Process StartWithoutDebugger(LaunchConfiguration config)
        {
            try
            {
                _serviceProvider.GetPythonToolsService().Logger?.LogEvent(Logging.PythonLogEvent.Launch, new Logging.LaunchInfo
                {
                    IsDebug = false,
                    Version = config.Interpreter?.Version.ToString() ?? ""
                });
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }

            return Process.Start(DebugLaunchHelper.CreateProcessStartInfo(_serviceProvider, config));
        }

        /// <summary>
        /// Default implementation of the "Start Debugging" command.
        /// </summary>
        private void StartWithDebugger(LaunchConfiguration config)
        {
            try
            {
                _serviceProvider.GetPythonToolsService().Logger?.LogEvent(Logging.PythonLogEvent.Launch, new Logging.LaunchInfo
                {
                    IsDebug = true,
                    Version = config.Interpreter?.Version.ToString() ?? ""
                });
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }

            // Historically, we would clear out config.InterpreterArguments at
            // this stage if doing mixed-mode debugging. However, there doesn't
            // seem to be any need to do this, so we now leave them alone.

            using (var dbgInfo = DebugLaunchHelper.CreateDebugTargetInfo(_serviceProvider, config))
            {
                dbgInfo.Launch();
            }
        }
    }
}
