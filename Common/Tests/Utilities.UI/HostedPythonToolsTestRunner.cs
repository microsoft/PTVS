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

using System.Reflection;
using System.Runtime.InteropServices;
using TestRunnerInterop;

namespace TestUtilities.UI {
    [ComVisible(true)]
    public sealed class HostedPythonToolsTestResult : IVsHostedPythonToolsTestResult {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string Traceback { get; set; }
    }

    [ComVisible(true)]
    public sealed class HostedPythonToolsTestRunner : IVsHostedPythonToolsTest {
        private readonly Assembly _assembly;

        public HostedPythonToolsTestRunner(Assembly assembly) {
            _assembly = assembly;
        }

        public IVsHostedPythonToolsTestResult Execute(string name, object[] arguments) {
            var parts = name.Split(":".ToCharArray(), 2);
            if (parts.Length == 2) {
                var type = _assembly.GetType(parts[0]);
                if (type != null) {
                    var meth = type.GetMethod(parts[1], BindingFlags.Instance | BindingFlags.Public);
                    if (meth != null) {
                        // TODO: Invoke the test

                        return new HostedPythonToolsTestResult {
                            IsSuccess = true
                        };
                    }
                }
            }

            return new HostedPythonToolsTestResult {
                IsSuccess = false,
                Message = $"Failed to find test \"{name}\" in \"{_assembly.FullName}\""
            };
        }
    }
}
