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

using System;
using System.Threading;
using System.Threading.Tasks;
using Common = Microsoft.PythonTools.Infrastructure;
using Analysis = Microsoft.PythonTools.Analysis.Infrastructure;

namespace TestUtilities {
    public sealed class MSTestEnvironment : Common.ITestEnvironment, Analysis.ITestEnvironment {
        private static MSTestEnvironment _instance;

        public static void Initialize() {
            _instance = new MSTestEnvironment();
            Analysis.TestEnvironment.Current = _instance;
            Common.TestEnvironment.Current = _instance;
        }

        public static void TestInitialize(int secondsTimeout = 30) => _instance.BeforeTestRun(secondsTimeout);
        public static void TestCleanup() => _instance.AfterTestRun();

        private readonly AsyncLocal<TaskObserver> _taskObserver = new AsyncLocal<TaskObserver>();

        public bool TryAddTaskToWait(Task task) {
            var taskObserver = _taskObserver.Value;
            if (taskObserver == null) {
                return false;
            }
            taskObserver.Add(task);
            return true;
        }
        
        private void BeforeTestRun(int secondsTimeout) {
            if (_taskObserver.Value != null) {
                throw new InvalidOperationException("AsyncLocal<TaskObserver> reentrancy");
            }

            _taskObserver.Value = new TaskObserver(secondsTimeout);
        }

        private void AfterTestRun() {
            try {
                _taskObserver.Value?.WaitForObservedTask();
            } finally {
                _taskObserver.Value = null;
            }
        }
    }
}