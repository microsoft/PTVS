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
    public class GitHubDiscoveryTests
    {
        //[TestMethod]
        public async Task Discover()
        {
            // Enable this test to create a list of templates currently available on GitHub
            // Note that this takes about 3 mins due to pauses between each query,
            // which are there to avoid 403 error.
            var templates = await GetAllGitHubTemplates();
            var urls = templates.Select(t => t.RemoteUrl).OrderBy(val => val);
            var owners = templates.
                Select(t => GetTemplateOwner(t)).
                Distinct().
                Where(val => !string.IsNullOrEmpty(val)).
                OrderBy(val => val);

            var folderPath = TestData.GetTempPath("GitHubDiscovery");
            File.WriteAllLines(Path.Combine(folderPath, "CookiecutterUrls.txt"), urls);
            File.WriteAllLines(Path.Combine(folderPath, "CookiecutterOwners.txt"), owners);
        }

        private static async Task<List<Template>> GetAllGitHubTemplates()
        {
            var source = new GitHubTemplateSource(new GitHubClient());
            string continuation = null;
            var templates = new List<Template>();
            do
            {
                var result = await source.GetTemplatesAsync("", continuation, CancellationToken.None);
                continuation = result.ContinuationToken;
                foreach (var template in result.Templates)
                {
                    Console.WriteLine(template.RemoteUrl);
                }
                templates.AddRange(result.Templates);
                Thread.Sleep(1000);
            } while (!string.IsNullOrEmpty(continuation));
            return templates;
        }

        private string GetTemplateOwner(Template template)
        {
            string owner;
            string name;
            if (ParseUtils.ParseGitHubRepoOwnerAndName(template.RemoteUrl, out owner, out name))
            {
                return owner;
            }
            else
            {
                return null;
            }
        }
    }
}
