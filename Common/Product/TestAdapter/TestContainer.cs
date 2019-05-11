// Visual Studio Shared Project
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
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudio.TestWindow.Extensibility.Model;

namespace Microsoft.VisualStudioTools.TestAdapter {
    internal class TestContainer : ITestContainer {
        private readonly int _version;
        private readonly Architecture _architecture;
        private readonly string _projectHome;
        private readonly TestCaseInfo[] _testCases;

        public TestContainer(ITestContainerDiscoverer discoverer, string source, string projectHome, int version, Architecture architecture, TestCaseInfo[] testCases) {
            Discoverer = discoverer;
            Source = source;
            _version = version;
            _projectHome = projectHome;
            _architecture = architecture;
            _testCases = testCases;
        }

        public TestCaseInfo[] TestCases {
            get {
                return _testCases;
            }
        }

        public int Version {
            get {
                return _version;
            }
        }

        public string Project {
            get {
                return _projectHome;
            }
        }

        public int CompareTo(ITestContainer other) {
            var container = other as TestContainer;
            if (container == null) {
                return -1;
            }

            var result = String.Compare(Source, container.Source, StringComparison.OrdinalIgnoreCase);
            if (result != 0) {
                return result;
            }

            return _version.CompareTo(container._version);
        }

        public IEnumerable<Guid> DebugEngines {
            get {
                // TODO: Create a debug engine that can be used to attach to the (managed) test executor
                // Mixed mode debugging is not strictly necessary, provided that
                // the first engine returned from this method can attach to a
                // managed executable. This may change in future versions of the
                // test framework, in which case we may be able to start
                // returning our own debugger and having it launch properly.
                yield break;
            }
        }

        public IDeploymentData DeployAppContainer() {
            return null;
        }

        public ITestContainerDiscoverer Discoverer { get; private set; }

        public bool IsAppContainerTestContainer {
            get { return false; }
        }

        public ITestContainer Snapshot() {
            return this;
        }

        public string Source { get; private set; }

        public FrameworkVersion TargetFramework {
            get { return FrameworkVersion.None; }
        }

        public Architecture TargetPlatform {
            get { return _architecture; }
        }

        public override string ToString() {
            return Source + ":" + Discoverer.ExecutorUri.ToString();
        }
    }
}