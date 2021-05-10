
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
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;

namespace TestAdapterTests.Mocks {
    public class MockPythonProject : PythonProject {
        public MockPythonProject(string projectHome, string projectName) {
            ProjectHome = projectHome;
            ProjectName = projectName;
        }

        public override string ProjectHome { get; }

        public override string ProjectName { get; }

#pragma warning disable 67
        public override event EventHandler<PythonProjectPropertyChangedArgs> ProjectPropertyChanged;
        public override event EventHandler ActiveInterpreterChanged;
#pragma warning restore  67

        public override IPythonInterpreterFactory GetInterpreterFactory() {
            throw new NotImplementedException();
        }

        public override LaunchConfiguration GetLaunchConfigurationOrThrow() {
            throw new NotImplementedException();
        }

        public override string GetProperty(string name) {
            throw new NotImplementedException();
        }

        public override string GetUnevaluatedProperty(string name) {
            throw new NotImplementedException();
        }

        public override void SetProperty(string name, string value) {
            throw new NotImplementedException();
        }
    }
}
