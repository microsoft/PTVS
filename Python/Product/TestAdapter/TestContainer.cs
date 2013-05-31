/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudio.TestWindow.Extensibility.Model;

namespace Microsoft.PythonTools.TestAdapter {
    internal class TestContainer : ITestContainer {
        private readonly DateTime timeStamp;
        private readonly Architecture architecture;

        public TestContainer(ITestContainerDiscoverer discoverer, string source, DateTime timeStamp, Architecture architecture) {
            this.Discoverer = discoverer;
            this.Source = source;
            this.timeStamp = timeStamp;
            this.architecture = architecture;
        }

        private TestContainer(TestContainer copy)
            : this(copy.Discoverer, copy.Source, copy.timeStamp, copy.architecture) { }

        public int CompareTo(ITestContainer other) {
            var container = other as TestContainer;
            if (container == null) {
                return -1;
            }

            var result = String.Compare(this.Source, container.Source, StringComparison.OrdinalIgnoreCase);
            if (result != 0) {
                return result;
            }

            return this.timeStamp.CompareTo(container.timeStamp);
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
            return new TestContainer(this);
        }

        public string Source { get; private set; }

        public FrameworkVersion TargetFramework {
            get { return FrameworkVersion.None; }
        }

        public Architecture TargetPlatform {
            get { return architecture; }
        }

        public override string ToString() {
            return this.Source + ":" + this.Discoverer.ExecutorUri.ToString();
        }
    }
}
