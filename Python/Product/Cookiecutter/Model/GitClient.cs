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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.Win32;

namespace Microsoft.CookiecutterTools.Model {
    class GitClient : IGitClient {
        private string _gitExeFilePath;

        public GitClient(string gitExeFilePath) {
            _gitExeFilePath = gitExeFilePath;
        }

        public static string RecommendedGitFilePath {
            get {
                string gitExeFilePath = TeamExplorerGitFilePath;
                if (File.Exists(gitExeFilePath)) {
                    return gitExeFilePath;
                }

                try {
                    if (ExecutableOnPath(GitExecutableName)) {
                        return GitExecutableName;
                    }
                } catch (NotSupportedException) {
                }

                return null;
            }
        }

        private static string GitExecutableName {
            get {
                return "git.exe";
            }
        }

        private static string TeamExplorerGitFilePath {
            get {
                try {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"Software\\Microsoft\VisualStudio\SxS\VS7")) {
                        var installRoot = (string)key.GetValue(AssemblyVersionInfo.VSVersion);
                        if (installRoot != null) {
                            // git.exe is in a folder path with a symlink to the actual extension dir with random name
                            var gitFolder = Path.Combine(installRoot, @"Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd");
                            var finalGitFolder = PathUtils.GetFinalPathName(gitFolder);
                            var gitExe = Path.Combine(finalGitFolder, GitExecutableName);
                            return gitExe;
                        }
                    }
                } catch (Exception e) when (!e.IsCriticalException()) {
                }

                return null;
            }
        }

        public async Task<Tuple<string, ProcessOutputResult>> CloneAsync(string repoUrl, string targetParentFolderPath) {
            Directory.CreateDirectory(targetParentFolderPath);

            string localTemplateFolder = GetClonedFolder(repoUrl, targetParentFolderPath);

            if (Directory.Exists(localTemplateFolder)) {
                ShellUtils.DeleteDirectory(localTemplateFolder);
            }

            var arguments = new string[] { "clone", repoUrl };
            var output = ProcessOutput.Run(_gitExeFilePath, arguments, targetParentFolderPath, null, false, null);
            using (output) {
                await output;

                var r = new ProcessOutputResult() {
                    ExeFileName = _gitExeFilePath,
                    ExitCode = output.ExitCode,
                    StandardOutputLines = output.StandardOutputLines.ToArray(),
                    StandardErrorLines = output.StandardErrorLines.ToArray(),
                };

                if (r.ExitCode < 0) {
                    throw new ProcessException(r);
                }

                if (!Directory.Exists(localTemplateFolder)) {
                    throw new ProcessException(r);
                }

                return Tuple.Create(localTemplateFolder, r);
            }
        }

        public async Task<string> GetRemoteOriginAsync(string repoFolderPath) {
            var arguments = new string[] { "remote", "-v" };
            var output = ProcessOutput.Run(_gitExeFilePath, arguments, repoFolderPath, null, false, null);
            using (output) {
                await output;
                foreach (var remote in output.StandardOutputLines) {
                    string origin;
                    if (ParseOrigin(remote, out origin)) {
                        return origin;
                    }
                }
            }
            return null;
        }

        public async Task<DateTime?> GetLastCommitDateAsync(string repoFolderPath, string branch) {
            var arguments = new List<string>() { "log", "-1", "--date=iso" };
            if (!string.IsNullOrEmpty(branch)) {
                arguments.Add(branch);
            }

            var output = ProcessOutput.Run(_gitExeFilePath, arguments, repoFolderPath, null, false, null);
            using (output) {
                await output;
                foreach (var line in output.StandardOutputLines) {
                    DateTime? date;
                    if (ParseDate(line, out date)) {
                        return date;
                    }
                }
            }
            return null;
        }

        public async Task<ProcessOutputResult> FetchAsync(string repoFolderPath) {
            var arguments = new string[] { "fetch" };
            var output = ProcessOutput.Run(_gitExeFilePath, arguments, repoFolderPath, null, false, null);
            using (output) {
                await output;

                var r = new ProcessOutputResult() {
                    ExeFileName = _gitExeFilePath,
                    ExitCode = output.ExitCode,
                    StandardOutputLines = output.StandardOutputLines.ToArray(),
                    StandardErrorLines = output.StandardErrorLines.ToArray(),
                };

                if (r.ExitCode < 0) {
                    throw new ProcessException(r);
                }

                return r;
            }
        }

        public async Task<ProcessOutputResult> MergeAsync(string repoFolderPath) {
            var arguments = new string[] { "merge" };
            var output = ProcessOutput.Run(_gitExeFilePath, arguments, repoFolderPath, null, false, null);
            using (output) {
                await output;

                var r = new ProcessOutputResult() {
                    ExeFileName = _gitExeFilePath,
                    ExitCode = output.ExitCode,
                    StandardOutputLines = output.StandardOutputLines.ToArray(),
                    StandardErrorLines = output.StandardErrorLines.ToArray(),
                };

                if (r.ExitCode < 0) {
                    throw new ProcessException(r);
                }

                return r;
            }
        }

        private static string GetClonedFolder(string repoUrl, string targetParentFolderPath) {
            string name;
            if (!ParseRepoName(repoUrl, out name)) {
                throw new ArgumentOutOfRangeException(nameof(repoUrl));
            }

            if (name.EndsWith(".git", StringComparison.InvariantCultureIgnoreCase)) {
                name = name.Substring(0, name.Length - 4);
            }

            var localTemplateFolder = Path.Combine(targetParentFolderPath, name);
            return localTemplateFolder;
        }

        private bool ParseDate(string line, out DateTime? date) {
            date = null;

            // Date:   2016-07-28 10:03:07 +0200
            if (line.StartsWith("Date:")) {
                var text = line.Substring("Date:".Length);
                date = Convert.ToDateTime(text).ToUniversalTime();
                return true;
            }

            return false;
        }

        private bool ParseOrigin(string remote, out string url) {
            url = null;

            if (remote.StartsWith("origin")) {
                int start = remote.IndexOf("https");
                if (start >= 0) {
                    int end = remote.IndexOf(' ', start);
                    if (end >= 0) {
                        url = remote.Substring(start, end - start);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ParseRepoName(string repoUrl, out string name) {
            name = null;

            int index = repoUrl.LastIndexOf('/');
            if (index < 0) {
                return false;
            }

            name = repoUrl.Substring(index + 1);
            return true;
        }

        private static bool ExecutableOnPath(string executable) {
            try {
                ProcessStartInfo info = new ProcessStartInfo("where", executable);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                var process = Process.Start(info);
                process.WaitForExit();
                return process.ExitCode == 0;
            } catch (Win32Exception) {
                throw new NotSupportedException();
            }
        }
    }
}
