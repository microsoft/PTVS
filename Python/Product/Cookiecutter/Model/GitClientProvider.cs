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

using System.IO;
using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Model {
    internal static class GitClientProvider {
        public static IGitClient Create(Redirector redirector, string commonIdeFolderPath) {
            string gitExeFilePath = null;

            // Try to locate Team Explorer's git.exe using the running instance ide folder
            if (!string.IsNullOrEmpty(commonIdeFolderPath)) {
                gitExeFilePath = GetTeamExplorerGitFilePathFromIdeFolderPath(commonIdeFolderPath);
            }

            // Just use git.exe, and it will work if it's in PATH
            // If it's not, the error will be output in redirector at time of use
            if (!File.Exists(gitExeFilePath)) {
                gitExeFilePath = GitExecutableName;
            }

            return new GitClient(gitExeFilePath, redirector);
        }

        private static string GitExecutableName {
            get {
                return "git.exe";
            }
        }

        private static string GetTeamExplorerGitFilePathFromIdeFolderPath(string ideFolderPath) {
            // git.exe is in a folder path with a symlink to the actual extension dir with random name
            var gitFolder = Path.Combine(ideFolderPath, @"CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\mingw32\bin");
            var finalGitFolder = PathUtils.GetFinalPathName(gitFolder);
            var gitExe = Path.Combine(finalGitFolder, GitExecutableName);
            return gitExe;
        }
    }
}
