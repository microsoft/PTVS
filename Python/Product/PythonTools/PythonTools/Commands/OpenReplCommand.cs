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
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for starting the Python REPL window.
    /// </summary>
    class OpenReplCommand : Command {
        private readonly IServiceProvider _serviceProvider;
        private readonly int _cmdId;

        public OpenReplCommand(IServiceProvider serviceProvider, int cmdId) {
            _serviceProvider = serviceProvider;
            _cmdId = cmdId;
        }

        public override void DoCommand(object sender, EventArgs e) {
            // Use the factory or command line passed as an argument.
            IPythonInterpreterFactory factory = null;
            InterpreterConfiguration config = null;
            var oe = e as OleMenuCmdEventArgs;
            if (oe != null) {
                string args;
                if ((factory = oe.InValue as IPythonInterpreterFactory) != null) {
                    config = factory.Configuration;
                } else if ((config = oe.InValue as InterpreterConfiguration) != null) {
                } else if (!string.IsNullOrEmpty(args = oe.InValue as string)) {
                    string description;
                    var parse = _serviceProvider.GetService(typeof(SVsParseCommandLine)) as IVsParseCommandLine;
                    if (ErrorHandler.Succeeded(parse.ParseCommandTail(args, -1)) &&
                        ErrorHandler.Succeeded(parse.EvaluateSwitches("e,env,environment:")) &&
                        ErrorHandler.Succeeded(parse.GetSwitchValue(0, out description)) &&
                        !string.IsNullOrEmpty(description)
                    ) {
                        var service = _serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
                        config = service.Configurations
                            .Where(PythonInterpreterFactoryExtensions.IsRunnable)
                            .FirstOrDefault(
                            // Descriptions are localized strings, hence CCIC
                            f => description.Equals(f.Description, StringComparison.CurrentCultureIgnoreCase)
                        );
                    }
                }
            }

            if (config == null) {
                var service = _serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
                var registry = _serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
                config = service.DefaultInterpreter?.Configuration ??
                    registry.Configurations
                    .Where(PythonInterpreterFactoryExtensions.IsRunnable)
                    .FirstOrDefault();
            }

            // This command is project-insensitive
            var provider = _serviceProvider.GetComponentModel()?.GetService<Repl.InteractiveWindowProvider>();
            try {
                provider?.OpenOrCreate(
                    config != null ? Repl.PythonReplEvaluatorProvider.GetEvaluatorId(config) : null
                );
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                throw new InvalidOperationException(Strings.ErrorOpeningInteractiveWindow.FormatUI(ex));
            }
        }

        public override EventHandler BeforeQueryStatus => QueryStatusMethod;

        private void QueryStatusMethod(object sender, EventArgs args) {
            var oleMenu = (OleMenuCommand)sender;
            oleMenu.ParametersDescription = "e,env,environment:";

            oleMenu.Visible = true;
            oleMenu.Enabled = true;
            oleMenu.Supported = true;
        }

        public override int CommandId {
            get { return _cmdId; }
        }
    }
}
