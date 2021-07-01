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

namespace Microsoft.PythonTools.Repl
{
    [Export(typeof(IInteractiveWindowCommand))]
    [InteractiveWindowRole("Debug")]
    [ContentType(PythonCoreConstants.ContentType)]
    class DebugReplFrameDownCommand : IInteractiveWindowCommand
    {
        public Task<ExecutionResult> Execute(IInteractiveWindow window, string arguments)
        {
            var eval = window.GetPythonDebugReplEvaluator();
            if (eval != null)
            {
                eval.FrameDown();
            }
            return ExecutionResult.Succeeded;
        }

        public string Description
        {
            get { return Strings.DebugReplFrameDownCommandDescription; }
        }

        public string Command
        {
            get { return "down"; }
        }

        public IEnumerable<ClassificationSpan> ClassifyArguments(ITextSnapshot snapshot, Span argumentsSpan, Span spanToClassify)
        {
            yield break;
        }

        public string CommandLine
        {
            get
            {
                return "";
            }
        }

        public IEnumerable<string> DetailedDescription
        {
            get
            {
                yield return Description;
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ParametersDescription
        {
            get
            {
                yield break;
            }
        }

        public IEnumerable<string> Names
        {
            get
            {
                yield return Command;
                yield return "d";
            }
        }
    }
}
