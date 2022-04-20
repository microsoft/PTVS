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
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Common.Core.Extensions;
using Microsoft.PythonTools.Common.Core.IO;

namespace Microsoft.PythonTools {
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
        public string Name => FullName.Substring(FullName.LastIndexOf('.') + 1);

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
                    var lastDot = FullName.LastIndexOf('.');
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

        /// <summary>
        /// Expands a sequence of directory paths to include any paths that are
        /// referenced in .pth files.
        /// 
        /// The original directories are not included in the result.
        /// </summary>
        public static IEnumerable<string> ExpandPathFiles(IFileSystem fileSystem, string path) {
            if (!Directory.Exists(path)) {
                yield break;
            }

            foreach (var file in PathUtils.EnumerateFiles(fileSystem, path, "*.pth", recurse: false)) {
                using (var reader = new StreamReader(file.FullName)) {
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
        public static bool IsPythonSourceFile(string path) => IsPythonFile(path, false, false, false);

        /// <summary>
        /// Returns true if the provided path references an importable Python
        /// module. This function does not access the filesystem.
        /// Returns false if an invalid string is provided. This function does
        /// not raise exceptions.
        /// </summary>
        public static bool IsPythonFile(string path) => IsPythonFile(path, true, true, true);

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
        /// <param name="topLevelPath">
        /// The directory to stop searching for packages at. The module name
        /// will never include the last segment of this path.
        /// </param>
        /// <exception cref="ArgumentException">
        /// path is not a valid Python module.
        /// </exception>
        /// <remarks>This overload </remarks>
        public static ModulePath FromFullPath(string path, string topLevelPath) => FromFullPath(path, topLevelPath, null);

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
    }
}
