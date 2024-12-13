// Visual Studio Shared Project
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.PythonTools.Infrastructure {
    static class PathUtils {
        private static readonly char[] InvalidPathChars = GetInvalidPathChars();
        private static readonly char[] InvalidFileChars = Path.GetInvalidFileNameChars();

        private static readonly char[] DirectorySeparators = new[] {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        };

        private static char[] GetInvalidPathChars() {
            return Path.GetInvalidPathChars().Concat(new[] { '*', '?' }).ToArray();
        }

        internal static bool TryMakeUri(string path, bool isDirectory, UriKind kind, out Uri uri) {
            if (isDirectory && !string.IsNullOrEmpty(path) && !HasEndSeparator(path)) {
                path += Path.DirectorySeparatorChar;
            }

            return Uri.TryCreate(path, kind, out uri);
        }

        internal static Uri MakeUri(string path, bool isDirectory, UriKind kind, string throwParameterName = "path") {
            try {
                if (isDirectory && !string.IsNullOrEmpty(path) && !HasEndSeparator(path)) {
                    path += Path.DirectorySeparatorChar;
                }

                return new Uri(path, kind);

            } catch (UriFormatException ex) {
                throw new ArgumentException("Path was invalid", throwParameterName, ex);
            } catch (ArgumentException ex) {
                throw new ArgumentException("Path was invalid", throwParameterName, ex);
            }
        }

        /// <summary>
        /// Normalizes and returns the provided path.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// If the provided path contains invalid characters.
        /// </exception>
        public static string NormalizePath(string path) {
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            }

            Uri uri;
            if (TryMakeUri(path, false, UriKind.Absolute, out uri) &&
                uri.Scheme != Uri.UriSchemeFile) {
                return uri.AbsoluteUri;
            }

            var root = EnsureEndSeparator(Path.GetPathRoot(path));
            var parts = path.Substring(root.Length).Split(DirectorySeparators);
            bool isDir = string.IsNullOrWhiteSpace(parts[parts.Length - 1]);

            for (int i = 0; i < parts.Length; ++i) {
                if (string.IsNullOrEmpty(parts[i])) {
                    if (i > 0) {
                        parts[i] = null;
                    }
                    continue;
                }

                if (parts[i] == ".") {
                    parts[i] = null;
                    continue;
                }

                if (parts[i] == "..") {
                    bool found = false;
                    for (int j = i - 1; j >= 0; --j) {
                        if (!string.IsNullOrEmpty(parts[j])) {
                            parts[i] = null;
                            parts[j] = null;
                            found = true;
                            break;
                        }
                    }
                    if (!found && !string.IsNullOrEmpty(root)) {
                        parts[i] = null;
                    }
                    continue;
                }

                parts[i] = parts[i].TrimEnd(' ', '.');
            }

            var newPath = root + string.Join(
                Path.DirectorySeparatorChar.ToString(),
                parts.Where(s => s != null)
            );
            return isDir ? EnsureEndSeparator(newPath) : newPath;
        }

        /// <summary>
        /// Normalizes and returns the provided directory path, always
        /// ending with '/'.
        /// </summary>
        public static string NormalizeDirectoryPath(string path) {
            Uri uri;
            if (TryMakeUri(path, true, UriKind.Absolute, out uri) &&
                uri.Scheme != Uri.UriSchemeFile) {
                return uri.AbsoluteUri;
            }

            return EnsureEndSeparator(NormalizePath(path));
        }

        /// <summary>
        /// Return true if both paths represent the same directory.
        /// </summary>
        public static bool IsSameDirectory(string path1, string path2) {
            if (string.IsNullOrEmpty(path1)) {
                return string.IsNullOrEmpty(path2);
            } else if (string.IsNullOrEmpty(path2)) {
                return false;
            }

            if (String.Equals(path1, path2, StringComparison.Ordinal)) {
                // Quick return, but will only work where the paths are already normalized and
                // have matching case.
                return true;
            }

            Uri uri1, uri2;
            return
                TryMakeUri(path1, true, UriKind.Absolute, out uri1) &&
                TryMakeUri(path2, true, UriKind.Absolute, out uri2) &&
                uri1 == uri2;
        }

        /// <summary>
        /// Return true if both paths represent the same location.
        /// </summary>
        public static bool IsSamePath(string file1, string file2) {
            if (string.IsNullOrEmpty(file1)) {
                return string.IsNullOrEmpty(file2);
            } else if (string.IsNullOrEmpty(file2)) {
                return false;
            }

            if (String.Equals(file1, file2, StringComparison.Ordinal)) {
                // Quick return, but will only work where the paths are already normalized and
                // have matching case.
                return true;
            }

            Uri uri1, uri2;
            return
                TryMakeUri(file1, false, UriKind.Absolute, out uri1) &&
                TryMakeUri(file2, false, UriKind.Absolute, out uri2) &&
                uri1 == uri2;
        }

        /// <summary>
        /// Return true if the path represents a file or directory contained in
        /// root or a subdirectory of root.
        /// </summary>
        public static bool IsSubpathOf(string root, string path) {
            if (string.IsNullOrEmpty(root)) {
                return false;
            }
            
            if (HasEndSeparator(root) && !path.Contains("..") && path.StartsWithOrdinal(root)) {
                // Quick return, but only where the paths are already normalized and
                // have matching case.
                return true;
            }

            var uriRoot = MakeUri(root, true, UriKind.Absolute, "root");
            var uriPath = MakeUri(path, false, UriKind.Absolute, "path");

            if (uriRoot.Equals(uriPath) || uriRoot.IsBaseOf(uriPath)) {
                return true;
            }

            // Special case where root and path are the same, but path was provided
            // without a terminating separator.
            var uriDirectoryPath = MakeUri(path, true, UriKind.Absolute, "path");
            if (uriRoot.Equals(uriDirectoryPath)) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a normalized directory path created by joining relativePath to root.
        /// The result is guaranteed to end with a backslash.
        /// </summary>
        /// <exception cref="ArgumentException">root is not an absolute path, or
        /// either path is invalid.</exception>
        /// <exception cref="InvalidOperationException">An absolute path cannot be
        /// created.</exception>
        public static string GetAbsoluteDirectoryPath(string root, string relativePath) {
            string absPath;

            if (string.IsNullOrEmpty(relativePath)) {
                return NormalizeDirectoryPath(root);
            }

            var relUri = MakeUri(relativePath, true, UriKind.RelativeOrAbsolute, "relativePath");
            Uri absUri;

            if (relUri.IsAbsoluteUri) {
                absUri = relUri;
            } else {
                var rootUri = MakeUri(root, true, UriKind.Absolute, "root");
                try {
                    absUri = new Uri(rootUri, relUri);
                } catch (UriFormatException ex) {
                    throw new InvalidOperationException("Cannot create absolute path", ex);
                }
            }

            absPath = absUri.IsFile ? absUri.LocalPath : absUri.AbsoluteUri;

            if (!string.IsNullOrEmpty(absPath) && !HasEndSeparator(absPath)) {
                absPath += absUri.IsFile ? Path.DirectorySeparatorChar : Path.AltDirectorySeparatorChar;
            }

            return absPath;
        }

        /// <summary>
        /// Returns a normalized file path created by joining relativePath to root.
        /// The result is not guaranteed to end with a backslash.
        /// </summary>
        /// <exception cref="ArgumentException">root is not an absolute path, or
        /// either path is invalid.</exception>
        public static string GetAbsoluteFilePath(string root, string relativePath) {
            var rootUri = MakeUri(root, true, UriKind.Absolute, "root");
            var relUri = MakeUri(relativePath, false, UriKind.RelativeOrAbsolute, "relativePath");

            Uri absUri;

            if (relUri.IsAbsoluteUri) {
                absUri = relUri;
            } else {
                try {
                    absUri = new Uri(rootUri, relUri);
                } catch (UriFormatException ex) {
                    throw new InvalidOperationException("Cannot create absolute path", ex);
                }
            }

            return absUri.IsFile ? absUri.LocalPath : absUri.AbsoluteUri;
        }

        /// <summary>
        /// Returns a relative path from the base path to the other path. This is
        /// intended for serialization rather than UI. See CreateFriendlyDirectoryPath
        /// for UI strings.
        /// </summary>
        /// <exception cref="ArgumentException">Either parameter was an invalid or a
        /// relative path.</exception>
        public static string GetRelativeDirectoryPath(string fromDirectory, string toDirectory) {
            var fromUri = MakeUri(fromDirectory, true, UriKind.Absolute, "fromDirectory");
            var toUri = MakeUri(toDirectory, true, UriKind.Absolute, "toDirectory");

            string relPath;
            var sep = toUri.IsFile ? Path.DirectorySeparatorChar : Path.AltDirectorySeparatorChar;

            try {
                var relUri = fromUri.MakeRelativeUri(toUri);
                if (relUri.IsAbsoluteUri) {
                    relPath = relUri.IsFile ? relUri.LocalPath : relUri.AbsoluteUri;
                } else {
                    relPath = Uri.UnescapeDataString(relUri.ToString());
                }
            } catch (InvalidOperationException ex) {
                Trace.WriteLine(string.Format("Error finding path from {0} to {1}", fromUri, toUri));
                Trace.WriteLine(ex);
                relPath = toUri.IsFile ? toUri.LocalPath : toUri.AbsoluteUri;
            }

            if (!string.IsNullOrEmpty(relPath) && !HasEndSeparator(relPath)) {
                relPath += Path.DirectorySeparatorChar;
            }

            if (toUri.IsFile) {
                return relPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            } else {
                return relPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        /// <summary>
        /// Returns a relative path from the base path to the file. This is
        /// intended for serialization rather than UI. See CreateFriendlyFilePath
        /// for UI strings.
        /// </summary>
        public static string GetRelativeFilePath(string fromDirectory, string toFile) {
            var fromUri = MakeUri(fromDirectory, true, UriKind.Absolute, "fromDirectory");
            var toUri = MakeUri(toFile, false, UriKind.Absolute, "toFile");

            string relPath;
            var sep = toUri.IsFile ? Path.DirectorySeparatorChar : Path.AltDirectorySeparatorChar;

            try {
                var relUri = fromUri.MakeRelativeUri(toUri);
                if (relUri.IsAbsoluteUri) {
                    relPath = relUri.IsFile ? relUri.LocalPath : relUri.AbsoluteUri;
                } else {
                    relPath = Uri.UnescapeDataString(relUri.ToString());
                }
            } catch (InvalidOperationException ex) {
                Trace.WriteLine(string.Format("Error finding path from {0} to {1}", fromUri, toUri));
                Trace.WriteLine(ex);
                relPath = toUri.IsFile ? toUri.LocalPath : toUri.AbsoluteUri;
            }

            if (toUri.IsFile) {
                return relPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            } else {
                return relPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        /// <summary>
        /// Tries to create a friendly directory path: '.' if the same as base path,
        /// relative path if short, absolute path otherwise.
        /// </summary>
        public static string CreateFriendlyDirectoryPath(string basePath, string path) {
            var relativePath = GetRelativeDirectoryPath(basePath, path);

            if (relativePath.Length > 1) {
                relativePath = TrimEndSeparator(relativePath);
            }

            if (string.IsNullOrEmpty(relativePath)) {
                relativePath = ".";
            }

            return relativePath;
        }

        /// <summary>
        /// Tries to create a friendly file path.
        /// </summary>
        public static string CreateFriendlyFilePath(string basePath, string path) {
            return GetRelativeFilePath(basePath, path);
        }

        /// <summary>
        /// Returns the last directory segment of a path. The last segment is
        /// assumed to be the string between the second-last and last directory
        /// separator characters in the path. If there is no suitable substring,
        /// the empty string is returned.
        /// 
        /// The first segment of the path is only returned if it does not
        /// contain a colon. Segments equal to "." are ignored and the preceding
        /// segment is used.
        /// </summary>
        /// <remarks>
        /// This should be used in place of:
        /// <c>Path.GetFileName(CommonUtils.TrimEndSeparator(Path.GetDirectoryName(path)))</c>
        /// </remarks>
        public static string GetLastDirectoryName(string path) {
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            }

            int last = path.LastIndexOfAny(DirectorySeparators);

            string result = string.Empty;
            while (last > 1) {
                int first = path.LastIndexOfAny(DirectorySeparators, last - 1);
                if (first < 0) {
                    if (path.IndexOf(':') < last) {
                        // Don't want to return scheme/drive as a directory
                        return string.Empty;
                    }
                    first = -1;
                }
                if (first == 1 && path[0] == path[1]) {
                    // Don't return computer name in UNC path
                    return string.Empty;
                }

                result = path.Substring(first + 1, last - (first + 1));
                if (!string.IsNullOrEmpty(result) && result != ".") {
                    // Result is valid
                    break;
                }

                last = first;
            }

            return result;
        }

        /// <summary>
        /// Returns the path to the parent directory segment of a path. If the
        /// last character of the path is a directory separator, the segment
        /// prior to that character is removed. Otherwise, the segment following
        /// the last directory separator is removed.
        /// </summary>
        /// <remarks>
        /// This should be used in place of:
        /// <c>Path.GetDirectoryName(CommonUtils.TrimEndSeparator(path)) + Path.DirectorySeparatorChar</c>
        /// </remarks>
        public static string GetParent(string path) {
            if (string.IsNullOrEmpty(path) || path.Length <= 1) {
                return string.Empty;
            }

            int last = path.Length - 1;
            if (DirectorySeparators.Contains(path[last])) {
                last -= 1;
            }

            if (last <= 0) {
                return string.Empty;
            }

            last = path.LastIndexOfAny(DirectorySeparators, last);

            if (last < 0) {
                return string.Empty;
            }

            return path.Remove(last + 1);
        }

        /// <summary>
        /// Returns the last segment of the path. If the last character is a
        /// directory separator, this will be the segment preceding the
        /// separator. Otherwise, it will be the segment following the last
        /// separator.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetFileOrDirectoryName(string path) {
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            }

            int last = path.Length - 1;
            if (DirectorySeparators.Contains(path[last])) {
                last -= 1;
            }

            if (last < 0) {
                return string.Empty;
            }

            int start = path.LastIndexOfAny(DirectorySeparators, last);

            return path.Substring(start + 1, last - start);
        }

        /// <summary>
        /// Returns true if the path has a directory separator character at the end.
        /// </summary>
        public static bool HasEndSeparator(string path) {
            return !string.IsNullOrEmpty(path) && DirectorySeparators.Contains(path[path.Length - 1]);
        }

        /// <summary>
        /// Removes up to one directory separator character from the end of path.
        /// </summary>
        public static string TrimEndSeparator(string path) {
            if (HasEndSeparator(path)) {
                if (path.Length > 2 && path[path.Length - 2] == ':') {
                    // The slash at the end of a drive specifier is not actually
                    // a separator.
                    return path;
                } else if (path.Length > 3 && path[path.Length - 2] == path[path.Length - 1] && path[path.Length - 3] == ':') {
                    // The double slash at the end of a schema is not actually a
                    // separator.
                    return path;
                }
                return path.Remove(path.Length - 1);
            } else {
                return path;
            }
        }

        /// <summary>
        /// Adds a directory separator character to the end of path if required.
        /// </summary>
        public static string EnsureEndSeparator(string path) {
            if (string.IsNullOrEmpty(path)) {
                return string.Empty;
            } else if (!HasEndSeparator(path)) {
                return path + Path.DirectorySeparatorChar;
            } else {
                return path;
            }
        }

        /// <summary>
        /// Removes leading @"..\" segments from a path.
        /// </summary>
        private static string TrimUpPaths(string path) {
            int actualStart = 0;
            while (actualStart + 2 < path.Length) {
                if (path[actualStart] == '.' && path[actualStart + 1] == '.' &&
                    (path[actualStart + 2] == Path.DirectorySeparatorChar || path[actualStart + 2] == Path.AltDirectorySeparatorChar)) {
                    actualStart += 3;
                } else {
                    break;
                }
            }

            return (actualStart > 0) ? path.Substring(actualStart) : path;
        }

        /// <summary>
        /// Returns true if the path is a valid path, regardless of whether the
        /// file exists or not.
        /// </summary>
        public static bool IsValidPath(string path) {
            return !string.IsNullOrEmpty(path) &&
                path.IndexOfAny(InvalidPathChars) < 0;
        }

        public static bool IsValidFile(string file) {
            return !string.IsNullOrWhiteSpace(file) &&
                file.IndexOfAny(InvalidFileChars) < 0;
        }

        /// <summary>
        /// Recursively searches for a file using breadth-first-search. This
        /// ensures that the result closest to <paramref name="root"/> is
        /// returned first.
        /// </summary>
        /// <param name="root">
        /// Directory to start searching.
        /// </param>
        /// <param name="file">
        /// Filename to find. Wildcards are not supported.
        /// </param>
        /// <param name="depthLimit">
        /// The number of subdirectories to search in.
        /// </param>
        /// <param name="firstCheck">
        /// A sequence of subdirectories to prioritize.
        /// </param>
        /// <returns>
        /// The path to the file if found, including <paramref name="root"/>;
        /// otherwise, null.
        /// </returns>
        public static string FindFile(
            string root,
            string file,
            int depthLimit = 2,
            IEnumerable<string> firstCheck = null
        ) {
            if (!Directory.Exists(root)) {
                return null;
            }

            var candidate = GetAbsoluteFilePath(root, file);
            if (File.Exists(candidate)) {
                return candidate;
            }
            if (firstCheck != null) {
                foreach (var subPath in firstCheck) {
                    candidate = Path.Combine(root, subPath, file);
                    if (File.Exists(candidate)) {
                        return candidate;
                    }
                }
            }

            // Do a BFS of the filesystem to ensure we find the match closest to
            // the root directory.
            var dirQueue = new Queue<KeyValuePair<string, int>>();
            dirQueue.Enqueue(new KeyValuePair<string, int>(root, 0));
            while (dirQueue.Any()) {
                var dirDepth = dirQueue.Dequeue();
                string dir = dirDepth.Key;
                var result = EnumerateFiles(dir, file, recurse: false).FirstOrDefault();
                if (result != null) {
                    return result;
                }
                int depth = dirDepth.Value;
                if (depth < depthLimit) {
                    foreach (var subDir in EnumerateDirectories(dir, recurse: false)) {
                        dirQueue.Enqueue(new KeyValuePair<string, int>(subDir, depth + 1));
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a filename in the specified location with the specified name and extension.
        /// If the file already exist it will calculate a name with a number in it.
        /// </summary>
        public static string GetAvailableFilename(string location, string basename, string extension) {
            var newPath = Path.Combine(location, basename);
            int index = 0;
            if (File.Exists(newPath + extension)) {
                string candidateNewPath;
                do {
                    candidateNewPath = string.Format("{0}{1}", newPath, ++index);
                } while (File.Exists(candidateNewPath + extension));
                newPath = candidateNewPath;
            }
            string final = newPath + extension;
            return final;
        }

        /// <summary>
        /// Safely enumerates all subdirectories under a given root. If a
        /// subdirectory is inaccessible, it will not be returned (compare and
        /// contrast with Directory.GetDirectories, which will crash without
        /// returning any subdirectories at all).
        /// </summary>
        /// <param name="root">
        /// Directory to enumerate under. This is not returned from this
        /// function.
        /// </param>
        /// <param name="recurse">
        /// <c>true</c> to return subdirectories of subdirectories.
        /// </param>
        /// <param name="fullPaths">
        /// <c>true</c> to return full paths for all subdirectories. Otherwise,
        /// the relative path from <paramref name="root"/> is returned.
        /// </param>
        public static IEnumerable<string> EnumerateDirectories(
            string root,
            bool recurse = true,
            bool fullPaths = true
        ) {
            var queue = new Queue<string>();
            if (!root.EndsWithOrdinal("\\")) {
                root += "\\";
            }
            queue.Enqueue(root);

            while (queue.Any()) {
                var path = queue.Dequeue();
                if (!path.EndsWithOrdinal("\\")) {
                    path += "\\";
                }

                IEnumerable<string> dirs = null;
                try {
                    dirs = Directory.GetDirectories(path);
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }
                if (dirs == null) {
                    continue;
                }

                foreach (var d in dirs) {
                    if (!fullPaths && !d.StartsWithOrdinal(root, ignoreCase: true)) {
                        continue;
                    }
                    if (recurse) {
                        queue.Enqueue(d);
                    }
                    yield return fullPaths ? d : d.Substring(root.Length);
                }
            }
        }

        /// <summary>
        /// Safely enumerates all files under a given root. If a subdirectory is
        /// inaccessible, its files will not be returned (compare and contrast
        /// with Directory.GetFiles, which will crash without returning any
        /// files at all).
        /// </summary>
        /// <param name="root">
        /// Directory to enumerate.
        /// </param>
        /// <param name="pattern">
        /// File pattern to return. You may use wildcards * and ?.
        /// </param>
        /// <param name="recurse">
        /// <c>true</c> to return files within subdirectories.
        /// </param>
        /// <param name="fullPaths">
        /// <c>true</c> to return full paths for all subdirectories. Otherwise,
        /// the relative path from <paramref name="root"/> is returned.
        /// </param>
        public static IEnumerable<string> EnumerateFiles(
            string root,
            string pattern = "*",
            bool recurse = true,
            bool fullPaths = true
        ) {
            if (!root.EndsWithOrdinal("\\")) {
                root += "\\";
            }

            var dirs = Enumerable.Repeat(root, 1);
            if (recurse) {
                dirs = dirs.Concat(EnumerateDirectories(root, true, false));
            }

            foreach (var dir in dirs) {
                var fullDir = Path.IsPathRooted(dir) ? dir : (root + dir);
                var dirPrefix = Path.IsPathRooted(dir) ? "" : EnsureEndSeparator(dir);

                IEnumerable<string> files = null;
                try {
                    files = Directory.GetFiles(fullDir, pattern);
                } catch (UnauthorizedAccessException) {
                } catch (IOException) {
                }
                if (files == null) {
                    continue;
                }

                foreach (var f in files) {
                    if (fullPaths) {
                        yield return f;
                    } else {
                        var relPath = dirPrefix + GetFileOrDirectoryName(f);
                        if (File.Exists(root + relPath)) {
                            yield return relPath;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a dictionary containing values from the two environments.
        /// </summary>
        /// <param name="baseEnvironment">The base environment.</param>
        /// <param name="subEnvironment">
        /// The sub environment. Values in this sequence override the base
        /// environment, unless they are merged.
        /// </param>
        /// <param name="keysToMerge">
        /// List of key names that should be merged. Keys are merged by
        /// appending the base environment's value after the sub environment,
        /// and separating with <see cref="Path.PathSeparator"/>.
        /// </param>
        public static Dictionary<string, string> MergeEnvironments(
            IEnumerable<KeyValuePair<string, string>> baseEnvironment,
            IEnumerable<KeyValuePair<string, string>> subEnvironment,
            params string[] keysToMerge
        ) {
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in baseEnvironment.MaybeEnumerate()) {
                env[kv.Key] = kv.Value;
            }

            var merge = new HashSet<string>(keysToMerge, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in subEnvironment.MaybeEnumerate()) {
                string existing;
                if (env.TryGetValue(kv.Key, out existing) && !string.IsNullOrWhiteSpace(existing)) {
                    if (merge.Contains(kv.Key)) {
                        env[kv.Key] = kv.Value + Path.PathSeparator + existing;
                    } else {
                        env[kv.Key] = kv.Value;
                    }
                } else {
                    env[kv.Key] = kv.Value;
                }
            }

            return env;
        }

        /// <summary>
        /// Creates a dictionary containing the environment specified in a
        /// multi-line string.
        /// </summary>
        public static Dictionary<string, string> ParseEnvironment(string environment) {
            var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(environment)) {
                foreach (var envVar in environment.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                    var nameValue = envVar.Split(new[] { '=' }, 2);
                    if (nameValue.Length == 2) {
                        env[nameValue[0]] = nameValue[1];
                    }
                }
            }

            return env;
        }

        /// <summary>
        /// Joins a list of paths using the path separator character (typically
        /// a semicolon).
        /// </summary>
        public static string JoinPathList(IEnumerable<string> paths) {
            var sb = new StringBuilder();
            foreach (var p in paths) {
                sb.Append(p);
                sb.Append(Path.PathSeparator);
            }
            if (sb.Length > 0) {
                sb.Length -= 1;
            }
            return sb.ToString();
        }

        public static bool HasExtension(string filePath, string ext) {
            int i = (filePath ?? throw new ArgumentNullException(nameof(filePath))).LastIndexOf('.');
            if (i < 0) {
                return string.IsNullOrEmpty(ext);
            }
            if (string.IsNullOrEmpty(ext)) {
                return false;
            }
            return string.Compare(filePath, i + 1, ext, (ext[0] == '.' ? 1 : 0), int.MaxValue, StringComparison.OrdinalIgnoreCase) == 0;
        }

    }
}
