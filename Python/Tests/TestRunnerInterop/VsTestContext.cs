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
using System.IO;

namespace TestRunnerInterop {
    public sealed class VsTestContext : IDisposable {
        private readonly string _container, _className;
        private VsInstance _vs;

        public VsTestContext(
            string container,
            string className
        ) {
            _container = container;
            _className = className;
        }

        public void RunTest(string testName, params object[] arguments) {
            if (_vs == null) {
                throw new InvalidOperationException("TestInitialize was not called");
            }
            _vs.RunTest(_container, $"{_className}.{testName}", arguments);
        }

        public void Dispose() {
            if (_vs != null) {
                _vs.Dispose();
                _vs = null;
            }
        }

        public void TestInitialize(string deploymentDirectory) {
            if (_vs == null || !_vs.IsRunning) {
                _vs?.Dispose();
                _vs = new VsInstance();
                _vs.StartOrRestart(
                    @"C:\Program Files (x86)\Microsoft Visual Studio\Preview\Enterprise\Common7\IDE\devenv.exe",
                    "/rootSuffix Exp",
                    deploymentDirectory,
                    Path.Combine(deploymentDirectory, "Temp")
                );
            }
        }

        public void TestCleanup() {
            // TODO: Reset VS state, or close and restart
        }
    }
}
