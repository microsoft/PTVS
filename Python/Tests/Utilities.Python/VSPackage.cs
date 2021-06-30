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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

[assembly: ProvideCodeBase(AssemblyName = "TestUtilities", CodeBase = "TestUtilities.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "TestUtilities.UI", CodeBase = "TestUtilities.UI.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "TestUtilities.Python", CodeBase = "TestUtilities.Python.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "TestUtilities.Python.Analysis", CodeBase = "TestUtilities.Python.Analysis.dll", Version = AssemblyVersionInfo.StableVersion)]
[assembly: ProvideCodeBase(AssemblyName = "MockVsTests", CodeBase = "MockVsTests.dll", Version = AssemblyVersionInfo.StableVersion)]

namespace TestUtilities.Python {
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideAutomationObject(AutomationObject)]
    public sealed class VSPackage : Package {
        public const string AutomationObject = "Microsoft.PythonTools.Tests.TestUtilities";

        private readonly Lazy<object> _automationObject = new Lazy<object>(() =>
            new TestUtilitiesAutomationObject()
        );

        protected override object GetAutomationObject(string name) {
            if (name == AutomationObject) {
                return _automationObject.Value;
            }
            return base.GetAutomationObject(name);
        }
    }

    [ComVisible(true)]
    public class TestUtilitiesAutomationObject {
    }
}
