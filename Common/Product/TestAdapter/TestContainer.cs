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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudio.TestWindow.Extensibility.Model;

namespace Microsoft.VisualStudioTools.TestAdapter {
    internal class TestContainer : ITestContainer {
        private readonly DateTime _timeStamp;
        private readonly Architecture _architecture;

        public TestContainer(ITestContainerDiscoverer discoverer, string source, DateTime timeStamp, Architecture architecture) {
            Discoverer = discoverer;
            Source = source;
            _timeStamp = timeStamp;
            _architecture = architecture;
        }

        private TestContainer(TestContainer copy)
            : this(copy.Discoverer, copy.Source, copy._timeStamp, copy._architecture) { }

        public int CompareTo(ITestContainer other) {
            var container = other as TestContainer;
            if (container == null) {
                return -1;
            }

            var result = String.Compare(Source, container.Source, StringComparison.OrdinalIgnoreCase);
            if (result != 0) {
                return result;
            }

            return _timeStamp.CompareTo(container._timeStamp);
        }

        public IEnumerable<Guid> DebugEngines {
            get {
                // TODO: Create a debug engine that can be used to attach to the (managed) test executor
                // Mixed mode debugging is not strictly necessary, provided that
                // the first engine returned from this method can attach to a
                // managed executable. This may change in future versions of the
                // test framework, in which case we may be able to start
                // returning our own debugger and having it launch properly.
                yield return new Guid("{EC1375B7-E2CE-43E8-BF75-DC638DE1F1F9}");
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
            return new TestContainer(this);
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