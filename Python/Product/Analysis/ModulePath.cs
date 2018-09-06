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
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public struct ModulePath {
        public static readonly ModulePath Empty = new ModulePath(null, null, null);

        /// <summary>
        /// Returns true if the provided version of Python can only import
        /// packages containing an <c>__init__.py</c> file.
        /// </summary>
        public static bool PythonVersionRequiresInitPyFiles(Version languageVersion)
            => languageVersion < new Version(3, 3);

        /// <summary>
        /// The name by which the module can be imported in Python code.
        /// </summary>
        public string FullName { get; }

        /// <summary>
        /// The file containing the source for the module.
        /// </summary>
        public string SourceFile { get; }

        /// <summary>
        /// The path to the library containing the module.
        /// </summary>
        public string LibraryPath { get; }

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
        /// <remarks>Changed in 2.2 to include .pyc and .pyo files.</remarks>
        public bool IsCompiled => PythonCompiledRegex.IsMatch(PathUtils.GetFileName(SourceFile));

        /// <summary>
        /// True if the module is a native extension module.
        /// </summary>
        /// <remarks>New in 2.2</remarks>
        public bool IsNativeExtension => PythonBinaryRegex.IsMatch(PathUtils.GetFileName(SourceFile));

        /// <summary>
        /// True if the module is a stub file.
        /// </summary>
        /// <remarks>New in 3.2</remarks>
        public bool IsStub => PythonStubRegex.IsMatch(PathUtils.GetFileName(SourceFile));

        /// <summary>
        /// True if the module can only be used in debug builds of the interpreter.
        /// </summary>
        public bool IsDebug {
            get {
                var m = PythonBinaryRegex.Match(PathUtils.GetFileName(SourceFile));
                // Only binaries require debug builds
                if (!m.Success) {
                    return false;
                }

                if (m.Groups["windebug"].Success) {
                    return true;
                }

                var abiTag = PythonAbiTagRegex.Match(m.Groups["abitag"].Value);
                if (abiTag.Groups["flags"].Value?.Contains("d") == true) {
                    return true;
                }

                return false;
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

        private static readonly Regex PythonPackageRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)(?<stubs>-stubs)?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonFileRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)\.py[iw]?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonStubRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)\.pyi$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonBinaryRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+?(?<windebug>_d)?)\.((?<abitag>(\w|_|-)+?)\.)?(pyd|so|dylib)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonCompiledRegex = new Regex(@"^(?!\d)(?<name>(\w|_)+)\.((\w|_|-)+?\.)?(pyd|py[co]|so|dylib)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex PythonAbiTagRegex = new Regex(@"^(
              (?<implementation>\w+)-(?<version>\d+)(?<flags>[dmu]+)?   # SOABI style
            | (?<abi>abi\d+)                                            # Stable ABI style
            | (?<version>\w+)-(?<platform>(\w|_)+)                      # Windows style
            )$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        private static IEnumerable<ModulePath> GetModuleNamesFromPathHelper(
            string libPath,
            string path,
            string baseModule,
            bool skipFiles,
            bool recurse,
            bool requireInitPy,
            bool includePackages
        ) {
            Debug.Assert(baseModule == "" || baseModule.EndsWithOrdinal("."));

            if (!Directory.Exists(path)) {
                yield break;
            }

            if (!skipFiles) {
                foreach (var file in PathUtils.EnumerateFiles(path, recurse: false)) {
                    var filename = PathUtils.GetFileName(file);
                    var match = PythonFileRegex.Match(filename);
                    if (!match.Success) {
                        match = PythonBinaryRegex.Match(filename);
                    }
                    if (match.Success) {
                        var name = match.Groups["name"].Value;
                        if (name.EndsWithOrdinal("_d") && file.EndsWithOrdinal(".pyd")) {
                            name = name.Remove(name.Length - 2);
                        }
                        yield return new ModulePath(baseModule + name, file, libPath ?? path);
                    }
                }
            }

            var directories = new List<ModulePath>();
            foreach (var dir in PathUtils.EnumerateDirectories(path, recurse: false)) {
                var dirname = PathUtils.GetFileName(dir);
                var match = PythonPackageRegex.Match(dirname);
                if (match.Success && !match.Groups["stubs"].Success) {
                    bool hasInitPy = true;
                    var modulePath = dir;
                    if (requireInitPy) {
                        modulePath = GetPackageInitPy(dir);
                        hasInitPy = File.Exists(modulePath);
                    }
                    if (hasInitPy) {
                        directories.Add(new ModulePath(
                            baseModule + match.Groups["name"].Value,
                            modulePath,
                            dir
                        ));
                    }
                }
            }

            if (recurse) {
                foreach (var dir in directories) {
                    foreach (var entry in GetModuleNamesFromPathHelper(
                        skipFiles ? dir.LibraryPath : libPath,
                        dir.LibraryPath,
                        dir.ModuleName + ".",
                        false,
                        true,
                        requireInitPy,
                        includePackages
                    )) {
                        yield return entry;
                    }
                }
            } else if (includePackages) {
                foreach (var dir in directories) {
                    yield return dir;
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
            bool requireInitPy = true,
            bool includePackages = false
        ) {
            return GetModuleNamesFromPathHelper(
                path,
                path,
                basePackage ?? string.Empty,
                !includeTopLevelFiles,
                recurse,
                requireInitPy,
                includePackages
            ).Where(mp => !string.IsNullOrEmpty(mp.ModuleName));
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
            bool requireInitPy = true,
            bool includePackages = false
        ) {
            return paths.SelectMany(p => GetModuleNamesFromPathHelper(
                p,
                p,
                baseModule ?? string.Empty,
                !includeTopLevelFiles,
                recurse,
                requireInitPy,
                includePackages
            )).Where(mp => !string.IsNullOrEmpty(mp.ModuleName));
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
                    foreach (var file in PathUtils.EnumerateFiles(path, "*.pth", recurse: false)) {
                        using (var reader = new StreamReader(file)) {
                            string line;
                            while ((line = reader.ReadLine()) != null) {
                                line = line.Trim();
                                if (line.StartsWithOrdinal("import ") ||
                                    !PathEqualityComparer.IsValidPath(line)) {
                                    continue;
                                }
                                line = line.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
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
        /// Returns a sequence of ModulePaths for all modules importable from
        /// the specified library.
        /// </summary>
        /// <remarks>
        /// Where possible, callers should use the methods from
        /// <see cref="PythonTypeDatabase"/> instead, as those are more accurate
        /// in the presence of non-standard Python installations. This function
        /// makes many assumptions about the install layout and may miss some
        /// modules.
        /// </remarks>
        public static IEnumerable<ModulePath> GetModulesInLib(
            string prefixPath,
            string libraryPath = null,
            string sitePath = null,
            bool requireInitPyFiles = true
        ) {
            if (File.Exists(prefixPath)) {
                prefixPath = Path.GetDirectoryName(prefixPath);
            }
            if (!Directory.Exists(libraryPath)) {
                libraryPath = Path.Combine(prefixPath, "Lib");
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

            if (Directory.Exists(prefixPath)) {
                modulesInDllsPath = GetModulesInPath(Path.Combine(prefixPath, "DLLs"), true, false);
                modulesInExePath = GetModulesInPath(prefixPath, true, false);
                excludedPthDirs.Add(prefixPath);
                excludedPthDirs.Add(Path.Combine(prefixPath, "DLLs"));
            } else {
                modulesInDllsPath = Enumerable.Empty<ModulePath>();
                modulesInExePath = Enumerable.Empty<ModulePath>();
            }

            // Get directories referenced by pth files
            var modulesInPath = GetModulesInPath(
                pthDirs.Except(excludedPthDirs, PathEqualityComparer.Instance),
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
        public static IEnumerable<ModulePath> GetModulesInLib(InterpreterConfiguration config) {
            return GetModulesInLib(
                config.PrefixPath,
                null,   // default library path
                null,   // default site-packages path
                PythonVersionRequiresInitPyFiles(config.Version)
            );
        }

        /// <summary>
        /// Returns true if the provided path references an importable Python
        /// module. This function does not access the filesystem.
        /// Retuns false if an invalid string is provided. This function does
        /// not raise exceptions.
        /// </summary>
        public static bool IsPythonFile(string path) {
            return IsPythonFile(path, true, true, true);
        }

        /// <summary>
        /// Returns true if the provided path references an editable Python
        /// source module. This function does not access the filesystem.
        /// Retuns false if an invalid string is provided. This function does
        /// not raise exceptions.
        /// </summary>
        /// <remarks>
        /// This function may return true even if the file is not an importable
        /// module. Use <see cref="IsPythonFile"/> and specify "strict" to
        /// ensure the module can be imported.
        /// </remarks>
        public static bool IsPythonSourceFile(string path) {
            return IsPythonFile(path, false, false, false);
        }

        /// <summary>
        /// Returns true if the provided name can be imported in Python code.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool IsImportable(string name) {
            try {
                return PythonPackageRegex.IsMatch(name);
            } catch (RegexMatchTimeoutException) {
                return false;
            }
        }

        /// <summary>
        /// Returns true if the provided path references an importable Python
        /// module. This function does not access the filesystem.
        /// Retuns false if an invalid string is provided. This function does
        /// not raise exceptions.
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <param name="strict">
        /// True if the filename must be importable; false to allow unimportable
        /// names.
        /// </param>
        /// <param name="allowCompiled">
        /// True if pyd files should be allowed.
        /// </param>
        /// <param name="allowCache">
        /// True if pyc and pyo files should be allowed.
        /// </param>
        public static bool IsPythonFile(string path, bool strict, bool allowCompiled, bool allowCache) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }

            string name;
            try {
                name = PathUtils.GetFileName(path);
            } catch (ArgumentException) {
                return false;
            }

            if (strict) {
                try {
                    var nameMatch = PythonFileRegex.Match(name);
                    if (allowCompiled && (nameMatch == null || !nameMatch.Success)) {
                        nameMatch = PythonCompiledRegex.Match(name);
                    }
                    return nameMatch != null && nameMatch.Success;
                } catch (RegexMatchTimeoutException) {
                    return false;
                }
            } else {
                var ext = name.Substring(name.LastIndexOf('.') + 1);
                return "py".Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                    "pyw".Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                    (allowCompiled && "pyd".Equals(ext, StringComparison.OrdinalIgnoreCase)) ||
                    (allowCache && (
                        "pyc".Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                        "pyo".Equals(ext, StringComparison.OrdinalIgnoreCase)
                    ));
            }
        }

        /// <summary>
        /// Returns true if the provided path is to an '__init__.py' file.
        /// Returns false if an invalid string is provided. This function does
        /// not raise exceptions.
        /// </summary>
        public static bool IsInitPyFile(string path) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }

            try {
                var name = PathUtils.GetFileName(path);
                return name.Equals("__init__.py", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("__init__.pyw", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("__init__.pyi", StringComparison.OrdinalIgnoreCase);
            } catch (ArgumentException) {
                return false;
            }
        }

        /// <summary>
        /// Returns a new ModulePath value determined from the provided full
        /// path to a Python file. This function will access the filesystem to
        /// determine the package name.
        /// </summary>
        /// <param name="path">
        /// The path referring to a Python file.
        /// </param>
        /// <exception cref="ArgumentException">
        /// path is not a valid Python module.
        /// </exception>
        public static ModulePath FromFullPath(string path) {
            return FromFullPath(path, null, null);
        }

        /// <summary>
        /// Returns a new ModulePath value determined from the provided full
        /// path to a Python file. This function will access the filesystem to
        /// determine the package name.
        /// </summary>
        /// <param name="path">
        /// The path referring to a Python file.
        /// </param>
        /// <param name="topLevelPath">
        /// The directory to stop searching for packages at. The module name
        /// will never include the last segment of this path.
        /// </param>
        /// <exception cref="ArgumentException">
        /// path is not a valid Python module.
        /// </exception>
        /// <remarks>This overload </remarks>
        public static ModulePath FromFullPath(string path, string topLevelPath) {
            return FromFullPath(path, topLevelPath, null);
        }

        /// <summary>
        /// Returns a new ModulePath value determined from the provided full
        /// path to a Python file. This function may access the filesystem to
        /// determine the package name unless <paramref name="isPackage"/> is
        /// provided.
        /// </summary>
        /// <param name="path">
        /// The path referring to a Python file.
        /// </param>
        /// <param name="topLevelPath">
        /// The directory to stop searching for packages at. The module name
        /// will never include the last segment of this path.
        /// </param>
        /// <param name="isPackage">
        /// A predicate that determines whether the specified substring of
        /// <paramref name="path"/> represents a package. If omitted, the
        /// default behavior is to check for a file named "__init__.py" in the
        /// directory passed to the predicate.
        /// </param>
        /// <exception cref="ArgumentException">
        /// path is not a valid Python module.
        /// </exception>
        public static ModulePath FromFullPath(
            string path,
            string topLevelPath = null,
            Func<string, bool> isPackage = null
        ) {
            var name = PathUtils.GetFileName(path);
            var nameMatch = PythonFileRegex.Match(name);
            if (nameMatch == null || !nameMatch.Success) {
                nameMatch = PythonBinaryRegex.Match(name);
            }
            if (nameMatch == null || !nameMatch.Success) {
                throw new ArgumentException("Not a valid Python module: " + path);
            }

            var fullName = nameMatch.Groups["name"].Value;
            var remainder = Path.GetDirectoryName(path);
            if (isPackage == null) {
                // We know that f will always end with a directory separator,
                // so just concatenate.
                isPackage = f => File.Exists(f + "__init__.py") || File.Exists(f + "__init__.pyw") || File.Exists(f + "__init__.pyi");
            }

            while (
                PathEqualityComparer.IsValidPath(remainder) &&
                isPackage(PathUtils.EnsureEndSeparator(remainder)) &&
                (string.IsNullOrEmpty(topLevelPath) ||
                 PathEqualityComparer.Instance.StartsWith(remainder, topLevelPath, allowFullMatch: false))
            ) {
                fullName = PathUtils.GetFileName(remainder) + "." + fullName;
                remainder = Path.GetDirectoryName(remainder);
            }

            return new ModulePath(fullName, path, remainder);
        }

        /// <summary>
        /// Returns a new ModulePath value determined from the provided search
        /// path and module name, if the module exists. This function may access
        /// the filesystem to determine the package name unless
        /// <paramref name="isPackage"/> and <param name="getModule"/> are
        /// provided.
        /// </summary>
        /// <param name="basePath">
        /// The path referring to a directory to start searching in.
        /// </param>
        /// <param name="moduleName">
        /// The full name of the module. If the name resolves to a package,
        /// "__init__" is automatically appended to the resulting name.
        /// </param>
        /// <param name="isPackage">
        /// A predicate that determines whether the specified substring of
        /// <paramref name="path"/> represents a package. If omitted, the
        /// default behavior is to check for a file named "__init__.py" in the
        /// directory passed to the predicate.
        /// </param>
        /// <param name="getModule">
        /// A function that returns valid module paths given a directory and a
        /// module name. The module name does not include any extension.
        /// For example, given "C:\Spam" and "eggs", this function may return
        /// one of "C:\Spam\eggs.py", "C:\Spam\eggs\__init__.py",
        /// "C:\Spam\eggs_d.cp35-win32.pyd" or some other full path. Returns
        /// null if there is no module importable by that name.
        /// </param>
        /// <exception cref="ArgumentException">
        /// moduleName is not a valid Python module.
        /// </exception>
        public static ModulePath FromBasePathAndName(
            string basePath,
            string moduleName,
            bool requireInitPy,
            Func<string, bool> isPackage = null,
            Func<string, string, string> getModule = null
        ) {
            ModulePath module;
            string errorParameter;
            bool isInvalid, isMissing;
            if (FromBasePathAndName_NoThrow(
                basePath,
                moduleName,
                isPackage,
                getModule,
                requireInitPy,
                out module,
                out isInvalid,
                out isMissing,
                out errorParameter
            )) {
                return module;
            }

            if (isInvalid) {
                throw new ArgumentException("Not a valid Python package: " + errorParameter);
            }
            if (isMissing) {
                throw new ArgumentException("Python package not found: " + errorParameter);
            }
            throw new ArgumentException("Unknown error finding module");
        }

        internal static bool FromBasePathAndName_NoThrow(
            string basePath,
            string moduleName,
            bool requireInitPy,
            out ModulePath modulePath
        ) => FromBasePathAndName_NoThrow(basePath, moduleName, null, null, requireInitPy, out modulePath, out _, out _, out _);

        public static bool FromBasePathAndFile_NoThrow(
            string basePath,
            string sourceFile,
            out ModulePath modulePath
        ) => FromBasePathAndFile_NoThrow(basePath, sourceFile, null, out modulePath, out _, out _);

        private static bool IsModuleNameMatch(Regex regex, string path, string mod) {
            var m = regex.Match(PathUtils.GetFileName(path));
            if (!m.Success) {
                return false;
            }
            return m.Groups["name"].Value == mod;
        }

        internal static string GetPackageInitPy(string path) {
            if (!Directory.Exists(path)) {
                return null;
            }
            var package = Path.Combine(path, "__init__.py");
            if (File.Exists(package)) {
                return package;
            }
            package = Path.Combine(path, "__init__.pyw");
            if (File.Exists(package)) {
                return package;
            }
            package = Path.Combine(path, "__init__.pyi");
            if (File.Exists(package)) {
                return package;
            }
            return null;
        }


        internal static bool FromBasePathAndName_NoThrow(
            string basePath,
            string moduleName,
            Func<string, bool> isPackage,
            Func<string, string, string> getModule,
            bool requireInitPy,
            out ModulePath modulePath,
            out bool isInvalid,
            out bool isMissing,
            out string errorParameter
        ) {
            modulePath = default(ModulePath);
            isInvalid = false;
            isMissing = false;
            errorParameter = null;

            var bits = moduleName.Split('.');
            var lastBit = bits.Last();

            if (isPackage == null) {
                isPackage = f => !string.IsNullOrEmpty(GetPackageInitPy(f));
            }
            if (getModule == null) {
                getModule = (dir, mod) => {
                    var modPath = Path.Combine(dir, mod);
                    var pack = GetPackageInitPy(modPath);
                    if (pack == null && !requireInitPy && Directory.Exists(modPath)) {
                        return modPath;
                    } else if (File.Exists(pack)) {
                        return pack;
                    }
                    var mods = PathUtils.EnumerateFiles(dir, mod + "*", recurse: false).ToArray();
                    return mods.FirstOrDefault(p => IsModuleNameMatch(PythonStubRegex, p, mod)) ??
                        mods.FirstOrDefault(p => IsModuleNameMatch(PythonBinaryRegex, p, mod)) ??
                        mods.FirstOrDefault(p => IsModuleNameMatch(PythonFileRegex, p, mod));
                };
            }

            var path = basePath;
            bool allowStub = true;
            Match m;

            foreach (var bit in bits.Take(bits.Length - 1)) {
                m = PythonPackageRegex.Match(bit);
                if (!m.Success || (!allowStub && m.Groups["stubs"].Success)) {
                    isInvalid = true;
                    errorParameter = bit;
                    return false;
                }
                allowStub = false;

                if (string.IsNullOrEmpty(path)) {
                    path = bit;
                } else {
                    path = Path.Combine(path, bit);
                }
                if (!isPackage(path)) {
                    isMissing = true;
                    errorParameter = path;
                    return false;
                }
            }

            m = PythonPackageRegex.Match(lastBit);
            if (!m.Success || (!allowStub && m.Groups["stubs"].Success)) {
                isInvalid = true;
                errorParameter = moduleName;
                return false;
            }
            path = getModule(path, lastBit);
            if (string.IsNullOrEmpty(path)) {
                isMissing = true;
                errorParameter = moduleName;
                return false;
            }

            modulePath = new ModulePath(moduleName, path, basePath);
            return true;
        }

        internal static bool FromBasePathAndFile_NoThrow(
            string basePath,
            string sourceFile,
            Func<string, bool> isPackage,
            out ModulePath modulePath,
            out bool isInvalid,
            out bool isMissing
        ) {
            modulePath = default(ModulePath);
            isInvalid = false;
            isMissing = false;

            if (!PathEqualityComparer.Instance.StartsWith(sourceFile, basePath)) {
                return false;
            }

            if (isPackage == null) {
                isPackage = f => !string.IsNullOrEmpty(GetPackageInitPy(f));
            }

            var nameMatch = PythonFileRegex.Match(PathUtils.GetFileName(sourceFile));
            if (!nameMatch.Success) {
                isInvalid = true;
                return false;
            }
            var bits = new List<string> { nameMatch.Groups["name"].Value };

            var path = PathUtils.TrimEndSeparator(PathUtils.GetParent(sourceFile));
            bool lastWasStubs = false;

            while (PathEqualityComparer.Instance.StartsWith(path, basePath, allowFullMatch: false)) {
                if (!isPackage(path)) {
                    isMissing = true;
                    return false;
                }
                if (lastWasStubs) {
                    isInvalid = true;
                    return false;
                }

                var bit = PathUtils.GetFileName(path);
                var m = PythonPackageRegex.Match(bit);
                if (!m.Success) {
                    isInvalid = true;
                    return false;
                }
                lastWasStubs = m.Groups["stubs"].Success;
                bits.Add(PathUtils.GetFileName(path));
                path = PathUtils.TrimEndSeparator(PathUtils.GetParent(path));
            }

            if (!PathEqualityComparer.Instance.Equals(basePath, path)) {
                isMissing = true;
                return false;
            }

            var moduleName = string.Join(".", bits.AsEnumerable().Reverse());
            modulePath = new ModulePath(moduleName, sourceFile, basePath);

            return true;
        }


        internal static IEnumerable<string> GetParents(string name, bool includeFullName = true) {
            if (string.IsNullOrEmpty(name)) {
                yield break;
            }

            var sb = new StringBuilder();
            var parts = name.Split('.');
            if (!includeFullName && parts.Length > 0) {
                parts[parts.Length - 1] = null;
            }

            foreach (var bit in parts.TakeWhile(s => !string.IsNullOrEmpty(s))) {
                sb.Append(bit);
                yield return sb.ToString();
                sb.Append('.');
            }
        }
    }
}
