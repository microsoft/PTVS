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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Core.IO {
    public sealed class DirectoryInfoProxy : IDirectoryInfo {
        private readonly DirectoryInfo _directoryInfo;

        public DirectoryInfoProxy(string directoryPath) {
            _directoryInfo = new DirectoryInfo(directoryPath);
        }

        public DirectoryInfoProxy(DirectoryInfo directoryInfo) {
            _directoryInfo = directoryInfo;
        }

        public bool Exists => _directoryInfo.Exists;
        public string FullName => _directoryInfo.FullName;
        public FileAttributes Attributes => _directoryInfo.Attributes;
        public IDirectoryInfo Parent => _directoryInfo.Parent != null ? new DirectoryInfoProxy(_directoryInfo.Parent) : null;

        public void Delete() => _directoryInfo.Delete();

        public IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos() => _directoryInfo
                .EnumerateFileSystemInfos()
                .Select(CreateFileSystemInfoProxy);

        public IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(SearchOption searchOption) => _directoryInfo
            .EnumerateFileSystemInfos("*", searchOption)
            .Select(CreateFileSystemInfoProxy);

        public IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string searchPattern, SearchOption searchOption) => _directoryInfo
            .EnumerateFileSystemInfos(searchPattern, searchOption)
            .Select(CreateFileSystemInfoProxy);

        public IEnumerable<IFileSystemInfo> EnumerateFileSystemInfos(string[] includePatterns, string[] excludePatterns) {
            var matcher = GetMatcher(includePatterns, excludePatterns);
            var matchResult = SafeExecuteMatcher(matcher);
            return matchResult.Files.Select((filePatternMatch) => {
                var path = PathUtils.NormalizePath(Path.Combine(_directoryInfo.FullName, filePatternMatch.Path));
                return CreateFileSystemInfoProxy(new FileInfo(path));
            });
        }

        public bool Match(string path, string[] includePatterns = default, string[] excludePatterns = default) {
            var matcher = GetMatcher(includePatterns, excludePatterns);
            return matcher.Match(FullName, path).HasMatches;
        }

        private static Matcher GetMatcher(string[] includePatterns, string[] excludePatterns) {
            Matcher matcher = new Matcher();
            matcher.AddIncludePatterns(includePatterns.IsNullOrEmpty() ? new[] { "**/*" } : includePatterns);
            if (!excludePatterns.IsNullOrEmpty()) {
                matcher.AddExcludePatterns(excludePatterns);
            }
            return matcher;
        }

        private PatternMatchingResult SafeExecuteMatcher(Matcher matcher) {
            var directoryInfo = new DirectoryInfoWrapper(_directoryInfo);
            for (var retries = 5; retries > 0; retries--) {
                try {
                    return matcher.Execute(directoryInfo);
                } catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            }
            return new PatternMatchingResult(Enumerable.Empty<FilePatternMatch>());
        }

        private static IFileSystemInfo CreateFileSystemInfoProxy(FileSystemInfo fileSystemInfo)
            => fileSystemInfo is DirectoryInfo directoryInfo
                ? (IFileSystemInfo)new DirectoryInfoProxy(directoryInfo)
                : new FileInfoProxy((FileInfo)fileSystemInfo);
    }
}
