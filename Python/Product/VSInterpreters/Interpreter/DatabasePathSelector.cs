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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Interpreter {
    static class DatabasePathSelector {
        public static string CalculateGlobalDatabasePath(InterpreterConfiguration config, int version) {
            Debug.Assert(version == LegacyDB.PythonTypeDatabase.FormatVersion, "Global paths are only supported for legacy databases");

            return PathUtils.GetAbsoluteDirectoryPath(
                LegacyDB.PythonTypeDatabase.CompletionDatabasePath,
                GetRelativePathForConfigurationId(config.Id)
            );
        }

        public static string GetRelativePathForConfigurationId(string id) {
            var subpath = id.Split('|');
            for (int i = 0; i < subpath.Length; ++i) {
                if (!PathUtils.IsValidPath(subpath[i])) {
                    subpath[i] = ToBase64(subpath[i]);
                }
            }
            return Path.Combine(subpath);
        }

        private static string ToBase64(string s) {
            return Convert.ToBase64String(new UTF8Encoding(false).GetBytes(s));
        }

        private static bool TryGetShellProperty<T>(this IServiceProvider provider, __VSSPROPID propId, out T value) {
            object obj;
            var shell = (IVsShell)provider.GetService(typeof(SVsShell));
            if (shell == null || shell.GetProperty((int)propId, out obj) != 0) {
                value = default(T);
                return false;
            }
            try {
                value = (T)obj;
                return true;
            } catch (InvalidCastException) {
                Debug.Fail("Expected property of type {0} but got value of type {1}".FormatUI(typeof(T).FullName, obj.GetType().FullName));
                value = default(T);
                return false;
            }
        }

        public static string CalculateVSLocalDatabasePath(IServiceProvider site, InterpreterConfiguration config, int version) {
            string appData;
            string subdir = "Python";
            if (site == null) {
                appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                subdir = "Python Tools";
            } else if (!site.TryGetShellProperty((__VSSPROPID)__VSSPROPID4.VSSPROPID_LocalAppDataDir, out appData)) {
                return null;
            }
            return PathUtils.GetAbsoluteDirectoryPath(
                appData,
                Path.Combine(
                    subdir,
#if DEBUG
                    $"CompletionDB-{version}-Debug",
#else
                    $"CompletionDB-{version}",
#endif
                    GetRelativePathForConfigurationId(config.Id)
                )
            );
        }

        public static string CalculateProjectLocalDatabasePath(IServiceProvider site, InterpreterConfiguration config, int version) {
            if (version == 0) {
                return PathUtils.GetAbsoluteDirectoryPath(config.PrefixPath, ".ptvs");
            }

            string appData;
            string subdir = "Python";
            if (site == null) {
                appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                subdir = "Python Tools";
            } else if (!site.TryGetShellProperty((__VSSPROPID)__VSSPROPID4.VSSPROPID_LocalAppDataDir, out appData)) {
                return null;
            }
            var root = PathUtils.GetAbsoluteDirectoryPath(
                appData,
                Path.Combine(
                    subdir,
#if DEBUG
                    $"CompletionDB-{version}-Temp-Debug"
#else
                    $"CompletionDB-{version}-Temp"
#endif
                )
            );

            string candidate;
            using (var map = OpenFile(PathUtils.GetAbsoluteFilePath(root, "paths.map"), false)) {
                var maplines = ReadAllLines(map);
                var match = maplines.FirstOrDefault(kv => config.PrefixPath.Equals(kv.Value, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match.Key)) {
                    return PathUtils.GetAbsoluteDirectoryPath(root, match.Key);
                }

                candidate = Enumerable.Range(0, int.MaxValue)
                    .Select(n => n.ToString(CultureInfo.InvariantCulture))
                    .FirstOrDefault(n => !maplines.Any(m => m.Key == n));
                if (map != null) {
                    maplines.Add(new KeyValuePair<string, string>(candidate, config.PrefixPath));
                    WriteAllLines(map, maplines);
                    return PathUtils.GetAbsoluteDirectoryPath(root, candidate);
                }
            }

            using (var map = OpenFile(PathUtils.GetAbsoluteFilePath(root, "paths.map"), true)) {
                var maplines = ReadAllLines(map);
                if (maplines.Any()) {
                    // Raced with a file change. Just recurse and we won't hit this
                    // path next time.
                    return CalculateProjectLocalDatabasePath(site, config, version);
                }

                maplines.Add(new KeyValuePair<string, string>(candidate, config.PrefixPath));
                WriteAllLines(map, maplines);
                return PathUtils.GetAbsoluteDirectoryPath(root, candidate);
            }
        }

        private static List<KeyValuePair<string, string>> ReadAllLines(Stream stream) {
            if (stream == null) {
                return new List<KeyValuePair<string, string>>();
            }

            stream.Seek(0, SeekOrigin.Begin);
            var lines = new List<KeyValuePair<string, string>>();
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, true)) {
                for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
                    int i = line.IndexOf(';');
                    if (i > 0) {
                        lines.Add(new KeyValuePair<string, string>(line.Remove(i), line.Substring(i + 1)));
                    }
                }
            }
            return lines;
        }

        private static void WriteAllLines(Stream stream, IEnumerable<KeyValuePair<string, string>> lines) {
            stream.Seek(0, SeekOrigin.Begin);
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, true)) {
                foreach (var line in lines) {
                    writer.WriteLine($"{line.Key};{line.Value}");
                }
            }
            stream.SetLength(stream.Position);
        }

        private static FileStream OpenFile(string path, bool create) {
            // Retry for up to one second
            for (int retries = 100; retries > 0; --retries) {
                try {
                    return new FileStream(path, create ? FileMode.OpenOrCreate : FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                } catch (FileNotFoundException) when (!create) {
                    return null;
                } catch (DirectoryNotFoundException) when (!create) {
                    return null;
                } catch (IOException) {
                    var dir = PathUtils.GetParent(path);
                    try {
                        Directory.CreateDirectory(dir);
                    } catch (IOException) {
                        // Cannot create directory for DB, so just bail out
                        return null;
                    }
                    Thread.Sleep(10);
                }
            }
            return null;
        }

    }
}
