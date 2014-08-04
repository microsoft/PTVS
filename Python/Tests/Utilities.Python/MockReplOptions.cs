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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Repl;

namespace TestUtilities.Python {
    internal class ReplTestReplOptions : PythonReplEvaluatorOptions {
        public override bool EnableAttach {
            get { return true; }
        }

        public override string InterpreterOptions {
            get { return ""; }
        }

        public override string WorkingDirectory {
            get { return ""; }
        }

        public override IDictionary<string, string> EnvironmentVariables {
            get { return null; }
        }

        public override string StartupScript {
            get { return null; }
        }

        public override string SearchPaths {
            get { return ""; }
        }

        public override string InterpreterArguments {
            get { return ""; }
        }

        public override VsProjectAnalyzer ProjectAnalyzer {
            get { return null; }
        }

        public override bool UseInterpreterPrompts {
            get { return true; }
        }

        public override string ExecutionMode {
            get { return ""; }
        }

        public override bool InlinePrompts {
            get { return false; }
        }

        public override bool ReplSmartHistory {
            get { return false; }
        }

        public override bool LiveCompletionsOnly {
            get { return false; }
        }

        public override string PrimaryPrompt {
            get { return ">>>"; }
        }

        public override string SecondaryPrompt {
            get { return "..."; }
        }
    }
}
