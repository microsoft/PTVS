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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public struct ModulePath {
        public static readonly ModulePath Empty = new ModulePath(null, null, null);

        /// <summary>
        /// The name by which the module can be imported in Python code.
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// The file containing the source for the module.
        /// </summary>
        public string SourceFile { get; set; }

        /// <summary>
        /// The path to the library containing the module.
        /// </summary>
        public string LibraryPath { get; set; }

        /// <summary>
        /// The last portion of <see cref="FullName"/>.
        /// </summary>
        public string Name {
            get {
                return FullName.Substring(FullName.LastIndexOf('.') + 1);
            }
        }

        /// <summary>
        /// True if the module is named '__main__' or '__init__'.
        /// </summary>
        public bool IsSpecialName {
            get {
                var name = Name;
                return name.Equals("__main__", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("__init__", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// True if the module is a binary file.
        /// </summary>
        public bool IsCompiled {
            get {
                return PythonBinaryRegex.IsMatch(Path.GetFileName(SourceFile));
            }
        }

        /// <summary>
        /// Creates a new ModulePath item.
        /// </summary>
        /// <param name="fullname">The full name of the module.</param>
        /// <param name="sourceFile">The full path to the source file
        /// implementing the module.</param>
        /// <param name="libraryPath">
        /// The path to the library containing the module. This is typically a
        /// higher-level directory of <paramref name="sourceFile"/>.
        /// </param>
        public ModulePath(string fullname, string sourceFile, string libraryPath)
            : this() {
            FullName = fullname;
            SourceFile = sourceFile;
            LibraryPath = libraryPath;
        }

        private static readonly Regex PythonPackageRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonEggRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)-.+\.egg$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonFileRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)\.pyw?$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonBinaryRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)\.pyd$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static IEnumerable<ModulePath> GetModuleNamesFromPathHelper(string libPath, string path, string baseModule, bool skipFiles) {
            Debug.Assert(baseModule == "" || baseModule.EndsWith("."));

            if (string.IsNullOrEmpty(path) ||
                path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ||
                !Directory.Exists(path)) {
                yield break;
            }

            if (!skipFiles) {
                foreach (var file in Directory.EnumerateFiles(path)) {
                    var filename = Path.GetFileName(file);
                    var match = PythonFileRegex.Match(filename);
                    if (!match.Success) {
                        match = PythonBinaryRegex.Match(filename);
                    }
                    if (match.Success) {
                        yield return new ModulePath(baseModule + match.Groups["name"].Value, file, libPath ?? path);
                    }
                }
            }

            foreach (var dir in Directory.EnumerateDirectories(path)) {
                var dirname = Path.GetFileName(dir);
                var match = PythonPackageRegex.Match(dirname);
                if (match.Success && File.Exists(Path.Combine(dir, "__init__.py"))) {
                    foreach (var entry in GetModuleNamesFromPathHelper(skipFiles ? dir : libPath, dir, baseModule + match.Groups["name"].Value + ".", false)) {
                        yield return entry;
                    }
                } else if (PythonEggRegex.IsMatch(dirname)) {
                    foreach (var entry in GetModuleNamesFromPathHelper(dir, dir, baseModule, false)) {
                        yield return entry;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a sequence of ModulePath items for all modules importable
        /// from the provided path, optionally excluding top level files.
        /// </summary>
        public static IEnumerable<ModulePath> GetModulesInPath(string path, bool includeTopLevelFiles = true) {
            return GetModuleNamesFromPathHelper(path, path, "", !includeTopLevelFiles);
        }

        /// <summary>
        /// Returns a sequence of ModulePath items for all modules importable
        /// from the provided paths.
        /// </summary>
        /// <param name="libPath">The main library path. All files in this path
        /// are considered.</param>
        /// <param name="paths">Other paths. Only files in subdirectories of
        /// these paths are considered.</param>
        public static IEnumerable<ModulePath> GetModulesInLib(IEnumerable<string> pathIncludingRoot, IEnumerable<string> pathExcludingRoot) {
            return pathIncludingRoot.SelectMany(path => GetModulesInPath(path, includeTopLevelFiles: true))
                .Concat(pathExcludingRoot.SelectMany(path => GetModulesInPath(path, includeTopLevelFiles: false)));
        }

        /// <summary>
        /// Expands a sequence of directory paths to include any paths that are
        /// referenced in .pth files.
        /// 
        /// The original directories are not included in the result.
        /// </summary>
        public static IEnumerable<string> ExpandPathFiles(IEnumerable<string> paths) {
            foreach (var path in paths) {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) {
                    foreach (var file in Directory.EnumerateFiles(path, "*.pth")) {
                        using (var reader = new StreamReader(file)) {
                            string line;
                            while ((line = reader.ReadLine()) != null) {
                                if (line.StartsWith("import ", StringComparison.Ordinal) ||
                                    line.IndexOfAny(Path.GetInvalidPathChars()) >= 0) {
                                    continue;
                                }
                                if (!Path.IsPathRooted(line)) {
                                    line = Path.Combine(path, line);
                                }
                                if (Directory.Exists(line)) {
                                    yield return line;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Expands a sequence of directory paths to include any paths that are
        /// referenced in .pth files.
        /// 
        /// The original directories are not included in the result.
        /// </summary>
        public static IEnumerable<string> ExpandPathFiles(params string[] paths) {
            return ExpandPathFiles(paths.AsEnumerable());
        }

        /// <summary>
        /// Returns a sequence of name-path pairs for all modules importable by
        /// the provided factory.
        /// </summary>
        public static IEnumerable<ModulePath> GetModulesInLib(IPythonInterpreterFactory factory) {
            string libDir, virtualEnvPackages;
            PythonTypeDatabase.GetLibDirs(factory, out libDir, out virtualEnvPackages);

            var siteDirs = new[] { Path.Combine(libDir, "site-packages") }.AsEnumerable();
            var libDirs = new[] { libDir }.Concat(ExpandPathFiles(siteDirs));

            return GetModulesInLib(libDirs, siteDirs);
        }


    }
}
