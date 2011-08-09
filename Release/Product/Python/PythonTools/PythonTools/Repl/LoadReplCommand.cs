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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.VisualStudio.Repl {
#if INTERACTIVE_WINDOW
    using IReplWindow = IInteractiveWindow;
    using IReplCommand = IInteractiveWindowCommand;
#endif

    [Export(typeof(IReplCommand))]
    class LoadReplCommand : IReplCommand {
        #region IReplCommand Members

        public Task<ExecutionResult> Execute(IReplWindow window, string arguments) {
            List<string> submissions = new List<string>();
            List<string> lines = new List<string>();

            // TODO: commmand/comment parsing - in multi-line strings - check if we have a complete statement before splitting?

            string commandPrefix = (string)window.GetOptionValue(ReplOptions.CommandPrefix);
            const string commentPrefix = "%%";
            string lineBreak = window.TextView.Options.GetNewLineCharacter();

            using (var stream = new StreamReader(arguments)) {
                string line;
                while ((line = stream.ReadLine()) != null) {
                    if (line.StartsWith(commentPrefix)) {
                        continue;
                    }

                    if (line.StartsWith(commandPrefix)) {
                        AddSubmission(submissions, lines, lineBreak);

                        submissions.Add(line);
                        lines.Clear();
                    } else {
                        lines.Add(line);
                    }
                }
            }
            AddSubmission(submissions, lines, lineBreak);

            window.Submit(submissions);
            return ExecutionResult.Succeeded;
        }

        private static void AddSubmission(List<string> submissions, List<string> lines, string lineBreak) {
            string submission = String.Join(lineBreak, lines);

            // skip empty submissions:
            if (submission.Length > 0) {
                submissions.Add(submission);
            }
        }

        public string Description {
            get { return "Loads commands from file and executes until complete"; }
        }

        public string Command {
            get { return "load"; }
        }

        public object ButtonContent {
            get {
                return null;
            }
        }

        #endregion
    }
}
