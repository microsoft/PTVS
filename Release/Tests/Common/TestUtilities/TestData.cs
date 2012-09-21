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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools;

namespace TestUtilities {
    public static class TestData {
        const string BinariesSourcePath = @"Binaries\" +
#if DEBUG
            @"Debug" + 
#else
            @"Release" + 
#endif
#if DEV11
            @"11.0\";
#else
            @"10.0\";
#endif
        const string BinariesOutPath = "";

        const string DataSourcePath = @"Release\Tests\Common\TestData\";
        const string DataOutPath = @"TestData\";

        private static string GetSolutionDir() {
            var dir = Path.GetDirectoryName((typeof(TestData)).Assembly.Location);
            while (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !File.Exists(Path.Combine(dir, "PythonTools.sln"))) {
                dir = Path.GetDirectoryName(dir);
            }
            return dir;
        }

        private static void CopyFiles(string sourceDir, string destDir) {
            try {
                Directory.CreateDirectory(destDir);
            } catch (IOException) {
            }
            
            var newDirectories = new HashSet<string>(from d in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories)
                                                     where d.StartsWith(sourceDir)
                                                     select d.Substring(sourceDir.Length));
            newDirectories.ExceptWith(from d in Directory.EnumerateDirectories(destDir, "*", SearchOption.AllDirectories)
                                      where d.StartsWith(destDir)
                                      select d.Substring(destDir.Length));

            foreach (var newDir in newDirectories.OrderBy(i => i.Length).Select(i => Path.Combine(destDir, i))) {
                try {
                    Directory.CreateDirectory(newDir);
                } catch {
                    Debug.WriteLine("Failed to create directory " + newDir);
                }
            }

            var newFiles = new HashSet<string>(from f in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
                                               where f.StartsWith(sourceDir)
                                               select f.Substring(sourceDir.Length));
            newFiles.ExceptWith(from f in Directory.EnumerateFiles(destDir, "*", SearchOption.AllDirectories)
                                where f.StartsWith(destDir)
                                select f.Substring(destDir.Length));

            foreach (var newFile in newFiles) {
                var copyFrom = Path.Combine(sourceDir, newFile);
                var copyTo = Path.Combine(destDir, newFile);
                try {
                    File.Copy(copyFrom, copyTo);
                    File.SetAttributes(copyTo, FileAttributes.Normal);
                } catch {
                    Debug.WriteLine("Failed to copy " + copyFrom + " to " + copyTo);
                }
            }
        }

        public static void Deploy() {
            var sourceRoot = GetSolutionDir();
            var deployRoot = Path.GetDirectoryName((typeof(TestData)).Assembly.Location);

            var binSource = Path.Combine(sourceRoot, BinariesSourcePath);
            var binDest = Path.Combine(deployRoot, BinariesOutPath);
            if (binSource == binDest) {
                Debug.Fail("Select the default.testsettings file before running tests.");
            }

            CopyFiles(binSource, binDest);
            CopyFiles(Path.Combine(sourceRoot, DataSourcePath), Path.Combine(deployRoot, DataOutPath));
        }

        /// <summary>
        /// Returns the full path to the deployed file.
        /// </summary>
        public static string GetPath(string relativePath) {
            var testRoot = Path.GetDirectoryName((typeof(TestData)).Assembly.Location);
            return CommonUtils.GetAbsoluteFilePath(testRoot, relativePath);
        }

        /// <summary>
        /// Opens a FileStream for a file from the current deployment.
        /// </summary>
        public static FileStream Open(string relativePath, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, FileShare share = FileShare.Read) {
            return new FileStream(GetPath(relativePath), mode, access, share);
        }

        /// <summary>
        /// Opens a StreamReader for a file from the current deployment.
        /// </summary>
        public static StreamReader Read(string relativePath, Encoding encoding = null) {
            return new StreamReader(GetPath(relativePath), encoding ?? Encoding.Default);
        }
    }
}
