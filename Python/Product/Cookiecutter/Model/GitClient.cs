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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Model {
    class GitClient : IGitClient {
        private readonly string _gitExeFilePath;
        private readonly Redirector _redirector;

        public GitClient(string gitExeFilePath, Redirector redirector) {
            _gitExeFilePath = gitExeFilePath;
            _redirector = redirector;
        }

        public async Task<string> CloneAsync(string repoUrl, string targetParentFolderPath) {
            Directory.CreateDirectory(targetParentFolderPath);

            string localTemplateFolder = GetClonedFolder(repoUrl, targetParentFolderPath);

            if (Directory.Exists(localTemplateFolder)) {
                ShellUtils.DeleteDirectory(localTemplateFolder);
            }

            // Ensure we always capture the output, because we need to check for errors in stderr
            var stdOut = new List<string>();
            var stdErr = new List<string>();

            Redirector redirector;
            if (_redirector != null) {
                redirector = new TeeRedirector(_redirector, new ListRedirector(stdOut, stdErr));
            } else {
                redirector = new ListRedirector(stdOut, stdErr);
            }

            var arguments = new string[] { "clone", repoUrl };
            if (!ExistsOnPath(_gitExeFilePath)) {
                throw new FileNotFoundException();
            }
            using (var output = ProcessOutput.Run(_gitExeFilePath, arguments, targetParentFolderPath, GetEnvironment(), false, redirector)) {
                await output;

                var r = new ProcessOutputResult() {
                    ExeFileName = _gitExeFilePath,
                    ExitCode = output.ExitCode,
                    StandardOutputLines = stdOut.ToArray(),
                    StandardErrorLines = stdErr.ToArray(),
                };

                if (output.ExitCode < 0 || HasFatalError(stdErr)) {
                    if (Directory.Exists(localTemplateFolder)) {
                        // Don't leave a failed clone on disk
                        try {
                            ShellUtils.DeleteDirectory(localTemplateFolder);
                        } catch (Exception ex) when (!ex.IsCriticalException()) {
                        }
                    }

                    throw new ProcessException(r);
                }

                if (!Directory.Exists(localTemplateFolder)) {
                    throw new ProcessException(r);
                }

                return localTemplateFolder;
            }
        }   
        
        public bool ExistsOnPath(string fileName) {
            return GetFullPath(fileName) != null;
        }

        public string GetFullPath(string fileName) {
            if (File.Exists(fileName)) {
                return Path.GetFullPath(fileName);
            }
            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator)) {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        public async Task<string> GetRemoteOriginAsync(string repoFolderPath) {
            var arguments = new string[] { "remote", "-v" };
            using (var output = ProcessOutput.Run(_gitExeFilePath, arguments, repoFolderPath, GetEnvironment(), false, null)) {
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

            using (var output = ProcessOutput.Run(_gitExeFilePath, arguments, repoFolderPath, GetEnvironment(), false, null)) {
                await output;
                foreach (var line in output.StandardOutputLines) {
                    // Line with date starts with 'Date'. Example:
                    // Date:   2016-07-28 10:03:07 +0200
                    if (line.StartsWithOrdinal("Date:")) {
                        try {
                            var text = line.Substring("Date:".Length);
                            return Convert.ToDateTime(text).ToUniversalTime();
                        } catch (FormatException) {
                            return null;
                        }
                    }
                }
            }
            return null;
        }

        public async Task FetchAsync(string repoFolderPath) {
            var arguments = new string[] { "fetch" };
            using (var output = ProcessOutput.Run(_gitExeFilePath, arguments, repoFolderPath, GetEnvironment(), false, null)) {
                await output;
                if (output.ExitCode < 0 || HasFatalError(output.StandardErrorLines)) {
                    throw new ProcessException(new ProcessOutputResult() {
                        ExeFileName = _gitExeFilePath,
                        ExitCode = output.ExitCode,
                        StandardErrorLines = output.StandardErrorLines.ToArray(),
                        StandardOutputLines = output.StandardOutputLines.ToArray(),
                    });
                }
            }
        }

        public async Task MergeAsync(string repoFolderPath) {
            var arguments = new string[] { "merge" };
            using (var output = ProcessOutput.Run(_gitExeFilePath, arguments, repoFolderPath, GetEnvironment(), false, _redirector)) {
                if (await output < 0) {
                    throw new ProcessException(new ProcessOutputResult() {
                        ExeFileName = _gitExeFilePath,
                        ExitCode = output.ExitCode,
                    });
                }
            }
        }

        private Dictionary<string, string> GetEnvironment() {
            var path =
                Path.GetDirectoryName(_gitExeFilePath) + ";" +
                Environment.GetEnvironmentVariable("PATH") ?? "";

            return new Dictionary<string, string>() {
                { "PATH", path },
                { "GIT_FLUSH", "1" },
                { "GIT_TERMINAL_PROMPT", "0" }
            };
        }

        private static bool HasFatalError(IEnumerable<string> standardErrorLines) {
            return standardErrorLines.Any(line => line.StartsWithOrdinal("fatal:", ignoreCase: true));
        }

        private static string GetClonedFolder(string repoUrl, string targetParentFolderPath) {
            string name;
            if (!ParseRepoName(repoUrl, out name)) {
                throw new ArgumentOutOfRangeException(nameof(repoUrl));
            }

            if (name.EndsWithOrdinal(".git", ignoreCase: true)) {
                name = name.Substring(0, name.Length - 4);
            }

            var localTemplateFolder = Path.Combine(targetParentFolderPath, name);
            return localTemplateFolder;
        }

        private bool ParseOrigin(string remote, out string url) {
            url = null;

            if (remote.StartsWithOrdinal("origin")) {
                int start = remote.IndexOfOrdinal("https", ignoreCase: true);
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
    }
}
