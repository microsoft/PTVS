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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestAdapterTests.Mocks {
    class MockMessageLogger : IMessageLogger {
        private readonly TestContext _ctx;
        public readonly List<Tuple<TestMessageLevel, string>> Messages = new List<Tuple<TestMessageLevel, string>>();

        public MockMessageLogger(TestContext ctx) => _ctx = ctx;


        public void SendMessage(TestMessageLevel level, string message) {
            Messages.Add(new Tuple<TestMessageLevel, string>(level, message));
            _ctx?.WriteLine($"[{level}] {message}");
        }

        public IEnumerable<string> GetErrors() => Messages.Where(m => m.Item1 == TestMessageLevel.Error).Select(m => m.Item2);

        public IEnumerable<string> GetWarnings() => Messages.Where(m => m.Item1 == TestMessageLevel.Warning).Select(m => m.Item2);

        public IEnumerable<string> GetInfos() => Messages.Where(m => m.Item1 == TestMessageLevel.Informational).Select(m => m.Item2);
    }
}
