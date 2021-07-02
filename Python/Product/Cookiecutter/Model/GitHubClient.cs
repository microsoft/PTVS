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

namespace Microsoft.CookiecutterTools.Model
{
    class GitHubClient : IGitHubClient
    {
        private const string UserAgent = "PythonToolsForVisualStudio/" + AssemblyVersionInfo.Version;

        // throws WebException (for example, with 403 forbidden) and JsonException
        public async Task<GitHubRepoSearchResult> SearchRepositoriesAsync(string requestUrl)
        {
            if (requestUrl == null)
            {
                throw new ArgumentNullException(nameof(requestUrl));
            }

            var wc = CreateClient();
            var json = await wc.DownloadStringTaskAsync(requestUrl);
            var link = wc.ResponseHeaders["Link"];
            var repoSearchResult = JsonConvert.DeserializeObject<GitHubRepoSearchResult>(json);
            if (link != null)
            {
                repoSearchResult.Links = ParseLinkHeader(link);
            }
            return repoSearchResult;
        }

        public async Task<GitHubRepoSearchResult> StartSearchRepositoriesAsync(string[] terms)
        {
            if (terms == null)
            {
                throw new ArgumentNullException(nameof(terms));
            }

            var q = string.Join("+", terms);
            var queryUrl = Invariant($"https://api.github.com/search/repositories?q={q}&sort=stars&order=desc&fork=false");
            return await SearchRepositoriesAsync(queryUrl);
        }

        public async Task<GitHubRepoSearchItem> GetRepositoryDetails(string owner, string name)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var wc = CreateClient();
            var queryUrl = Invariant($"https://api.github.com/repos/{owner}/{name}");
            var json = await wc.DownloadStringTaskAsync(queryUrl);
            return JsonConvert.DeserializeObject<GitHubRepoSearchItem>(json);
        }

        public async Task<bool> FileExistsAsync(GitHubRepoSearchItem repo, string filePath)
        {
            var wc = new WebClient();
            var url = Invariant($"https://raw.githubusercontent.com/{repo.FullName}/master/{filePath}");
            try
            {
                var json = await wc.DownloadDataTaskAsync(url);
                return true;
            }
            catch (WebException)
            {
                return false;
            }
        }

        private static WebClient CreateClient()
        {
            var wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            wc.Headers.Add(HttpRequestHeader.UserAgent, UserAgent);
            return wc;
        }

        private static GitHubPaginationLinks ParseLinkHeader(string val)
        {
            var links = new GitHubPaginationLinks();

            foreach (var link in val.Split(','))
            {
                var parts = link.Split(';');
                var rel = parts[1].Trim();
                var url = parts[0].Trim().TrimStart('<').TrimEnd('>');
                switch (rel)
                {
                    case "rel=\"next\"":
                        links.Next = url;
                        break;
                    case "rel=\"prev\"":
                        links.Prev = url;
                        break;
                    case "rel=\"first\"":
                        links.First = url;
                        break;
                    case "rel=\"last\"":
                        links.Last = url;
                        break;
                }
            }

            return links;
        }
    }
}
