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
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Analysis {
    public struct ModulePath {
        public static readonly ModulePath Empty = new ModulePath(null, null, null);

        /// <summary>
        /// Returns true if the provided version of Python can only import
        /// packages containing an <c>__init__.py</c> file.
        /// </summary>
        public static bool PythonVersionRequiresInitPyFiles(Version languageVersion) {
            return languageVersion < new Version(3, 3);
        }

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
        /// The same as FullName unless the last part of the name is '__init__',
        /// in which case this is FullName without the last part.
        /// </summary>
        public string ModuleName {
            get {
                if (Name.Equals("__init__", StringComparison.OrdinalIgnoreCase)) {
                    int lastDot = FullName.LastIndexOf('.');
                    if (lastDot < 0) {
                        return string.Empty;
                    } else {
                        return FullName.Substring(0, lastDot);
                    }
                } else {
                    return FullName;
                }
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

        private static readonly Regex PythonPackageRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonFileRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)\.pyw?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonBinaryRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)\.pyd$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static IEnumerable<ModulePath> GetModuleNamesFromPathHelper(
            string libPath,
            string path,
            string baseModule,
            bool skipFiles,
            bool recurse,
            bool requireInitPy
        ) {
            Debug.Assert(baseModule == "" || baseModule.EndsWith("."));

            if (!Directory.Exists(path)) {
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

            if (recurse) {
                foreach (var dir in Directory.EnumerateDirectories(path)) {
                    var dirname = Path.GetFileName(dir);
                    var match = PythonPackageRegex.Match(dirname);
                    if (match.Success && (!requireInitPy || File.Exists(Path.Combine(dir, "__init__.py")))) {
                        foreach (var entry in GetModuleNamesFromPathHelper(
                            skipFiles ? dir : libPath,
                            dir,
                            baseModule + match.Groups["name"].Value + ".",
                            false,
                            true,
                            requireInitPy
                        )) {
                            yield return entry;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns a sequence of ModulePath items for all modules importable
        /// from the provided path, optionally excluding top level files.
        /// </summary>
        public static IEnumerable<ModulePath> GetModulesInPath(
            string path,
            bool includeTopLevelFiles = true,
            bool recurse = true,
            string basePackage = null,
            bool requireInitPy = true
        ) {
            return GetModuleNamesFromPathHelper(
                path,
                path,
                basePackage ?? string.Empty,
                !includeTopLevelFiles,
                recurse,
                requireInitPy
            );
        }

        /// <summary>
        /// Returns a sequence of ModulePath items for all modules importable
        /// from the provided path, optionally excluding top level files.
        /// </summary>
        public static IEnumerable<ModulePath> GetModulesInPath(
            IEnumerable<string> paths,
            bool includeTopLevelFiles = true,
            bool recurse = true,
            string baseModule = null,
            bool requireInitPy = true
        ) {
            return paths.SelectMany(p => GetModuleNamesFromPathHelper(
                p,
                p,
                baseModule ?? string.Empty,
                !includeTopLevelFiles,
                recurse,
                requireInitPy
            ));
        }

        /// <summary>
        /// Expands a sequence of directory paths to include any paths that are
        /// referenced in .pth files.
        /// 
        /// The original directories are not included in the result.
        /// </summary>
        public static IEnumerable<string> ExpandPathFiles(IEnumerable<string> paths) {
            foreach (var path in paths) {
                if (Directory.Exists(path)) {
                    foreach (var file in Directory.EnumerateFiles(path, "*.pth")) {
                        using (var reader = new StreamReader(file)) {
                            string line;
                            while ((line = reader.ReadLine()) != null) {
                                if (line.StartsWith("import ", StringComparison.Ordinal) ||
                                    !CommonUtils.IsValidPath(line)) {
                                    continue;
                                }
                                line = line.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                                if (!Path.IsPathRooted(line)) {
                                    line = CommonUtils.GetAbsoluteDirectoryPath(path, line);
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
        /// Returns a sequence of ModulePaths for all modules importable from
        /// the specified library.
        /// </summary>
        public static IEnumerable<ModulePath> GetModulesInLib(
            string interpreterPath,
            string libraryPath,
            string sitePath = null,
            bool requireInitPyFiles = true
        ) {
            if (File.Exists(interpreterPath)) {
                interpreterPath = Path.GetDirectoryName(interpreterPath);
            }
            if (!Directory.Exists(libraryPath)) {
                return Enumerable.Empty<ModulePath>();
            }
            if (string.IsNullOrEmpty(sitePath)) {
                sitePath = Path.Combine(libraryPath, "site-packages");
            }
            var pthDirs = ExpandPathFiles(sitePath);
            var excludedPthDirs = new HashSet<string>() {
                sitePath,
                libraryPath
            };

            // Get modules in stdlib
            var modulesInStdLib = GetModulesInPath(libraryPath, true, true, requireInitPy: requireInitPyFiles);

            // Get files in site-packages
            var modulesInSitePackages = GetModulesInPath(sitePath, true, false, requireInitPy: requireInitPyFiles);

            // Get directories in site-packages
            // This is separate from getting files to ensure that each package
            // gets its own library path.
            var packagesInSitePackages = GetModulesInPath(sitePath, false, true, requireInitPy: requireInitPyFiles);

            // Get modules in DLLs directory
            IEnumerable<ModulePath> modulesInDllsPath;

            // Get modules in interpreter directory
            IEnumerable<ModulePath> modulesInExePath;

            if (Directory.Exists(interpreterPath)) {
                modulesInDllsPath = GetModulesInPath(Path.Combine(interpreterPath, "DLLs"), true, false);
                modulesInExePath = GetModulesInPath(interpreterPath, true, false);
                excludedPthDirs.Add(interpreterPath);
                excludedPthDirs.Add(Path.Combine(interpreterPath, "DLLs"));
            } else {
                modulesInDllsPath = Enumerable.Empty<ModulePath>();
                modulesInExePath = Enumerable.Empty<ModulePath>();
            }

            // Get directories referenced by pth files
            var modulesInPath = GetModulesInPath(
                pthDirs.Where(p1 => excludedPthDirs.All(p2 => !CommonUtils.IsSameDirectory(p1, p2))),
                true,
                true,
                requireInitPy: requireInitPyFiles
            );

            return modulesInPath
                .Concat(modulesInDllsPath)
                .Concat(modulesInStdLib)
                .Concat(modulesInExePath)
                .Concat(modulesInSitePackages)
                .Concat(packagesInSitePackages);
        }

        /// <summary>
        /// Returns a sequence of ModulePaths for all modules importable by the
        /// provided factory.
        /// </summary>
        public static IEnumerable<ModulePath> GetModulesInLib(IPythonInterpreterFactory factory) {
            return GetModulesInLib(
                factory.Configuration.InterpreterPath,
                factory.Configuration.LibraryPath,
                null,   // default site-packages path
                PythonVersionRequiresInitPyFiles(factory.Configuration.Version)
            );
        }

        /// <summary>
        /// Returns true if the provided path references an importable Python
        /// module. This function does not access the filesystem.
        /// </summary>
        public static bool IsPythonFile(string path) {
            var name = Path.GetFileName(path);
            var nameMatch = PythonFileRegex.Match(name);
            if (nameMatch == null || !nameMatch.Success) {
                nameMatch = PythonBinaryRegex.Match(name);
            }
            return nameMatch != null && nameMatch.Success;
        }

        /// <summary>
        /// Returns a new ModulePath value determined from the provided full
        /// path to a Python file. This function will access the filesystem to
        /// determine the package name.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// path is not a valid Python module.
        /// </exception>
        public static ModulePath FromFullPath(string path) {
            return FromFullPath(path, null);
        }

        /// <summary>
        /// Returns a new ModulePath value determined from the provided full
        /// path to a Python file. This function will access the filesystem to
        /// determine the package name.
        /// </summary>
        /// <param name="topLevelPath">
        /// The directory to stop searching for packages at. The module name
        /// will never include the last segment of this path.
        /// </param>
        /// <exception cref="ArgumentException">
        /// path is not a valid Python module.
        /// </exception>
        public static ModulePath FromFullPath(string path, string topLevelPath) {
            var name = Path.GetFileName(path);
            var nameMatch = PythonFileRegex.Match(name);
            if (nameMatch == null || !nameMatch.Success) {
                nameMatch = PythonBinaryRegex.Match(name);
            }
            if (nameMatch == null || !nameMatch.Success) {
                throw new ArgumentException("Not a valid Python module: " + path);
            }

            var fullName = nameMatch.Groups["name"].Value;
            var remainder = Path.GetDirectoryName(path);
            while (
                CommonUtils.IsValidPath(remainder) &&
                File.Exists(Path.Combine(remainder, "__init__.py")) &&
                (string.IsNullOrEmpty(topLevelPath) ||
                 (CommonUtils.IsSubpathOf(topLevelPath, remainder) &&
                  !CommonUtils.IsSameDirectory(topLevelPath, remainder)))
            ) {
                fullName = Path.GetFileName(remainder) + "." + fullName;
                remainder = Path.GetDirectoryName(remainder);
            }

            return new ModulePath(fullName, path, remainder);
        }
    }
}
