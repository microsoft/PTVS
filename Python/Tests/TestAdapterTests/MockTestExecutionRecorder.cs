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
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace TestAdapterTests {
    class MockTestExecutionRecorder : IFrameworkHandle {
        public readonly List<TestResult> Results = new List<TestResult>();

        public bool EnableShutdownAfterTestRun {
            get {
                return false;
            }
            set {
            }
        }

        public int LaunchProcessWithDebuggerAttached(string filePath, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables) {
            return 0;
        }

        public void RecordResult(TestResult result) {
            this.Results.Add(result);
        }

        public void RecordAttachments(IList<AttachmentSet> attachmentSets) {
        }

        public void RecordEnd(TestCase testCase, TestOutcome outcome) {
        }

        public void RecordStart(TestCase testCase) {
        }

        public void SendMessage(TestMessageLevel testMessageLevel, string message) {
        }
    }
}
