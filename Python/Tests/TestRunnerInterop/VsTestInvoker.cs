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

namespace TestRunnerInterop
{
    public sealed class VsTestInvoker
    {
        private readonly string _container, _className;

        public VsTestInvoker(
            VsTestContext context,
            string container,
            string className
        )
        {
            Context = context;

            _container = container;
            _className = className;
        }

        public VsTestContext Context { get; }

        public void RunTest(string testName, params object[] arguments) => RunTest(Context.DefaultTimeout, testName, arguments);

        public void RunTest(TimeSpan timeout, string testName, params object[] arguments)
        {
            Context.RunTest(_container, $"{_className}.{testName}", timeout, arguments);
        }
    }
}
