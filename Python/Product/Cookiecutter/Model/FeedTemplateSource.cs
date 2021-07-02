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
    class FeedTemplateSource : ITemplateSource
    {
        private readonly Uri _feedLocation;
        private List<Template> _cache;

        public FeedTemplateSource(Uri feedLocation)
        {
            _feedLocation = feedLocation;
        }

        public async Task<TemplateEnumerationResult> GetTemplatesAsync(string filter, string continuationToken, CancellationToken cancellationToken)
        {
            if (_cache == null)
            {
                await BuildCacheAsync();
            }

            var keywords = SearchUtils.ParseKeywords(filter);

            var templates = new List<Template>();
            foreach (var template in _cache)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (SearchUtils.SearchMatches(keywords, template))
                {
                    templates.Add(template.Clone());
                }
            }

            return new TemplateEnumerationResult(templates);
        }

        public void InvalidateCache()
        {
            _cache = null;
        }

        private async Task BuildCacheAsync()
        {
            _cache = new List<Template>();

            try
            {
                var client = new WebClient();
                var feed = await client.DownloadStringTaskAsync(_feedLocation);
                var feedUrls = feed.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var entry in feedUrls)
                {
                    var template = new Template()
                    {
                        RemoteUrl = entry,
                    };

                    string owner;
                    string name;
                    if (ParseUtils.ParseGitHubRepoOwnerAndName(entry, out owner, out name))
                    {
                        template.Name = owner + "/" + name;
                    }
                    else
                    {
                        template.Name = entry;
                    }

                    _cache.Add(template);
                }
            }
            catch (WebException ex)
            {
                throw new TemplateEnumerationException(Strings.FeedLoadError, ex);
            }
        }
    }
}
