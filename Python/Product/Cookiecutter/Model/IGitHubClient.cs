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
    interface IGitHubClient
    {
        Task<GitHubRepoSearchResult> SearchRepositoriesAsync(string requestUrl);
        Task<GitHubRepoSearchResult> StartSearchRepositoriesAsync(string[] terms);
        Task<GitHubRepoSearchItem> GetRepositoryDetails(string owner, string name);
        Task<bool> FileExistsAsync(GitHubRepoSearchItem repo, string filePath);
    }

    struct GitHubRepoSearchResult
    {
        [JsonProperty("total_count")]
        public int TotalCount;

        [JsonProperty("incomplete_results")]
        public bool IncompleteResults;

        [JsonProperty("items")]
        public GitHubRepoSearchItem[] Items;

        public GitHubPaginationLinks Links;
    }

    struct GitHubRepoSearchItem
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("full_name")]
        public string FullName;

        [JsonProperty("html_url")]
        public string HtmlUrl;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("fork")]
        public bool IsFork;

        [JsonProperty("url")]
        public string Url;

        [JsonProperty("owner")]
        public GitHubRepoOwner Owner;
    }

    struct GitHubRepoOwner
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("html_url")]
        public string HtmlUrl;

        [JsonProperty("avatar_url")]
        public string AvatarUrl;
    }

    struct GitHubPaginationLinks
    {
        public string Next;
        public string Prev;
        public string First;
        public string Last;
    }
}
