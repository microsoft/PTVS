
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
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Pytest;
using Microsoft.PythonTools.TestAdapter.UnitTest;

namespace Microsoft.PythonTools.TestAdapter.Services {
    internal class DiscovererFactory {

        public static IPythonTestDiscoverer GetDiscoverer(PythonProjectSettings settings) {
            switch (settings.TestFramework) {
                case TestFrameworkType.Pytest:
                    return new Pytest.PytestTestDiscoverer(settings);
                case TestFrameworkType.UnitTest:
                    return new UnitTestTestDiscoverer(settings);
                case TestFrameworkType.None:
                default:
                    throw new NotImplementedException($"CreateDiscoveryService TestFrameworkType:{settings.TestFramework.ToString()} not supported");
            }
        }
    }
}
