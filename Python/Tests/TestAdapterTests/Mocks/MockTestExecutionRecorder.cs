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

namespace TestAdapterTests.Mocks
{
	class MockTestExecutionRecorder : IFrameworkHandle
	{
		public readonly List<TestResult> Results = new List<TestResult>();
		public readonly List<string> Messages = new List<string>();
		public readonly List<AttachmentSet> Attachments = new List<AttachmentSet>();

		public bool EnableShutdownAfterTestRun
		{
			get
			{
				return false;
			}
			set
			{
			}
		}

		public int LaunchProcessWithDebuggerAttached(string filePath, string workingDirectory, string arguments, IDictionary<string, string> environmentVariables)
		{
			return 0;
		}

		public void RecordResult(TestResult result)
		{
			this.Results.Add(result);
		}

		public void RecordAttachments(IList<AttachmentSet> attachmentSets)
		{
			Attachments.AddRange(attachmentSets);
		}

		public void RecordEnd(TestCase testCase, TestOutcome outcome)
		{
		}

		public void RecordStart(TestCase testCase)
		{
		}

		public void SendMessage(TestMessageLevel testMessageLevel, string message)
		{
			Messages.Add(string.Format("{0}:{1}", testMessageLevel, message));
		}
	}
}
