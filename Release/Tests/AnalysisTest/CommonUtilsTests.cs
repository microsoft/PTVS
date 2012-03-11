/* ****************************************************************************
 *
 * Copyright (c) Steve Dower (Zooba). 
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
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PythonTools;

namespace AnalysisTest
{
    [TestClass]
    public class CommonUtilsTests
    {
        [TestMethod]
        public void TestMakeUri() {
            Assert.AreEqual(@"C:\a\b\c\", CommonUtils.MakeUri(@"C:\a\b\c", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"C:\a\b\c", CommonUtils.MakeUri(@"C:\a\b\c", false, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"\\a\b\c\", CommonUtils.MakeUri(@"\\a\b\c", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"\\a\b\c", CommonUtils.MakeUri(@"\\a\b\c", false, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"ftp://me@a.net:123/b/c/", CommonUtils.MakeUri(@"ftp://me@a.net:123/b/c", true, UriKind.Absolute).AbsoluteUri);
            Assert.AreEqual(@"ftp://me@a.net:123/b/c", CommonUtils.MakeUri(@"ftp://me@a.net:123/b/c", false, UriKind.Absolute).AbsoluteUri);
            Assert.AreEqual(@"C:\a b c\d e f\g\", CommonUtils.MakeUri(@"C:\a b c\d e f\g", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"C:\a b c\d e f\g", CommonUtils.MakeUri(@"C:\a b c\d e f\g", false, UriKind.Absolute).LocalPath);

            Assert.AreEqual(@"C:\a\b\c\", CommonUtils.MakeUri(@"C:\a\b\c\d\..", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"C:\a\b\c\e", CommonUtils.MakeUri(@"C:\a\b\c\d\..\e", false, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"\\a\b\c\", CommonUtils.MakeUri(@"\\a\b\c\d\..", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"\\a\b\c\e", CommonUtils.MakeUri(@"\\a\b\c\d\..\e", false, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"ftp://me@a.net:123/b/c/", CommonUtils.MakeUri(@"ftp://me@a.net:123/b/c/d/..", true, UriKind.Absolute).AbsoluteUri);
            Assert.AreEqual(@"ftp://me@a.net:123/b/c/e", CommonUtils.MakeUri(@"ftp://me@a.net:123/b/c/d/../e", false, UriKind.Absolute).AbsoluteUri);

            Assert.IsTrue(CommonUtils.MakeUri(@"C:\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsTrue(CommonUtils.MakeUri(@"\\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsTrue(CommonUtils.MakeUri(@"ftp://me@a.net:123/b/c", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            
            Assert.IsFalse(CommonUtils.MakeUri(@"a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsFalse(CommonUtils.MakeUri(@"\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsFalse(CommonUtils.MakeUri(@".\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsFalse(CommonUtils.MakeUri(@"..\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);

            Assert.IsTrue(CommonUtils.MakeUri(@"C:\a\b", false, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsTrue(CommonUtils.MakeUri(@"C:\a\b", true, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsTrue(CommonUtils.MakeUri(@"\\a\b", false, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsTrue(CommonUtils.MakeUri(@"\\a\b", true, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsFalse(CommonUtils.MakeUri(@"ftp://me@a.net:123/b/c", false, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsFalse(CommonUtils.MakeUri(@"ftp://me@a.net:123/b/c", true, UriKind.RelativeOrAbsolute).IsFile);

            Assert.AreEqual(@"..\a\b\c\", CommonUtils.MakeUri(@"..\a\b\c", true, UriKind.Relative).ToString());
            Assert.AreEqual(@"..\a\b\c", CommonUtils.MakeUri(@"..\a\b\c", false, UriKind.Relative).ToString());
            Assert.AreEqual(@"..\a b c\", CommonUtils.MakeUri(@"..\a b c", true, UriKind.Relative).ToString());
            Assert.AreEqual(@"..\a b c", CommonUtils.MakeUri(@"..\a b c", false, UriKind.Relative).ToString());
            Assert.AreEqual(@"../a/b/c\", CommonUtils.MakeUri(@"../a/b/c", true, UriKind.Relative).ToString());
            Assert.AreEqual(@"../a/b/c", CommonUtils.MakeUri(@"../a/b/c", false, UriKind.Relative).ToString());
            Assert.AreEqual(@"../a b c\", CommonUtils.MakeUri(@"../a b c", true, UriKind.Relative).ToString());
            Assert.AreEqual(@"../a b c", CommonUtils.MakeUri(@"../a b c", false, UriKind.Relative).ToString());
        }

        private static void AssertIsNotSameDirectory(string first, string second) {
            Assert.IsFalse(CommonUtils.IsSameDirectory(first, second), string.Format("First: {0} Second: {1}", first, second));
            first = first.Replace("\\", "/");
            second = second.Replace("\\", "/");
            Assert.IsFalse(CommonUtils.IsSameDirectory(first, second), string.Format("First: {0} Second: {1}", first, second));
        }

        private static void AssertIsSameDirectory(string first, string second) {
            Assert.IsTrue(CommonUtils.IsSameDirectory(first, second), string.Format("First: {0} Second: {1}", first, second));
            first = first.Replace("\\", "/");
            second = second.Replace("\\", "/");
            Assert.IsTrue(CommonUtils.IsSameDirectory(first, second), string.Format("First: {0} Second: {1}", first, second));
        }

        private static void AssertIsNotSamePath(string first, string second) {
            Assert.IsFalse(CommonUtils.IsSamePath(first, second), string.Format("First: {0} Second: {1}", first, second));
            first = first.Replace("\\", "/");
            second = second.Replace("\\", "/");
            Assert.IsFalse(CommonUtils.IsSamePath(first, second), string.Format("First: {0} Second: {1}", first, second));
        }

        private static void AssertIsSamePath(string first, string second) {
            Assert.IsTrue(CommonUtils.IsSamePath(first, second), string.Format("First: {0} Second: {1}", first, second));
            first = first.Replace("\\", "/");
            second = second.Replace("\\", "/");
            Assert.IsTrue(CommonUtils.IsSamePath(first, second), string.Format("First: {0} Second: {1}", first, second));
        }

        [TestMethod]
        public void TestIsSamePath() {
            // These paths should all look like files. Separators are added to the end
            // to test the directory cases. Paths ending in "." or ".." are always directories,
            // and will fail the tests here.
            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c",
                @"a\b\.\c", @"a\b\c",
                @"a\b\d\..\c", @"a\b\c",
                @"a\b\c", @"a\..\a\b\..\b\c\..\c"
                )) {

                foreach (var root in new[] { @"C:\", @"\\pc\Share\", @"ftp://me@ftp.home.net/" }) {
                    string first, second;
                    first = root + testCase.Item1;
                    second = root + testCase.Item2;
                    AssertIsSamePath(first, second);
                    AssertIsNotSamePath(first + "\\", second);
                    AssertIsNotSamePath(first, second + "\\");
                    AssertIsSamePath(first + "\\", second + "\\");
                    if (!root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase)) {
                        // Files are case-insensitive
                        AssertIsSamePath(first.ToLowerInvariant(), second.ToUpperInvariant());
                    } else {
                        // FTP is case-sensitive
                        AssertIsNotSamePath(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }

                    AssertIsSameDirectory(first, second);
                    AssertIsSameDirectory(first + "\\", second);
                    AssertIsSameDirectory(first, second + "\\");
                    AssertIsSameDirectory(first + "\\", second + "\\");
                    if (!root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase)) {
                        // Files are case-insensitive
                        AssertIsSameDirectory(first.ToLowerInvariant(), second.ToUpperInvariant());
                    } else {
                        // FTP is case-sensitive
                        AssertIsNotSameDirectory(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }
                }
            }

            // The first part always resolves to a directory, regardless of whether there
            // is a separator at the end.
            foreach(var testCase in Pairs(
                @"a\b\c\..", @"a\b",
                @"a\b\c\..\..", @"a"
                )) {
                foreach (var root in new[] { @"C:\", @"\\pc\Share\", @"ftp://me@example.com/" }) {
                    string first, second;
                    first = root + testCase.Item1;
                    second = root + testCase.Item2;
                    AssertIsNotSamePath(first, second);
                    AssertIsNotSamePath(first + "\\", second);
                    AssertIsSamePath(first, second + "\\");
                    AssertIsSamePath(first + "\\", second + "\\");
                    AssertIsNotSamePath(first.ToLowerInvariant(), second.ToUpperInvariant());

                    AssertIsSameDirectory(first, second);
                    AssertIsSameDirectory(first + "\\", second);
                    AssertIsSameDirectory(first, second + "\\");
                    AssertIsSameDirectory(first + "\\", second + "\\");
                    if (!root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase)) {
                        // Files are case-insensitive
                        AssertIsSameDirectory(first.ToLowerInvariant(), second.ToUpperInvariant());
                    } else {
                        // FTP is case-sensitive
                        AssertIsNotSameDirectory(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }
                }
            }
        }

        [TestMethod]
        public void TestCreateFriendlyDirectoryPath() {
            foreach (var testCase in Triples(
                @"C:\a\b", @"C:\", @"\",
                @"C:\a\b", @"C:\a", @"..",
                @"C:\a\b", @"C:\a\b", @".",
                @"C:\a\b", @"C:\a\b\c", @"c",
                @"C:\a\b", @"D:\a\b", @"D:\a\b",

                @"\\pc\share\a\b", @"\\pc\share\", @"\",
                @"\\pc\share\a\b", @"\\pc\share\a", @"..",
                @"\\pc\share\a\b", @"\\pc\share\a\b", @".",
                @"\\pc\share\a\b", @"\\pc\share\a\b\c", @"c",
                @"\\pc\share\a\b", @"\\pc\othershare\a\b", @"..\..\..\othershare\a\b",

                @"ftp://me@example.com/a/b", @"ftp://me@example.com/", @"/",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a", @"..",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/b", @".",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/b/c", @"c",
                @"ftp://me@example.com/a/b", @"ftp://me@another.example.com/a/b", @"ftp://me@another.example.com/a/b"
                )) {
                var expected = testCase.Item3;
                var actual = CommonUtils.CreateFriendlyDirectoryPath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TestCreateFriendlyFilePath() {
            foreach (var testCase in Triples(
                @"C:\a\b", @"C:\file.exe", @"\file.exe",
                @"C:\a\b", @"C:\a\file.exe", @"..\file.exe",
                @"C:\a\b", @"C:\a\b\file.exe", @"file.exe",
                @"C:\a\b", @"C:\a\b\c\file.exe", @"c\file.exe",
                @"C:\a\b", @"D:\a\b\file.exe", @"D:\a\b\file.exe",

                @"\\pc\share\a\b", @"\\pc\share\file.exe", @"\file.exe",
                @"\\pc\share\a\b", @"\\pc\share\a\file.exe", @"..\file.exe",
                @"\\pc\share\a\b", @"\\pc\share\a\b\file.exe", @"file.exe",
                @"\\pc\share\a\b", @"\\pc\share\a\b\c\file.exe", @"c\file.exe",
                @"\\pc\share\a\b", @"\\pc\othershare\a\b\file.exe", @"..\..\..\othershare\a\b\file.exe",

                @"ftp://me@example.com/a/b", @"ftp://me@example.com/file.exe", @"/file.exe",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/file.exe", @"../file.exe",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/b/file.exe", @"file.exe",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/b/c/file.exe", @"c/file.exe",
                @"ftp://me@example.com/a/b", @"ftp://me@another.example.com/a/b/file.exe", @"ftp://me@another.example.com/a/b/file.exe"
                )) {
                var expected = testCase.Item3;
                var actual = CommonUtils.CreateFriendlyFilePath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TestGetRelativeDirectoryPath() {
            foreach (var testCase in Triples(
                @"C:\a\b", @"C:\", @"\",
                @"C:\a\b", @"C:\a", @"..\",
                @"C:\a\b\c", @"C:\a", @"..\..\",
                @"C:\a\b", @"C:\a\b", @"",
                @"C:\a\b", @"C:\a\b\c", @"c\",
                @"C:\a\b", @"D:\a\b", @"D:\a\b\",
                @"C:\a\b", @"C:\d\e", @"\d\e\",

                @"\\root\share\path", @"\\Root\Share", @"\",
                @"\\root\share\path", @"\\Root\share\Path\subpath", @"subpath\",
                @"\\root\share\path\subpath", @"\\Root\share\Path\othersubpath", @"..\othersubpath\",
                @"\\root\share\path", @"\\root\othershare\path", @"..\..\othershare\path\",
                @"\\root\share\path", @"\\root\share\otherpath\", @"\otherpath\",

                @"ftp://me@example.com/share/path", @"ftp://me@example.com/", @"/",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/Share", @"/Share/",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/share/path/subpath", @"subpath/",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/share/Path/subpath", @"../Path/subpath/",
                @"ftp://me@example.com/share/path/subpath", @"ftp://me@example.com/share/path/othersubpath", @"../othersubpath/",
                @"ftp://me@example.com/share/path/subpath", @"ftp://me@example.com/share/Path/othersubpath", @"../../Path/othersubpath/",
                @"ftp://me@example.com/path", @"ftp://me@example.com/otherpath/", @"/otherpath/",

                @"C:\a\b\c\d", @"C:\.dottedname", @"\.dottedname\",
                @"C:\a\b\c\d", @"C:\..dottedname", @"\..dottedname\",
                @"C:\a\b\c\d", @"C:\a\.dottedname", @"..\..\..\.dottedname\",
                @"C:\a\b\c\d", @"C:\a\..dottedname", @"..\..\..\..dottedname\"
                )) {
                var expected = testCase.Item3;
                var actual = CommonUtils.GetRelativeDirectoryPath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TestGetRelativeFilePath() {
            foreach (var testCase in Triples(
                @"C:\a\b", @"C:\file.exe", @"\file.exe",
                @"C:\a\b", @"C:\a\file.exe", @"..\file.exe",
                @"C:\a\b\c", @"C:\a\file.exe", @"..\..\file.exe",
                @"C:\a\b", @"C:\A\B\file.exe", @"file.exe",
                @"C:\a\b", @"C:\a\B\C\file.exe", @"C\file.exe",
                @"C:\a\b", @"D:\a\b\file.exe", @"D:\a\b\file.exe",
                @"C:\a\b", @"C:\d\e\file.exe", @"\d\e\file.exe",

                @"\\root\share\path", @"\\Root\Share\file.exe", @"\file.exe",
                @"\\root\share\path", @"\\Root\Share\Path\file.exe", @"file.exe",
                @"\\root\share\path", @"\\Root\share\Path\subpath\file.exe", @"subpath\file.exe",
                @"\\root\share\path\subpath", @"\\Root\share\Path\othersubpath\file.exe", @"..\othersubpath\file.exe",
                @"\\root\share\path", @"\\root\othershare\path\file.exe", @"..\..\othershare\path\file.exe",
                @"\\root\share\path", @"\\root\share\otherpath\file.exe", @"\otherpath\file.exe",
                @"\\root\share\", @"\\otherroot\share\file.exe", @"\\otherroot\share\file.exe",

                @"ftp://me@example.com/share/path", @"ftp://me@example.com/file.exe", @"/file.exe",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/Share/file.exe", @"/Share/file.exe",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/share/path/subpath/file.exe", @"subpath/file.exe",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/share/Path/subpath/file.exe", @"../Path/subpath/file.exe",
                @"ftp://me@example.com/share/path/subpath", @"ftp://me@example.com/share/path/othersubpath/file.exe", @"../othersubpath/file.exe",
                @"ftp://me@example.com/share/path/subpath", @"ftp://me@example.com/share/Path/othersubpath/file.exe", @"../../Path/othersubpath/file.exe",
                @"ftp://me@example.com/path", @"ftp://me@example.com/otherpath/file.exe", @"/otherpath/file.exe"
                )) {
                var expected = testCase.Item3;
                var actual = CommonUtils.GetRelativeFilePath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TestGetAbsoluteDirectoryPath() {
            foreach (var testCase in Triples(
                @"C:\a\b", @"\", @"C:\",
                @"C:\a\b", @"..\", @"C:\a\",
                @"C:\a\b", @"", @"C:\a\b\",
                @"C:\a\b", @".", @"C:\a\b\",
                @"C:\a\b", @"c", @"C:\a\b\c\",
                @"C:\a\b", @"D:\a\b", @"D:\a\b\",
                @"C:\a\b", @"\d\e", @"C:\d\e\",

                @"\\root\share\path", @"..", @"\\root\share\",
                @"\\root\share\path", @"subpath", @"\\root\share\path\subpath\",
                @"\\root\share\path", @"..\otherpath\", @"\\root\share\otherpath\",

                @"ftp://me@example.com/path", @"..", @"ftp://me@example.com/",
                @"ftp://me@example.com/path", @"subpath", @"ftp://me@example.com/path/subpath/",
                @"ftp://me@example.com/path", @"../otherpath/", @"ftp://me@example.com/otherpath/"
                )) {
                var expected = testCase.Item3;
                var actual = CommonUtils.GetAbsoluteDirectoryPath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TestGetAbsoluteFilePath() {
            foreach (var testCase in Triples(
                @"C:\a\b", @"\file.exe", @"C:\file.exe",
                @"C:\a\b", @"..\file.exe", @"C:\a\file.exe",
                @"C:\a\b", @"file.exe", @"C:\a\b\file.exe",
                @"C:\a\b", @"c\file.exe", @"C:\a\b\c\file.exe",
                @"C:\a\b", @"D:\a\b\file.exe", @"D:\a\b\file.exe",
                @"C:\a\b", @"\d\e\file.exe", @"C:\d\e\file.exe",

                @"\\root\share\path", @"..\file.exe", @"\\root\share\file.exe",
                @"\\root\share\path", @"file.exe", @"\\root\share\path\file.exe",
                @"\\root\share\path", @"subpath\file.exe", @"\\root\share\path\subpath\file.exe",
                @"\\root\share\path", @"..\otherpath\file.exe", @"\\root\share\otherpath\file.exe",

                @"ftp://me@example.com/path", @"../file.exe", @"ftp://me@example.com/file.exe",
                @"ftp://me@example.com/path", @"file.exe", @"ftp://me@example.com/path/file.exe",
                @"ftp://me@example.com/path", @"subpath/file.exe", @"ftp://me@example.com/path/subpath/file.exe",
                @"ftp://me@example.com/path", @"../otherpath/file.exe", @"ftp://me@example.com/otherpath/file.exe"
                )) {
                var expected = testCase.Item3;
                var actual = CommonUtils.GetAbsoluteFilePath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TestNormalizeDirectoryPath() {
            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c\",
                @"a\b\.\c", @"a\b\.\c\",
                @"a\b\d\..\c", @"a\b\d\..\c\"
                )) {
                foreach (var root in new[] { "", @".\", @"..\", @"\" }) {
                    var expected = root + testCase.Item2;
                    var actual = CommonUtils.NormalizeDirectoryPath(root + testCase.Item1);

                    Assert.AreEqual(expected, actual);
                }
            }

            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c\",
                @"a\b\.\c", @"a\b\c\",
                @"a\b\d\..\c", @"a\b\c\"
                )) {
                foreach (var root in new[] { @"C:\", @"\\pc\share\", @"ftp://me@example.com/" }) {
                    var expected = root + testCase.Item2;
                    var actual = CommonUtils.NormalizeDirectoryPath(root + testCase.Item1);
                    if (root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase)) {
                        expected = expected.Replace('\\', '/');
                    }

                    Assert.AreEqual(expected, actual);

                    actual = CommonUtils.NormalizeDirectoryPath(root + testCase.Item1 + @"\");

                    Assert.AreEqual(expected, actual);
                }
            }
        }

        [TestMethod]
        public void TestNormalizePath() {
            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c",
                @"a\b\.\c", @"a\b\.\c",
                @"a\b\d\..\c", @"a\b\d\..\c"
                )) {
                foreach (var root in new[] { "", @".\", @"..\", @"\" }) {
                    var expected = root + testCase.Item2;
                    var actual = CommonUtils.NormalizePath(root + testCase.Item1);

                    Assert.AreEqual(expected, actual);

                    expected += @"\";
                    actual = CommonUtils.NormalizePath(root + testCase.Item1 + @"\");

                    Assert.AreEqual(expected, actual);
                }
            }

            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c",
                @"a\b\.\c", @"a\b\c",
                @"a\b\d\..\c", @"a\b\c"
                )) {
                foreach (var root in new[] { @"C:\", @"\\pc\share\", @"ftp://me@example.com/" }) {
                    var expected = root + testCase.Item2;
                    var actual = CommonUtils.NormalizePath(root + testCase.Item1);
                    if (root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase)) {
                        expected = expected.Replace('\\', '/');
                    }

                    Assert.AreEqual(expected, actual);

                    expected += @"\";
                    actual = CommonUtils.NormalizePath(root + testCase.Item1 + @"\");
                    if (root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase)) {
                        expected = expected.Replace('\\', '/');
                    }

                    Assert.AreEqual(expected, actual);
                }
            }
        }

        [TestMethod]
        public void TestTrimEndSeparator() {
            // TrimEndSeparator uses System.IO.Path.(Alt)DirectorySeparatorChar
            // Here we assume these are '\\' and '/'

            foreach (var testCase in Pairs(
                @"no separator", @"no separator",
                @"one slash/", @"one slash",
                @"two slashes//", @"two slashes/",
                @"one backslash\", @"one backslash",
                @"two backslashes\\", @"two backslashes\",
                @"mixed/\", @"mixed/",
                @"mixed\/", @"mixed\",
                @"/leading", @"/leading",
                @"\leading", @"\leading",
                @"wit/hin", @"wit/hin",
                @"wit\hin", @"wit\hin"
                )) {
                var expected = testCase.Item2;
                var actual = CommonUtils.TrimEndSeparator(testCase.Item1);

                Assert.AreEqual(expected, actual);
            }
        }

        private IEnumerable<Tuple<string, string>> Pairs(params string[] items) {
            using (var e = items.Cast<string>().GetEnumerator()) {
                while (e.MoveNext()) {
                    var first = e.Current;
                    if (!e.MoveNext()) {
                        yield break;
                    }
                    var second = e.Current;

                    yield return new Tuple<string, string>(first, second);
                }
            }
        }

        private IEnumerable<Tuple<string, string, string>> Triples(params string[] items) {
            using (var e = items.Cast<string>().GetEnumerator()) {
                while (e.MoveNext()) {
                    var first = e.Current;
                    if (!e.MoveNext()) {
                        yield break;
                    }
                    var second = e.Current;
                    if (!e.MoveNext()) {
                        yield break;
                    }
                    var third = e.Current;

                    yield return new Tuple<string, string, string>(first, second, third);
                }
            }
        }
    }
}
