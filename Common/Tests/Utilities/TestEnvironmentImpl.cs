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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TestUtilities
{
    public class TestEnvironmentImpl
    {
        protected internal static TestEnvironmentImpl Instance { get; protected set; }

        public static void TestInitialize(int secondsTimeout = 10) => Instance?.BeforeTestRun(secondsTimeout);
        public static void TestCleanup() => Instance?.AfterTestRun();

        private readonly AsyncLocal<TaskObserver> _taskObserver = new AsyncLocal<TaskObserver>();
        private readonly AssemblyLoader _assemblyLoader = new AssemblyLoader();
        private readonly string _binPath = typeof(TestEnvironmentImpl).Assembly.GetAssemblyDirectory();

        public TestEnvironmentImpl AddAssemblyResolvePaths(params string[] paths)
        {
            _assemblyLoader.AddPaths(paths.Where(n => !string.IsNullOrEmpty(n)).ToArray());
            return this;
        }

        public TestEnvironmentImpl AddVsResolvePaths()
            => AddAssemblyResolvePaths(_binPath, VisualStudioPath.CommonExtensions, VisualStudioPath.PrivateAssemblies, VisualStudioPath.PublicAssemblies);

        public bool TryAddTaskToWait(Task task)
        {
            var taskObserver = _taskObserver.Value;
            if (taskObserver == null)
            {
                return false;
            }
            taskObserver.Add(task);
            return true;
        }

        private void BeforeTestRun(int secondsTimeout)
        {
            AssertListener.Initialize();
            if (_taskObserver.Value != null)
            {
                throw new InvalidOperationException("AsyncLocal<TaskObserver> reentrancy");
            }

            _taskObserver.Value = new TaskObserver(secondsTimeout);
        }

        private void AfterTestRun()
        {
            try
            {
                _taskObserver.Value?.WaitForObservedTask();
                AssertListener.ThrowUnhandled();
            }
            finally
            {
                _taskObserver.Value = null;
            }
        }
    }
}