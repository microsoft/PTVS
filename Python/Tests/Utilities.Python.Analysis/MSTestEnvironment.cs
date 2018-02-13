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

using Common = Microsoft.PythonTools.Infrastructure;
using Analysis = Microsoft.PythonTools.Analysis.Infrastructure;

namespace TestUtilities.Python {
    public sealed class MSTestEnvironment : TestEnvironment, Common.ITestEnvironment, Analysis.ITestEnvironment {
        public static void Initialize() {
            var isntance = new MSTestEnvironment();
            Instance = isntance;
            Analysis.TestEnvironment.Current = isntance;
            Common.TestEnvironment.Current = isntance;
        }
    }
}