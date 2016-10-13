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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Model {
    class LocalTemplateSource : ILocalTemplateSource {
        private string _installedFolderPath;
        private IGitClient _gitClient;
        private List<Template> _cache;

        public LocalTemplateSource(string installedFolderPath, IGitClient gitClient) {
            _installedFolderPath = installedFolderPath;
            _gitClient = gitClient;
        }

        public async Task<TemplateEnumerationResult> GetTemplatesAsync(string filter, string continuationToken, CancellationToken cancellationToken) {
            if (_cache == null) {
                await BuildCacheAsync();
            }

            var keywords = SearchUtils.ParseKeywords(filter);

            var templates = new List<Template>();
            foreach (var template in _cache) {
                cancellationToken.ThrowIfCancellationRequested();

                if (SearchUtils.SearchMatches(keywords, template)) {
                    templates.Add(template.Clone());
                }
            }

            return new TemplateEnumerationResult(templates);
        }

        public void InvalidateCache() {
            _cache = null;
        }

        public Task DeleteTemplateAsync(string repoPath) {
            ShellUtils.DeleteDirectory(repoPath);

            _cache.RemoveAll(t => t.LocalFolderPath == repoPath);

            return Task.CompletedTask;
        }

        public async Task<ProcessOutputResult> UpdateTemplateAsync(string repoPath) {
            var res = await _gitClient.MergeAsync(repoPath);

            var template = _cache.SingleOrDefault(t => t.LocalFolderPath == repoPath);
            if (template != null) {
                template.ClonedLastUpdate = await _gitClient.GetLastCommitDateAsync(template.LocalFolderPath);
            }

            return res;
        }

        public async Task<Tuple<bool?, ProcessOutputResult>> CheckForUpdateAsync(string repoPath) {
            if (_cache == null) {
                await BuildCacheAsync();
            }

            var template = _cache.SingleOrDefault(t => t.RemoteUrl == repoPath);
            if (template == null) {
                return null;
            }

            var res = await _gitClient.FetchAsync(template.LocalFolderPath);

            template.ClonedLastUpdate = await _gitClient.GetLastCommitDateAsync(template.LocalFolderPath);
            template.RemoteLastUpdate = await _gitClient.GetLastCommitDateAsync(template.LocalFolderPath, "origin/master");

            if (template.RemoteLastUpdate.HasValue && template.ClonedLastUpdate.HasValue) {
                var span = template.RemoteLastUpdate - template.ClonedLastUpdate;
            }

            return Tuple.Create(template.UpdateAvailable, res);
        }

        private async Task BuildCacheAsync() {
            _cache = new List<Template>();

            if (Directory.Exists(_installedFolderPath)) {
                foreach (var folder in PathUtils.EnumerateDirectories(_installedFolderPath, recurse: false, fullPaths: true)) {

                    var template = new Template() {
                        LocalFolderPath = folder,
                        Name = PathUtils.GetFileOrDirectoryName(folder),
                    };

                    await InitializeRemoteAsync(template);

                    _cache.Add(template);
                }
            }
        }

        private async Task InitializeRemoteAsync(Template template) {
            var origin = await _gitClient.GetRemoteOriginAsync(template.LocalFolderPath);
            if (origin != null) {
                template.RemoteUrl = origin;
            }
        }
    }
}
