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
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplEvaluator = IInteractiveEngine;
#endif

    /// <summary>
    /// Provides the command for starting the Python REPL window.
    /// </summary>
    class OpenReplCommand : Command {
        private readonly int _cmdId;
        private readonly IPythonInterpreterFactory _factory;

        public OpenReplCommand(int cmdId, IPythonInterpreterFactory factory) {
            _cmdId = cmdId;
            _factory = factory;
        }

        public override void DoCommand(object sender, EventArgs e) {
            // _factory is never null, but if a specific factory or command line
            // is passed as an argument, use that instead.
            var factory = _factory;
            var oe = e as OleMenuCmdEventArgs;
            if (oe != null) {
                IPythonInterpreterFactory asFactory;
                string args;
                if ((asFactory = oe.InValue as IPythonInterpreterFactory) != null) {
                    factory = asFactory;
                } else if (!string.IsNullOrEmpty(args = oe.InValue as string)) {
                    string description;
                    var parse = PythonToolsPackage.GetGlobalService(typeof(SVsParseCommandLine)) as IVsParseCommandLine;
                    if (ErrorHandler.Succeeded(parse.ParseCommandTail(args, -1)) &&
                        ErrorHandler.Succeeded(parse.EvaluateSwitches("e,env,environment:")) &&
                        ErrorHandler.Succeeded(parse.GetSwitchValue(0, out description)) &&
                        !string.IsNullOrEmpty(description)
                    ) {
                        var service = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
                        asFactory = service.Interpreters.FirstOrDefault(
                            // Descriptions are localized strings, hence CCIC
                            f => description.Equals(f.Description, StringComparison.CurrentCultureIgnoreCase)
                        );
                        if (asFactory != null) {
                            factory = asFactory;
                        }
                    }
                }
            }

            // These commands are project-insensitive, so pass null for project.
            var window = (ToolWindowPane)ExecuteInReplCommand.EnsureReplWindow(factory, null);

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            ((IReplWindow)window).Focus();
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return QueryStatusMethod;
            }
        }

        private void QueryStatusMethod(object sender, EventArgs args) {
            var oleMenu = (OleMenuCommand)sender;

            oleMenu.ParametersDescription = "e,env,environment:";

            if (_factory == null) {
                oleMenu.Visible = false;
                oleMenu.Enabled = false;
                oleMenu.Supported = false;
            } else {
                oleMenu.Visible = true;
                oleMenu.Enabled = true;
                oleMenu.Supported = true;
                oleMenu.Text = Description;
            }
        }

        public string Description {
            get {
                return _factory.Description + " Interactive";
            }
        }
        
        public override int CommandId {
            get { return (int)_cmdId; }
        }
    }
}
