/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.VisualStudioTools {
    internal static class CommonUtils {
        private static char[] InvalidPathChars = GetInvalidPathChars();

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
        public static string NormalizePath(string path) {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            var uri = MakeUri(path, false, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri) {
                if (uri.IsFile) {
                    return uri.LocalPath;
                } else {
                    return uri.AbsoluteUri.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            } else {
                return Uri.UnescapeDataString(uri.ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
        }

        /// <summary>
        /// Normalizes and returns the provided directory path, always
        /// ending with '/'.
        /// </summary>
        public static string NormalizeDirectoryPath(string path) {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            var uri = MakeUri(path, true, UriKind.RelativeOrAbsolute);
            if (uri.IsAbsoluteUri) {
                if (uri.IsFile) {
                    return uri.LocalPath;
                } else {
                    return uri.AbsoluteUri.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            } else {
                return Uri.UnescapeDataString(uri.ToString()).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
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
            if (HasEndSeparator(root) && !path.Contains("..") && path.StartsWith(root, StringComparison.Ordinal)) {
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

                    var rootedPath = TrimUpPaths(relPath);
                    if (rootedPath != relPath) {
                        rootedPath = sep + rootedPath;
                        if (new Uri(fromUri, rootedPath) == toUri) {
                            relPath = rootedPath;
                        }
                    }
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

                    var rootedPath = TrimUpPaths(relPath);
                    if (rootedPath != relPath) {
                        rootedPath = sep + rootedPath;
                        if (new Uri(fromUri, rootedPath) == toUri) {
                            relPath = rootedPath;
                        }
                    }
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
        /// Returns true if the path has a directory separator character at the end.
        /// </summary>
        public static bool HasEndSeparator(string path) {
            return (!string.IsNullOrEmpty(path) &&
                (path[path.Length - 1] == Path.DirectorySeparatorChar ||
                 path[path.Length - 1] == Path.AltDirectorySeparatorChar));
        }

        /// <summary>
        /// Removes up to one directory separator character from the end of path.
        /// </summary>
        public static string TrimEndSeparator(string path) {
            if (HasEndSeparator(path)) {
                return path.Remove(path.Length - 1);
            } else {
                return path;
            }
        }

        /// <summary>
        /// Adds a directory separator character to the end of path if required.
        /// </summary>
        public static string EnsureEndSeparator(string path) {
            if (!HasEndSeparator(path)) {
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
    }
}
