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
using System.Threading;

namespace Microsoft.PythonTools.Analysis.Infrastructure {
    public static class TestEnvironment {
        private static ITestEnvironment _current;

        public static ITestEnvironment Current {
            get => _current;
            set {
                var oldValue = Interlocked.CompareExchange(ref _current, value, null);
                if (oldValue != null) {
                    throw new InvalidOperationException("Only one test environment can be set per app domain");
                }
            }
        }
    }
}