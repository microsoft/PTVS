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
using System.Linq;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestWindow.Extensibility;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestMethodResolver))]
    class TestMethodResolver : ITestMethodResolver {
        private readonly IServiceProvider _serviceProvider;
        
        #region ITestMethodResolver Members

        [ImportingConstructor]
        public TestMethodResolver([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider,
            [Import]TestContainerDiscoverer discoverer) {
            _serviceProvider = serviceProvider;
        }

        public Uri ExecutorUri {
            get { return PythonConstants.ExecutorUri; }
        }

        [Obsolete]
        public string GetCurrentTest(string filePath, int line, int lineCharOffset) {

            var componentModel = _serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var testService = componentModel.GetService<ITestsService>();

            var tests = testService.GetTestsAsEnumerableAsync().GetAwaiter().GetResult().ToList();

            var testcase = tests
                .Where(t => String.Compare(t.Source, filePath, ignoreCase: true) == 0)
                .OrderByDescending(t => t.LineNumber)
                .Where(t => t.LineNumber <= line)
                .FirstOrDefault();

            if (testcase == null)
                return null;
            
            return testcase.FullyQualifiedName;
        }


        #endregion
    }
}
