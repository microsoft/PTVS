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

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudio.TestWindow.Extensibility.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.VisualStudioTools.TestAdapter
{
    internal class TestContainer : ITestContainer
    {
        private readonly DateTime _lastWriteTime;
        private readonly DateTime _cachedTime;
        private readonly bool _isWorkspace;

        public TestContainer(
            ITestContainerDiscoverer discoverer,
            string source,
            string projectHome,
            string projectName,
            Architecture architecture,
            bool isWorkspace
        )
        {
            Discoverer = discoverer;
            Source = source; // Make sure source matches discovery new TestCase source.
            Project = projectHome;
            ProjectName = projectName;
            TargetPlatform = architecture;
            _lastWriteTime = GetLastWriteTimeStamp();
            _cachedTime = DateTime.Now;
            _isWorkspace = isWorkspace;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="copy"></param>
        private TestContainer(TestContainer copy) : this(
                   copy.Discoverer,
                   copy.Source,
                   copy.Project,
                   copy.ProjectName,
                   copy.TargetPlatform,
                   copy._isWorkspace
        )
        {
            _lastWriteTime = copy._lastWriteTime;
            _cachedTime = copy._cachedTime;
        }

        /// <summary>
        /// Project path
        /// </summary>
        public string Project { get; private set; }

        public string ProjectName { get; private set; }

        public int CompareTo(ITestContainer other)
        {
            var container = other as TestContainer;
            if (container == null)
            {
                return -1;
            }

            if (_isWorkspace != container._isWorkspace)
            {
                return -1;
            }

            var result = String.Compare(Source, container.Source, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }

            result = String.Compare(this.Project, container.Project, StringComparison.Ordinal);
            if (result != 0)
            {
                return result;
            }

            result = String.Compare(this.ProjectName, container.ProjectName, StringComparison.Ordinal);
            if (result != 0)
            {
                return result;
            }

            result = _cachedTime.CompareTo(container._cachedTime);
            if (result != 0)
            {
                return result;
            }

            result = _lastWriteTime.CompareTo(container._lastWriteTime);
            return result;
        }

        public IEnumerable<Guid> DebugEngines
        {
            get
            {
                // TODO: Create a debug engine that can be used to attach to the (managed) test executor
                // Mixed mode debugging is not strictly necessary, provided that
                // the first engine returned from this method can attach to a
                // managed executable. This may change in future versions of the
                // test framework, in which case we may be able to start
                // returning our own debugger and having it launch properly.
                yield break;
            }
        }

        public IDeploymentData DeployAppContainer()
        {
            return null;
        }

        public ITestContainerDiscoverer Discoverer { get; private set; }

        public bool IsAppContainerTestContainer
        {
            get { return false; }
        }

        public ITestContainer Snapshot()
        {
            return new TestContainer(this);
        }

        public string Source { get; private set; }

        public FrameworkVersion TargetFramework => FrameworkVersion.None;

        public Architecture TargetPlatform { get; private set; }

        public override string ToString()
        {
            return Source + ":" + Discoverer.ExecutorUri.ToString();
        }

        private DateTime GetLastWriteTimeStamp()
        {
            if (!String.IsNullOrEmpty(this.Source) && File.Exists(this.Source))
            {
                return File.GetLastWriteTime(this.Source);
            }
            else
            {
                return DateTime.MinValue;
            }
        }
    }
}