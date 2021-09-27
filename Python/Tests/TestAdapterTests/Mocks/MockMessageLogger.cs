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
    class MockMessageLogger : IMessageLogger
    {
        public readonly List<Tuple<TestMessageLevel, string>> Messages = new List<Tuple<TestMessageLevel, string>>();

        public void SendMessage(TestMessageLevel testMessageLevel, string message)
        {
            Messages.Add(new Tuple<TestMessageLevel, string>(testMessageLevel, message));
        }

        public IEnumerable<string> GetErrors() => Messages.Where(m => m.Item1 == TestMessageLevel.Error).Select(m => m.Item2);

        public IEnumerable<string> GetWarnings() => Messages.Where(m => m.Item1 == TestMessageLevel.Warning).Select(m => m.Item2);

        public IEnumerable<string> GetInfos() => Messages.Where(m => m.Item1 == TestMessageLevel.Informational).Select(m => m.Item2);
    }
}
