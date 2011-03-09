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
using System.IO;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.PythonTools.Project.Automation;

namespace Microsoft.PythonTools {
    internal static class CommonUtils {
        /// <summary>
        /// Returns absolute path of a given directory, always
        /// ending with '/'.
        /// </summary>
        public static string NormalizeDirectoryPath(string path) {
            string absPath = new Uri(path).LocalPath;
            if (!absPath.EndsWith("\\")) {
                absPath += "\\";
            }
            return absPath;
        }

        /// <summary>
        /// Return true if both paths represent the same directory.
        /// </summary>
        public static bool AreTheSameDirectories(string path1, string path2) {
            if (path1 == null || path2 == null) {
                return false;
            }
            return NormalizeDirectoryPath(path1) == NormalizeDirectoryPath(path2);
        }

        /// <summary>
        /// Tries to create friendly directory path: '.' if the same as base path,
        /// relative path if short, absolute path otherwise.
        /// </summary>
        public static string CreateFriendlyPath(string basePath, string path) {
            string normalizedBaseDir = NormalizeDirectoryPath(basePath);
            string normalizedDir = NormalizeDirectoryPath(path);
            return normalizedBaseDir == normalizedDir ? " . " :
                new DirectoryInfo(normalizedDir).Name;
        }
    }
}
