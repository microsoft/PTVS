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
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestMethodResolver))]
    class TestMethodResolver : ITestMethodResolver {
        private readonly IServiceProvider _serviceProvider;
        private readonly TestContainerDiscoverer _discoverer;

        #region ITestMethodResolver Members

        [ImportingConstructor]
        public TestMethodResolver([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider,
            [Import]TestContainerDiscoverer discoverer) {
            _serviceProvider = serviceProvider;
            _discoverer = discoverer;
        }

        public Uri ExecutorUri {
            get { return PythonConstants.ExecutorUri; }
        }

        public string GetCurrentTest(string filePath, int line, int lineCharOffset) {
            var rdt = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
            var pyProj = PythonProject.FromObject(VsProjectExtensions.PathToProject(filePath, rdt));
            if (pyProj != null) {
                var container = _discoverer.GetTestContainer(pyProj.ProjectHome, filePath);
                if (container != null) {
                    foreach (var testCase in container.TestCases) {
                        if (testCase.StartLine >= line && line <= testCase.EndLine) {
                            var moduleName = PathUtils.CreateFriendlyFilePath(pyProj.ProjectHome, testCase.Filename);
                            return moduleName + "::" + testCase.ClassName + "::" + testCase.MethodName;
                        }
                    }
                }
            }

            return null;
        }

       

        #endregion
    }
}
