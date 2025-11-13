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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.IO; // added for temp directory/file creation
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using TestAdapterTests.Mocks;

namespace TestAdapterTests {
    [TestClass]
    public class ProjectInfoTests {
        /// <summary>
        /// Recreate collection was modified while iterating exception
        /// System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
        /// Test shouldn't throw. Verifies concurrent clear/build does not break enumeration and containers are added.
        /// </summary>
        /// <returns></returns>
        [TestMethod, Priority(0)]
        public async Task TestBuildingAndClearingProjectMapConcurrently() {
            ITestContainerDiscoverer dummyDiscoverer = null;
            var projectMap = new ConcurrentDictionary<string, ProjectInfo>();
            var projectName = "dummyName";
            PythonProject dummyProject = new MockPythonProject("dummyHome", projectName);

            // Simulate rebuild workspace
            projectMap[projectName] = new ProjectInfo(dummyProject);

            // Create a root temp directory so AddTestContainer will succeed (it requires Directory.Exists(path))
            string tempRoot = Path.Combine(Path.GetTempPath(), "PTVS_ProjectInfoTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            var rebuildTasks = Enumerable.Range(1, 10)
                .Select(i => Task.Run(async () => {
                    projectMap.Clear();
                    var info = new ProjectInfo(dummyProject);
                    projectMap[projectName] = info;
                    foreach (int j in Enumerable.Range(1, 1000)) {
                        // Use directory paths instead of fake .py files so Directory.Exists(path) returns true
                        var dirPath = Path.Combine(tempRoot, j.ToString());
                        Directory.CreateDirectory(dirPath);
                        info.AddTestContainer(dummyDiscoverer, dirPath);
                    }
                    await Task.Delay(100);
                })
            );

            // Simulate Get TestContainers concurrently
            var iterateTasks = Enumerable.Range(1, 100)
                .Select(i => Task.Run(async () => {
                    var items = projectMap.Values.SelectMany(p => p.GetAllContainers()).ToList();
                    await Task.Delay(10);
                })
            );

            await Task.WhenAll(rebuildTasks.Concat(iterateTasks));

            // Verify last built project has all containers
            Assert.AreEqual(1000, projectMap[projectName].GetAllContainers().Count());
        }
    }
}