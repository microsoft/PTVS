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

namespace CookiecutterTests
{
    [TestClass]
    public class GitClientTests
    {
        private const string GitHubWindowsIncompatibleRepoUrl = "https://github.com/huguesv/cookiecutter-pyvanguard";

        [TestMethod]
        public async Task CloneWindowsIncompatibleRepo()
        {
            var client = GitClientProvider.Create(null, null);

            var outputParentFolder = TestData.GetTempPath();

            try
            {
                // Clone a repo that uses folders with invalid characters on Windows
                await client.CloneAsync(GitHubWindowsIncompatibleRepoUrl, outputParentFolder);
                Assert.Fail($"Failed to generate exception when cloning repository. You should manually check that cloning '{GitHubWindowsIncompatibleRepoUrl}' still fails on Windows.");
            }
            catch (ProcessException ex)
            {
                Assert.AreNotEqual(0, ex.Result.StandardErrorLines.Length);
            }

            // Make sure failed clone is cleaned up
            Assert.AreEqual(0, Directory.GetDirectories(outputParentFolder).Length);
        }

        [TestMethod]
        public async Task CloneNonExistentRepo()
        {
            var client = GitClientProvider.Create(null, null);

            var outputParentFolder = TestData.GetTempPath();

            try
            {
                await client.CloneAsync("https://github.com/Microsoft/non-existent-repo", outputParentFolder);
                Assert.Fail($"Failed to generate exception when cloning non-existent repository.");
            }
            catch (ProcessException ex)
            {
                Assert.AreNotEqual(0, ex.Result.StandardErrorLines.Length);
            }

            // Make sure failed clone is cleaned up
            Assert.AreEqual(0, Directory.GetDirectories(outputParentFolder).Length);
        }
    }
}
