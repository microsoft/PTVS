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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Repl;

namespace TestUtilities.Python {
    internal class ReplTestReplOptions : PythonReplEvaluatorOptions {
        private bool _enableAttach;

        public ReplTestReplOptions(bool enableAttach = true) {
            _enableAttach = enableAttach;
        }

        public override bool EnableAttach {
            get { return _enableAttach; }
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
