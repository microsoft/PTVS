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
    class MockTemplateSource : ILocalTemplateSource
    {
        public Dictionary<Tuple<string, string>, Tuple<Template[], string>> Templates { get; } = new Dictionary<Tuple<string, string>, Tuple<Template[], string>>();
        public Dictionary<string, bool?> UpdatesAvailable { get; } = new Dictionary<string, bool?>();
        public List<string> Updated { get; } = new List<string>();
        public List<string> Added { get; } = new List<string>();
        public List<string> Deleted { get; } = new List<string>();

        public Task<TemplateEnumerationResult> GetTemplatesAsync(string filter, string continuationToken, CancellationToken cancellationToken)
        {
            Tuple<Template[], string> result;
            if (Templates.TryGetValue(Tuple.Create(filter, continuationToken), out result))
            {
                return Task.FromResult(new TemplateEnumerationResult(result.Item1, result.Item2));
            }
            return Task.FromResult(new TemplateEnumerationResult(new Template[0]));
        }

        public Task DeleteTemplateAsync(string repoPath)
        {
            Deleted.Add(repoPath);
            return Task.CompletedTask;
        }

        public Task UpdateTemplateAsync(string repoPath)
        {
            Updated.Add(repoPath);
            return Task.CompletedTask;
        }

        Task<bool?> ILocalTemplateSource.CheckForUpdateAsync(string repoPath)
        {
            bool? available;
            if (!UpdatesAvailable.TryGetValue(repoPath, out available))
            {
                available = false;
            }
            return Task.FromResult(available);
        }

        public Task AddTemplateAsync(string repoPath)
        {
            Added.Add(repoPath);
            return Task.CompletedTask;
        }
    }
}
