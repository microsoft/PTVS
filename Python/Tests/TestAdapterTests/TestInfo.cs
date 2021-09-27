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

namespace TestAdapterTests
{
    class TestInfo
    {
        public TestInfo(
            string displayName,
            string fullyQualifiedName,
            string filePath,
            int lineNumber,
            TestOutcome? outcome = null,
            TimeSpan? minDuration = null,
            string containedErrorMessage = null,
            string[] containedStdOut = null,
            StackFrame[] stackFrames = null,
            string pytestXmlClassName = null,
            string pytestExecPathSuffix = null)
        {
            DisplayName = displayName;
            FullyQualifiedName = fullyQualifiedName;
            FilePath = filePath;
            LineNumber = lineNumber;
            Outcome = outcome ?? TestOutcome.None;
            MinDuration = minDuration ?? TimeSpan.Zero;
            ContainedErrorMessage = containedErrorMessage;
            ContainedStdOut = containedStdOut;
            StackFrames = stackFrames;
            PytestXmlClassName = pytestXmlClassName;
            PytestExecPathSuffix = pytestExecPathSuffix;
        }

        public string DisplayName { get; }

        public string FullyQualifiedName { get; }

        public string FilePath { get; }

        public int LineNumber { get; }

        public TestOutcome Outcome { get; }

        public TimeSpan MinDuration { get; }

        public string ContainedErrorMessage { get; }

        public string[] ContainedStdOut { get; }

        public StackFrame[] StackFrames { get; }

        public string PytestXmlClassName { get; }

        public string PytestExecPathSuffix { get; }
    }
}
