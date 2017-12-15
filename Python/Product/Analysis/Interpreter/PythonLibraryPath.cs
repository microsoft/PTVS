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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using IOPath = System.IO.Path;

namespace Microsoft.PythonTools.Analysis {
    public sealed class PythonLibraryPath {
        private readonly string _path;
        private readonly bool _isStandardLibrary;
        private readonly string _modulePrefix;

        private static readonly Regex ParseRegex = new Regex(
            @"(?<path>[^|]+)\|(?<stdlib>stdlib)?\|(?<prefix>[^|]+)?"
        );

        public PythonLibraryPath(string path, bool isStandardLibrary, string modulePrefix) {
            _path = path;
            _isStandardLibrary = isStandardLibrary;
            _modulePrefix = modulePrefix;
        }

        public string Path {
            get { return _path; }
        }

        public bool IsStandardLibrary {
            get { return _isStandardLibrary; }
        }

        public string ModulePrefix {
            get { return _modulePrefix ?? string.Empty; }
        }

        public override string ToString() {
            return string.Format("{0}|{1}|{2}", _path, _isStandardLibrary ? "stdlib" : "", _modulePrefix ?? "");
        }

        public static PythonLibraryPath Parse(string s) {
            if (string.IsNullOrEmpty(s)) {
                throw new ArgumentNullException("source");
            }
            
            var m = ParseRegex.Match(s);
            if (!m.Success || !m.Groups["path"].Success) {
                throw new FormatException();
            }
            
            return new PythonLibraryPath(
                m.Groups["path"].Value,
                m.Groups["stdlib"].Success,
                m.Groups["prefix"].Success ? m.Groups["prefix"].Value : null
            );
        }

        /// <summary>
        /// Gets the default set of search paths based on the path to the root
        /// of the standard library.
        /// </summary>
        /// <param name="library">Root of the standard library.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        /// <remarks>New in 2.2, moved in 3.3</remarks>
        public static List<PythonLibraryPath> GetDefaultDatabaseSearchPaths(string library) {
            var result = new List<PythonLibraryPath>();
            if (!Directory.Exists(library)) {
                return result;
            }

            result.Add(new PythonLibraryPath(library, true, null));

            var sitePackages = PathUtils.GetAbsoluteDirectoryPath(library, "site-packages");
            if (!Directory.Exists(sitePackages)) {
                return result;
            }

            result.Add(new PythonLibraryPath(sitePackages, false, null));
            result.AddRange(ModulePath.ExpandPathFiles(sitePackages)
                .Select(p => new PythonLibraryPath(p, false, null))
            );

            return result;
        }

        /// <summary>
        /// Gets the set of search paths for the specified factory as
        /// efficiently as possible. This may involve executing the
        /// interpreter, and may cache the paths for retrieval later.
        /// </summary>
        public static async Task<IList<PythonLibraryPath>> GetDatabaseSearchPathsAsync(InterpreterConfiguration config, string cachePath) {
            for (int retries = 5; retries > 0; --retries) {
                if (!string.IsNullOrEmpty(cachePath)) {
                    var paths = GetCachedDatabaseSearchPaths(cachePath);
                    if (paths != null) {
                        return paths;
                    }
                }

                try {
                    var paths = await GetUncachedDatabaseSearchPathsAsync(config.InterpreterPath);
                    if (!string.IsNullOrEmpty(cachePath)) {
                        WriteDatabaseSearchPaths(cachePath, paths);
                    }
                    return paths;
                } catch (InvalidOperationException) {
                    // Failed to get paths
                    break;
                } catch (IOException) {
                    // Failed to write paths - sleep and then loop
                    Thread.Sleep(50);
                }
            }

            var ospy = PathUtils.FindFile(config.PrefixPath, "os.py", firstCheck: new[] { "Lib" });
            if (!string.IsNullOrEmpty(ospy)) {
                return GetDefaultDatabaseSearchPaths(PathUtils.GetParent(ospy));
            }

            return Array.Empty<PythonLibraryPath>();
        }

        /// <summary>
        /// Gets the set of search paths by running the interpreter.
        /// </summary>
        /// <param name="interpreter">Path to the interpreter.</param>
        /// <returns>A list of search paths for the interpreter.</returns>
        /// <remarks>Added in 2.2, moved in 3.3</remarks>
        public static async Task<List<PythonLibraryPath>> GetUncachedDatabaseSearchPathsAsync(string interpreter) {
            List<string> lines;

            // sys.path will include the working directory, so we make an empty
            // path that we can filter out later
            var tempWorkingDir = IOPath.Combine(IOPath.GetTempPath(), IOPath.GetRandomFileName());
            Directory.CreateDirectory(tempWorkingDir);
            var srcGetSearchPaths = PythonToolsInstallPath.GetFile("get_search_paths.py", typeof(PythonLibraryPath).Assembly);
            var getSearchPaths = PathUtils.GetAbsoluteFilePath(tempWorkingDir, PathUtils.GetFileOrDirectoryName(srcGetSearchPaths));
            File.Copy(srcGetSearchPaths, getSearchPaths);

            try {
                using (var proc = ProcessOutput.Run(
                    interpreter,
                    new[] {
                        "-S",   // don't import site - we do that in code
                        "-E",   // ignore environment
                        getSearchPaths
                    },
                    tempWorkingDir,
                    null,
                    false,
                    null
                )) {
                    int exitCode = -1;
                    try {
                        exitCode = await proc;
                    } catch (OperationCanceledException) {
                    }
                    if (exitCode != 0) {
                        throw new InvalidOperationException(string.Format(
                            "Cannot obtain list of paths{0}{1}",
                            Environment.NewLine,
                            string.Join(Environment.NewLine, proc.StandardErrorLines))
                        );
                    }

                    lines = proc.StandardOutputLines.ToList();
                }
            } finally {
                try {
                    Directory.Delete(tempWorkingDir, true);
                } catch (Exception ex) {
                    if (ex.IsCriticalException()) {
                        throw;
                    }
                }
            }

            return lines.Select(s => {
                if (s.StartsWith(tempWorkingDir, StringComparison.OrdinalIgnoreCase)) {
                    return null;
                }
                try {
                    return Parse(s);
                } catch (FormatException) {
                    Debug.Fail("Invalid format for search path: " + s);
                    return null;
                }
            }).Where(p => p != null).ToList();
        }

        /// <summary>
        /// Gets the set of search paths that were last saved for a database.
        /// </summary>
        /// <param name="databasePath">Path containing the database.</param>
        /// <returns>The cached list of search paths.</returns>
        /// <remarks>Added in 2.2, moved in 3.3</remarks>
        public static List<PythonLibraryPath> GetCachedDatabaseSearchPaths(string cachePath) {
            if (!File.Exists(cachePath)) {
                return null;
            }

            try {
                var result = new List<PythonLibraryPath>();
                using (var file = File.OpenText(cachePath)) {
                    string line;
                    while ((line = file.ReadLine()) != null) {
                        try {
                            result.Add(Parse(line));
                        } catch (FormatException) {
                            Debug.Fail("Invalid format: " + line);
                        }
                    }
                }

                return result;
            } catch (IOException) {
                return null;
            }
        }

        /// <summary>
        /// Saves search paths for a database.
        /// </summary>
        /// <param name="databasePath">The path to the database.</param>
        /// <param name="paths">The list of search paths.</param>
        /// <remarks>Added in 2.2, moved in 3.3</remarks>
        public static void WriteDatabaseSearchPaths(string cachePath, IEnumerable<PythonLibraryPath> paths) {
            try {
                Directory.CreateDirectory(PathUtils.GetParent(cachePath));
                using (var file = new StreamWriter(cachePath)) {
                    foreach (var path in paths) {
                        file.WriteLine(path.ToString());
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                // There's no recoverable reason for this to fail,
                // so report it when debugging otherwise silently
                // skip caching.
                Debug.Fail(ex.ToUnhandledExceptionMessage(typeof(PythonLibraryPath)));
                return;
            }
        }

    }
}
