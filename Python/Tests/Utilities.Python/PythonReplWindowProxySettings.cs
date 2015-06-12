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
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestUtilities.UI.Python {
    public sealed class PythonReplWindowProxySettings : ReplWindowProxySettings {
        public PythonReplWindowProxySettings() {
            SourceFileName = "stdin";
            IntFirstMember = "bit_length";
            RawInput = "raw_input";
            IPythonIntDocumentation = Python2IntDocumentation;
            Print42Output = "42";
            ImportError = "ImportError: No module named {0}";
        }

        public new PythonReplWindowProxySettings Clone() {
            return (PythonReplWindowProxySettings)MemberwiseClone();
        }

        public override void AssertValid() {
            Version.AssertInstalled();
        }

        public override VisualStudioApp CreateApp() {
            return new PythonVisualStudioApp();
        }

        public override ToolWindowPane ActivateInteractiveWindow(VisualStudioApp app, string executionMode) {
            string description = null;
            if (Version.IsCPython) {
                description = string.Format("{0} {1}",
                    Version.Isx64 ? CPythonInterpreterFactoryConstants.Description64 : CPythonInterpreterFactoryConstants.Description32,
                    Version.Version.ToVersion()
                );
            } else if (Version.IsIronPython) {
                description = string.Format("{0} {1}",
                    Version.Isx64 ? "IronPython 64-bit" : "IronPython",
                    Version.Version.ToVersion()
                );
            }
            Assert.IsNotNull(description, "Unknown interpreter");

            var automation = (IVsPython)app.Dte.GetObject("VsPython");
            var options = (IPythonOptions)automation;
            var replOptions = options.GetInteractiveOptions(description);
            Assert.IsNotNull(replOptions, "Could not find options for " + description);

            replOptions.InlinePrompts = InlinePrompts;
            replOptions.UseInterpreterPrompts = UseInterpreterPrompts;
            replOptions.PrimaryPrompt = PrimaryPrompt;
            replOptions.SecondaryPrompt = SecondaryPrompt;
            replOptions.EnableAttach = EnableAttach;

            var oldExecutionMode = replOptions.ExecutionMode;
            app.OnDispose(() => replOptions.ExecutionMode = oldExecutionMode);
            replOptions.ExecutionMode = executionMode;

            var oldAddNewLineAtEndOfFullyTypedWord = options.Intellisense.AddNewLineAtEndOfFullyTypedWord;
            app.OnDispose(() => options.Intellisense.AddNewLineAtEndOfFullyTypedWord = oldAddNewLineAtEndOfFullyTypedWord);
            options.Intellisense.AddNewLineAtEndOfFullyTypedWord = AddNewLineAtEndOfFullyTypedWord;

            bool success = false;
            for (int retries = 1; retries < 20; ++retries) {
                try {
                    app.ExecuteCommand("Python.Interactive", "/e:\"" + description + "\"");
                    success = true;
                    break;
                } catch (AggregateException) {
                }
                app.DismissAllDialogs();
                app.SetFocus();
                Thread.Sleep(retries * 100);
            }
            Assert.IsTrue(success, "Unable to open " + description + " through DTE");
            var interpreters = app.ComponentModel.GetService<IInterpreterOptionsService>();
            var replId = PythonReplEvaluatorProvider.GetReplId(
                interpreters.FindInterpreter(Version.Id, Version.Version.ToVersion())
            );

            var provider = app.ComponentModel.GetService<InteractiveWindowProvider>();
            return (ToolWindowPane)provider.FindReplWindow(replId);
        }

        public const string Python2IntDocumentation = @"Type:        int
String form: 42
Docstring:
int(x=0) -> int or long
int(x, base=10) -> int or long

Convert a number or string to an integer, or return 0 if no arguments
are given.  If x is floating point, the conversion truncates towards zero.
If x is outside the integer range, the function returns a long instead.

If x is not a number or if base is given, then x must be a string or
Unicode object representing an integer literal in the given base.  The
literal can be preceded by '+' or '-' and be surrounded by whitespace.
The base defaults to 10.  Valid bases are 0 and 2-36.  Base 0 means to
interpret the base from the string as an integer literal.
>>> int('0b100', base=0)
4";

        public const string Python3IntDocumentation = @"Type:        int
String form: 42
Docstring:
int(x=0) -> integer
int(x, base=10) -> integer

Convert a number or string to an integer, or return 0 if no arguments
are given.  If x is a number, return x.__int__().  For floating point
numbers, this truncates towards zero.

If x is not a number or if base is given, then x must be a string,
bytes, or bytearray instance representing an integer literal in the
given base.  The literal can be preceded by '+' or '-' and be surrounded
by whitespace.  The base defaults to 10.  Valid bases are 0 and 2-36.
Base 0 means to interpret the base from the string as an integer literal.
>>> int('0b100', base=0)
4";

        public PythonVersion Version { get; set; }

        public string SourceFileName { get; set; }

        public string IPythonIntDocumentation { get; set; }

        public string IntFirstMember { get; set; }

        public string RawInput { get; set; }

        public string Print42Output { get; set; }

        public bool KeyboardInterruptHasTracebackHeader { get; set; }

        public string ImportError { get; set; }

        public bool AddNewLineAtEndOfFullyTypedWord { get; set; }
    }
}
