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
    class GitHubTemplateSource : ITemplateSource
    {
        private readonly IGitHubClient _client;
        private const string TemplateDefinitionFileName = "cookiecutter.json";

        public GitHubTemplateSource(IGitHubClient client)
        {
            _client = client;
        }

        public async Task<TemplateEnumerationResult> GetTemplatesAsync(string filter, string continuationToken, CancellationToken cancellationToken)
        {
            var terms = new List<string>();
            terms.Add("cookiecutter");

            var keywords = SearchUtils.ParseKeywords(filter);
            if (keywords != null && keywords.Length > 0)
            {
                terms.AddRange(keywords);
            }

            var templates = new List<Template>();

            try
            {
                GitHubRepoSearchResult result;
                if (continuationToken == null)
                {
                    result = await _client.StartSearchRepositoriesAsync(terms.ToArray());
                }
                else
                {
                    result = await _client.SearchRepositoriesAsync(continuationToken);
                }

                foreach (var repo in result.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await _client.FileExistsAsync(repo, TemplateDefinitionFileName))
                    {
                        var template = new Template();
                        template.RemoteUrl = repo.HtmlUrl;
                        template.Name = repo.FullName;
                        template.Description = repo.Description;
                        template.AvatarUrl = repo.Owner.AvatarUrl;
                        template.OwnerUrl = repo.Owner.HtmlUrl;
                        templates.Add(template);
                    }
                }

                return new TemplateEnumerationResult(templates, result.Links.Next);
            }
            catch (WebException ex)
            {
                throw new TemplateEnumerationException(Strings.GitHubSearchError, ex);
            }
            catch (JsonException ex)
            {
                throw new TemplateEnumerationException(Strings.GitHubSearchError, ex);
            }
        }

        public void InvalidateCache()
        {
        }
    }
}
