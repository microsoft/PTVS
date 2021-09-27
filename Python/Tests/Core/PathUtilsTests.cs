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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace PythonToolsTests
{
    [TestClass]
    public class PathUtilsTests
    {
        [ClassInitialize]
        public static void DoDeployment(TestContext context)
        {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestMakeUri()
        {
            Assert.AreEqual(@"C:\a\b\c\", PathUtils.MakeUri(@"C:\a\b\c", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"C:\a\b\c", PathUtils.MakeUri(@"C:\a\b\c", false, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"\\a\b\c\", PathUtils.MakeUri(@"\\a\b\c", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"\\a\b\c", PathUtils.MakeUri(@"\\a\b\c", false, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"ftp://me@a.net:123/b/c/", PathUtils.MakeUri(@"ftp://me@a.net:123/b/c", true, UriKind.Absolute).AbsoluteUri);
            Assert.AreEqual(@"ftp://me@a.net:123/b/c", PathUtils.MakeUri(@"ftp://me@a.net:123/b/c", false, UriKind.Absolute).AbsoluteUri);
            Assert.AreEqual(@"C:\a b c\d e f\g\", PathUtils.MakeUri(@"C:\a b c\d e f\g", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"C:\a b c\d e f\g", PathUtils.MakeUri(@"C:\a b c\d e f\g", false, UriKind.Absolute).LocalPath);

            Assert.AreEqual(@"C:\a\b\c\", PathUtils.MakeUri(@"C:\a\b\c\d\..", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"C:\a\b\c\e", PathUtils.MakeUri(@"C:\a\b\c\d\..\e", false, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"\\a\b\c\", PathUtils.MakeUri(@"\\a\b\c\d\..", true, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"\\a\b\c\e", PathUtils.MakeUri(@"\\a\b\c\d\..\e", false, UriKind.Absolute).LocalPath);
            Assert.AreEqual(@"ftp://me@a.net:123/b/c/", PathUtils.MakeUri(@"ftp://me@a.net:123/b/c/d/..", true, UriKind.Absolute).AbsoluteUri);
            Assert.AreEqual(@"ftp://me@a.net:123/b/c/e", PathUtils.MakeUri(@"ftp://me@a.net:123/b/c/d/../e", false, UriKind.Absolute).AbsoluteUri);

            Assert.IsTrue(PathUtils.MakeUri(@"C:\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsTrue(PathUtils.MakeUri(@"\\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsTrue(PathUtils.MakeUri(@"ftp://me@a.net:123/b/c", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);

            Assert.IsFalse(PathUtils.MakeUri(@"a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsFalse(PathUtils.MakeUri(@"\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsFalse(PathUtils.MakeUri(@".\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);
            Assert.IsFalse(PathUtils.MakeUri(@"..\a\b", false, UriKind.RelativeOrAbsolute).IsAbsoluteUri);

            Assert.IsTrue(PathUtils.MakeUri(@"C:\a\b", false, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsTrue(PathUtils.MakeUri(@"C:\a\b", true, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsTrue(PathUtils.MakeUri(@"\\a\b", false, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsTrue(PathUtils.MakeUri(@"\\a\b", true, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsFalse(PathUtils.MakeUri(@"ftp://me@a.net:123/b/c", false, UriKind.RelativeOrAbsolute).IsFile);
            Assert.IsFalse(PathUtils.MakeUri(@"ftp://me@a.net:123/b/c", true, UriKind.RelativeOrAbsolute).IsFile);

            Assert.AreEqual(@"..\a\b\c\", PathUtils.MakeUri(@"..\a\b\c", true, UriKind.Relative).ToString());
            Assert.AreEqual(@"..\a\b\c", PathUtils.MakeUri(@"..\a\b\c", false, UriKind.Relative).ToString());
            Assert.AreEqual(@"..\a b c\", PathUtils.MakeUri(@"..\a b c", true, UriKind.Relative).ToString());
            Assert.AreEqual(@"..\a b c", PathUtils.MakeUri(@"..\a b c", false, UriKind.Relative).ToString());
            Assert.AreEqual(@"../a/b/c\", PathUtils.MakeUri(@"../a/b/c", true, UriKind.Relative).ToString());
            Assert.AreEqual(@"../a/b/c", PathUtils.MakeUri(@"../a/b/c", false, UriKind.Relative).ToString());
            Assert.AreEqual(@"../a b c\", PathUtils.MakeUri(@"../a b c", true, UriKind.Relative).ToString());
            Assert.AreEqual(@"../a b c", PathUtils.MakeUri(@"../a b c", false, UriKind.Relative).ToString());
        }

        private static void AssertIsNotSameDirectory(string first, string second)
        {
            Assert.IsFalse(PathUtils.IsSameDirectory(first, second), string.Format("First: {0} Second: {1}", first, second));
            first = first.Replace("\\", "/");
            second = second.Replace("\\", "/");
            Assert.IsFalse(PathUtils.IsSameDirectory(first, second), string.Format("First: {0} Second: {1}", first, second));
        }

        private static void AssertIsSameDirectory(string first, string second)
        {
            Assert.IsTrue(PathUtils.IsSameDirectory(first, second), string.Format("First: {0} Second: {1}", first, second));
            first = first.Replace("\\", "/");
            second = second.Replace("\\", "/");
            Assert.IsTrue(PathUtils.IsSameDirectory(first, second), string.Format("First: {0} Second: {1}", first, second));
        }

        private static void AssertIsNotSamePath(string first, string second)
        {
            Assert.IsFalse(PathUtils.IsSamePath(first, second), string.Format("First: {0} Second: {1}", first, second));
            first = first.Replace("\\", "/");
            second = second.Replace("\\", "/");
            Assert.IsFalse(PathUtils.IsSamePath(first, second), string.Format("First: {0} Second: {1}", first, second));
        }

        private static void AssertIsSamePath(string first, string second)
        {
            Assert.IsTrue(PathUtils.IsSamePath(first, second), string.Format("First: {0} Second: {1}", first, second));
            first = first.Replace("\\", "/");
            second = second.Replace("\\", "/");
            Assert.IsTrue(PathUtils.IsSamePath(first, second), string.Format("First: {0} Second: {1}", first, second));
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestIsSamePath()
        {
            // These paths should all look like files. Separators are added to the end
            // to test the directory cases. Paths ending in "." or ".." are always directories,
            // and will fail the tests here.
            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c",
                @"a\b\.\c", @"a\b\c",
                @"a\b\d\..\c", @"a\b\c",
                @"a\b\c", @"a\..\a\b\..\b\c\..\c"
                ))
            {

                foreach (var root in new[] { @"C:\", @"\\pc\Share\", @"ftp://me@ftp.home.net/" })
                {
                    string first, second;
                    first = root + testCase.Item1;
                    second = root + testCase.Item2;
                    AssertIsSamePath(first, second);
                    AssertIsNotSamePath(first + "\\", second);
                    AssertIsNotSamePath(first, second + "\\");
                    AssertIsSamePath(first + "\\", second + "\\");
                    if (!root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase))
                    {
                        // Files are case-insensitive
                        AssertIsSamePath(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }
                    else
                    {
                        // FTP is case-sensitive
                        AssertIsNotSamePath(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }

                    AssertIsSameDirectory(first, second);
                    AssertIsSameDirectory(first + "\\", second);
                    AssertIsSameDirectory(first, second + "\\");
                    AssertIsSameDirectory(first + "\\", second + "\\");
                    if (!root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase))
                    {
                        // Files are case-insensitive
                        AssertIsSameDirectory(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }
                    else
                    {
                        // FTP is case-sensitive
                        AssertIsNotSameDirectory(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }
                }
            }

            // The first part always resolves to a directory, regardless of whether there
            // is a separator at the end.
            foreach (var testCase in Pairs(
                @"a\b\c\..", @"a\b",
                @"a\b\c\..\..", @"a"
                ))
            {
                foreach (var root in new[] { @"C:\", @"\\pc\Share\", @"ftp://me@example.com/" })
                {
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
                    if (!root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase))
                    {
                        // Files are case-insensitive
                        AssertIsSameDirectory(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }
                    else
                    {
                        // FTP is case-sensitive
                        AssertIsNotSameDirectory(first.ToLowerInvariant(), second.ToUpperInvariant());
                    }
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestCreateFriendlyDirectoryPath()
        {
            foreach (var testCase in Triples(
                @"C:\a\b", @"C:\", @"..\..",
                @"C:\a\b", @"C:\a", @"..",
                @"C:\a\b", @"C:\a\b", @".",
                @"C:\a\b", @"C:\a\b\c", @"c",
                @"C:\a\b", @"D:\a\b", @"D:\a\b",
                @"C:\a\b", @"C:\", @"..\..",

                @"\\pc\share\a\b", @"\\pc\share\", @"..\..",
                @"\\pc\share\a\b", @"\\pc\share\a", @"..",
                @"\\pc\share\a\b", @"\\pc\share\a\b", @".",
                @"\\pc\share\a\b", @"\\pc\share\a\b\c", @"c",
                @"\\pc\share\a\b", @"\\pc\othershare\a\b", @"..\..\..\othershare\a\b",

                @"ftp://me@example.com/a/b", @"ftp://me@example.com/", @"../..",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a", @"..",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/b", @".",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/b/c", @"c",
                @"ftp://me@example.com/a/b", @"ftp://me@another.example.com/a/b", @"ftp://me@another.example.com/a/b"
                ))
            {
                var expected = testCase.Item3;
                var actual = PathUtils.CreateFriendlyDirectoryPath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestCreateFriendlyFilePath()
        {
            foreach (var testCase in Triples(
                @"C:\a\b", @"C:\file.exe", @"..\..\file.exe",
                @"C:\a\b", @"C:\a\file.exe", @"..\file.exe",
                @"C:\a\b", @"C:\a\b\file.exe", @"file.exe",
                @"C:\a\b", @"C:\a\b\c\file.exe", @"c\file.exe",
                @"C:\a\b", @"D:\a\b\file.exe", @"D:\a\b\file.exe",

                @"\\pc\share\a\b", @"\\pc\share\file.exe", @"..\..\file.exe",
                @"\\pc\share\a\b", @"\\pc\share\a\file.exe", @"..\file.exe",
                @"\\pc\share\a\b", @"\\pc\share\a\b\file.exe", @"file.exe",
                @"\\pc\share\a\b", @"\\pc\share\a\b\c\file.exe", @"c\file.exe",
                @"\\pc\share\a\b", @"\\pc\othershare\a\b\file.exe", @"..\..\..\othershare\a\b\file.exe",

                @"ftp://me@example.com/a/b", @"ftp://me@example.com/file.exe", @"../../file.exe",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/file.exe", @"../file.exe",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/b/file.exe", @"file.exe",
                @"ftp://me@example.com/a/b", @"ftp://me@example.com/a/b/c/file.exe", @"c/file.exe",
                @"ftp://me@example.com/a/b", @"ftp://me@another.example.com/a/b/file.exe", @"ftp://me@another.example.com/a/b/file.exe"
                ))
            {
                var expected = testCase.Item3;
                var actual = PathUtils.CreateFriendlyFilePath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestGetRelativeDirectoryPath()
        {
            foreach (var testCase in Triples(
                @"C:\a\b", @"C:\", @"..\..\",
                @"C:\a\b", @"C:\a", @"..\",
                @"C:\a\b\c", @"C:\a", @"..\..\",
                @"C:\a\b", @"C:\a\b", @"",
                @"C:\a\b", @"C:\a\b\c", @"c\",
                @"C:\a\b", @"D:\a\b", @"D:\a\b\",
                @"C:\a\b", @"C:\d\e", @"..\..\d\e\",

                @"\\root\share\path", @"\\Root\Share", @"..\",
                @"\\root\share\path", @"\\Root\share\Path\subpath", @"subpath\",
                @"\\root\share\path\subpath", @"\\Root\share\Path\othersubpath", @"..\othersubpath\",
                @"\\root\share\path", @"\\root\othershare\path", @"..\..\othershare\path\",
                @"\\root\share\path", @"\\root\share\otherpath\", @"..\otherpath\",

                @"ftp://me@example.com/share/path", @"ftp://me@example.com/", @"../../",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/Share", @"../../Share/",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/share/path/subpath", @"subpath/",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/share/Path/subpath", @"../Path/subpath/",
                @"ftp://me@example.com/share/path/subpath", @"ftp://me@example.com/share/path/othersubpath", @"../othersubpath/",
                @"ftp://me@example.com/share/path/subpath", @"ftp://me@example.com/share/Path/othersubpath", @"../../Path/othersubpath/",
                @"ftp://me@example.com/path", @"ftp://me@example.com/otherpath/", @"../otherpath/",

                @"C:\a\b\c\d", @"C:\.dottedname", @"..\..\..\..\.dottedname\",
                @"C:\a\b\c\d", @"C:\..dottedname", @"..\..\..\..\..dottedname\",
                @"C:\a\b\c\d", @"C:\a\.dottedname", @"..\..\..\.dottedname\",
                @"C:\a\b\c\d", @"C:\a\..dottedname", @"..\..\..\..dottedname\",

                "C:\\a\\b\\", @"C:\a\b", @"",
                @"C:\a\b", "C:\\a\\b\\", @""
            ))
            {
                var expected = testCase.Item3;
                var actual = PathUtils.GetRelativeDirectoryPath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual, string.Format("From {0} to {1}", testCase.Item1, testCase.Item2));
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestGetRelativeFilePath()
        {
            foreach (var testCase in Triples(
                @"C:\a\b", @"C:\file.exe", @"..\..\file.exe",
                @"C:\a\b", @"C:\a\file.exe", @"..\file.exe",
                @"C:\a\b\c", @"C:\a\file.exe", @"..\..\file.exe",
                @"C:\a\b", @"C:\A\B\file.exe", @"file.exe",
                @"C:\a\b", @"C:\a\B\C\file.exe", @"C\file.exe",
                @"C:\a\b", @"D:\a\b\file.exe", @"D:\a\b\file.exe",
                @"C:\a\b", @"C:\d\e\file.exe", @"..\..\d\e\file.exe",

                @"\\root\share\path", @"\\Root\Share\file.exe", @"..\file.exe",
                @"\\root\share\path", @"\\Root\Share\Path\file.exe", @"file.exe",
                @"\\root\share\path", @"\\Root\share\Path\subpath\file.exe", @"subpath\file.exe",
                @"\\root\share\path\subpath", @"\\Root\share\Path\othersubpath\file.exe", @"..\othersubpath\file.exe",
                @"\\root\share\path", @"\\root\othershare\path\file.exe", @"..\..\othershare\path\file.exe",
                @"\\root\share\path", @"\\root\share\otherpath\file.exe", @"..\otherpath\file.exe",
                @"\\root\share\", @"\\otherroot\share\file.exe", @"\\otherroot\share\file.exe",

                @"ftp://me@example.com/share/path", @"ftp://me@example.com/file.exe", @"../../file.exe",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/Share/file.exe", @"../../Share/file.exe",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/share/path/subpath/file.exe", @"subpath/file.exe",
                @"ftp://me@example.com/share/path", @"ftp://me@example.com/share/Path/subpath/file.exe", @"../Path/subpath/file.exe",
                @"ftp://me@example.com/share/path/subpath", @"ftp://me@example.com/share/path/othersubpath/file.exe", @"../othersubpath/file.exe",
                @"ftp://me@example.com/share/path/subpath", @"ftp://me@example.com/share/Path/othersubpath/file.exe", @"../../Path/othersubpath/file.exe",
                @"ftp://me@example.com/path", @"ftp://me@example.com/otherpath/file.exe", @"../otherpath/file.exe",

                @"C:\a\b", "C:\\a\\b\\", @"",
                // This is the expected behavior for GetRelativeFilePath
                // because the 'b' in the second part is assumed to be a file
                // and hence may be different to the directory 'b' in the first
                // part.
                // GetRelativeDirectoryPath returns an empty string, because it
                // assumes that both paths are directories.
                "C:\\a\\b\\", @"C:\a\b", @"..\b",

                // Ensure end-separators are retained when the target is a
                // directory rather than a file.
                "C:\\a\\", "C:\\a\\b\\", "b\\",
                "C:\\a", "C:\\a\\b\\", "b\\"
                ))
            {
                var expected = testCase.Item3;
                var actual = PathUtils.GetRelativeFilePath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual, string.Format("From {0} to {1}", testCase.Item1, testCase.Item2));
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestGetAbsoluteDirectoryPath()
        {
            foreach (var testCase in Triples(
                @"C:\a\b", @"\", @"C:\",
                @"C:\a\b", @"..\", @"C:\a\",
                @"C:\a\b", @"", @"C:\a\b\",
                @"C:\a\b", @".", @"C:\a\b\",
                @"C:\a\b", @"c", @"C:\a\b\c\",
                @"C:\a\b", @"D:\a\b", @"D:\a\b\",
                @"C:\a\b", @"\d\e", @"C:\d\e\",
                @"C:\a\b\c\d", @"..\..\..\..", @"C:\",

                @"\\root\share\path", @"..", @"\\root\share\",
                @"\\root\share\path", @"subpath", @"\\root\share\path\subpath\",
                @"\\root\share\path", @"..\otherpath\", @"\\root\share\otherpath\",

                @"ftp://me@example.com/path", @"..", @"ftp://me@example.com/",
                @"ftp://me@example.com/path", @"subpath", @"ftp://me@example.com/path/subpath/",
                @"ftp://me@example.com/path", @"../otherpath/", @"ftp://me@example.com/otherpath/"
            ))
            {
                var expected = testCase.Item3;
                var actual = PathUtils.GetAbsoluteDirectoryPath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestGetAbsoluteFilePath()
        {
            foreach (var testCase in Triples(
                @"C:\a\b", @"\file.exe", @"C:\file.exe",
                @"C:\a\b", @"..\file.exe", @"C:\a\file.exe",
                @"C:\a\b", @"file.exe", @"C:\a\b\file.exe",
                @"C:\a\b", @"c\file.exe", @"C:\a\b\c\file.exe",
                @"C:\a\b", @"D:\a\b\file.exe", @"D:\a\b\file.exe",
                @"C:\a\b", @"\d\e\file.exe", @"C:\d\e\file.exe",
                @"C:\a\b\c\d\", @"..\..\..\..\", @"C:\",

                @"\\root\share\path", @"..\file.exe", @"\\root\share\file.exe",
                @"\\root\share\path", @"file.exe", @"\\root\share\path\file.exe",
                @"\\root\share\path", @"subpath\file.exe", @"\\root\share\path\subpath\file.exe",
                @"\\root\share\path", @"..\otherpath\file.exe", @"\\root\share\otherpath\file.exe",

                @"ftp://me@example.com/path", @"../file.exe", @"ftp://me@example.com/file.exe",
                @"ftp://me@example.com/path", @"file.exe", @"ftp://me@example.com/path/file.exe",
                @"ftp://me@example.com/path", @"subpath/file.exe", @"ftp://me@example.com/path/subpath/file.exe",
                @"ftp://me@example.com/path", @"../otherpath/file.exe", @"ftp://me@example.com/otherpath/file.exe"
            ))
            {
                var expected = testCase.Item3;
                var actual = PathUtils.GetAbsoluteFilePath(testCase.Item1, testCase.Item2);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestNormalizeDirectoryPath()
        {
            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c\",
                @"a\b\.\c", @"a\b\c\",
                @"a\b\d\..\c", @"a\b\c\",
                @"a\b\\c", @"a\b\c\"
            ))
            {
                foreach (var root in new[] { "", @".\", @"..\", @"\" })
                {
                    var expected = (root == @".\" ? "" : root) + testCase.Item2;
                    var actual = PathUtils.NormalizeDirectoryPath(root + testCase.Item1);

                    Assert.AreEqual(expected, actual);
                }
            }

            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c\",
                @"a\b\.\c", @"a\b\c\",
                @"a\b\d\..\c", @"a\b\c\",
                @"a\..\..\b", @"b\"
            ))
            {
                foreach (var root in new[] { @"C:\", @"\\pc\share\", @"ftp://me@example.com/" })
                {
                    var expected = root + testCase.Item2;
                    var actual = PathUtils.NormalizeDirectoryPath(root + testCase.Item1);
                    if (root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase))
                    {
                        expected = expected.Replace('\\', '/');
                    }

                    Assert.AreEqual(expected, actual);

                    actual = PathUtils.NormalizeDirectoryPath(root + testCase.Item1 + @"\");

                    Assert.AreEqual(expected, actual);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestNormalizePath()
        {
            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c",
                @"a\b\.\c", @"a\b\c",
                @"a\b\d\..\c", @"a\b\c",
                @"a\b\\c", @"a\b\c"
            ))
            {
                foreach (var root in new[] { "", @".\", @"..\", @"\" })
                {
                    var expected = (root == @".\" ? "" : root) + testCase.Item2;
                    var actual = PathUtils.NormalizePath(root + testCase.Item1);

                    Assert.AreEqual(expected, actual);

                    expected += @"\";
                    actual = PathUtils.NormalizePath(root + testCase.Item1 + @"\");

                    Assert.AreEqual(expected, actual);
                }
            }

            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\c",
                @"a\b\.\c", @"a\b\c",
                @"a\b\d\..\c", @"a\b\c",
                @"a\..\..\b", @"b"
            ))
            {
                foreach (var root in new[] { @"C:\", @"\\pc\share\", @"ftp://me@example.com/" })
                {
                    var expected = root + testCase.Item2;
                    var actual = PathUtils.NormalizePath(root + testCase.Item1);
                    if (root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase))
                    {
                        expected = expected.Replace('\\', '/');
                    }

                    Assert.AreEqual(expected, actual);

                    expected += @"\";
                    actual = PathUtils.NormalizePath(root + testCase.Item1 + @"\");
                    if (root.StartsWith("ftp", StringComparison.OrdinalIgnoreCase))
                    {
                        expected = expected.Replace('\\', '/');
                    }

                    Assert.AreEqual(expected, actual);
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestTrimEndSeparator()
        {
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
                @"wit\hin", @"wit\hin",
                @"C:\a\", @"C:\a",
                @"C:\", @"C:\",
                @"ftp://a/", @"ftp://a",
                @"ftp://", @"ftp://"
            ))
            {
                var expected = testCase.Item2;
                var actual = PathUtils.TrimEndSeparator(testCase.Item1);

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestIsSubpathOf()
        {
            // Positive tests
            foreach (var testCase in Pairs(
                @"C:\a\b", @"C:\A\B",
                @"C:\a\b\", @"C:\A\B",      // IsSubpathOf has a special case for this
                @"C:\a\b", @"C:\A\B\C",
                @"C:\a\b\", @"C:\a\b\c",    // IsSubpathOf has a quick path for this
                @"C:\a\b\", @"C:\A\B\C",    // Quick path should not be taken
                @"C:", @"C:\a\b\",
                @"C:\a\b", @"C:\A\X\..\B\C"
                ))
            {
                Assert.IsTrue(PathUtils.IsSubpathOf(testCase.Item1, testCase.Item2), string.Format("{0} should be subpath of {1}", testCase.Item2, testCase.Item1));
            }

            // Negative tests
            foreach (var testCase in Pairs(
                @"C:\a\b\c", @"C:\A\B",
                @"C:\a\bcd", @"c:\a\b\cd",  // Quick path should not be taken
                @"C:\a\bcd", @"C:\A\B\CD",  // Quick path should not be taken
                @"C:\a\b", @"D:\A\B\C",
                @"C:\a\b\c", @"C:\B\A\C\D",
                @"C:\a\b\", @"C:\a\b\..\x\c" // Quick path should not be taken
                ))
            {
                Assert.IsFalse(PathUtils.IsSubpathOf(testCase.Item1, testCase.Item2), string.Format("{0} should not be subpath of {1}", testCase.Item2, testCase.Item1));
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestGetLastDirectoryName()
        {
            foreach (var testCase in Pairs(
                @"a\b\c", "b",
                @"a\b\", "b",
                @"a\b\c\", "c",
                @"a\b\.\c", "b",
                @"a\b\.\.\.\.\.\.\c", "b"
            ))
            {
                foreach (var scheme in new[] { "", "C:\\", "\\", ".\\", "\\\\share\\root\\", "ftp://" })
                {
                    var path = scheme + testCase.Item1;

                    Assert.AreEqual(testCase.Item2, PathUtils.GetLastDirectoryName(path), "Path: " + path);

                    if (path.IndexOf('.') >= 0)
                    {
                        // Path.GetFileName will always fail on these, so don't
                        // even bother testing.
                        continue;
                    }

                    string ioPathResult;
                    try
                    {
                        ioPathResult = Path.GetFileName(PathUtils.TrimEndSeparator(Path.GetDirectoryName(path)));
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    Assert.AreEqual(
                        PathUtils.GetLastDirectoryName(path),
                        ioPathResult ?? string.Empty,
                        "Did not match Path.GetFileName(...) result for " + path
                    );
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestGetParentOfDirectory()
        {
            foreach (var testCase in Pairs(
                @"a\b\c", @"a\b\",
                @"a\b\", @"a\",
                @"a\b\c\", @"a\b\"
            ))
            {
                foreach (var scheme in new[] { "", "C:\\", "\\", ".\\", "\\\\share\\root\\", "ftp://" })
                {
                    var path = scheme + testCase.Item1;
                    var expected = scheme + testCase.Item2;

                    Assert.AreEqual(expected, PathUtils.GetParent(path), "Path: " + path);

                    if (scheme.Contains("://"))
                    {
                        // Path.GetFileName will always fail on these, so don't
                        // even bother testing.
                        continue;
                    }

                    string ioPathResult;
                    try
                    {
                        ioPathResult = Path.GetDirectoryName(PathUtils.TrimEndSeparator(path)) + Path.DirectorySeparatorChar;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    Assert.AreEqual(
                        PathUtils.GetParent(path),
                        ioPathResult ?? string.Empty,
                        "Did not match Path.GetDirectoryName(...) result for " + path
                    );
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestGetFileOrDirectoryName()
        {
            foreach (var testCase in Pairs(
                @"a\b\c", @"c",
                @"a\b\", @"b",
                @"a\b\c\", @"c",
                @"a", @"a",
                @"a\", @"a"
            ))
            {
                foreach (var scheme in new[] { "", "C:\\", "\\", ".\\", "\\\\share\\root\\", "ftp://" })
                {
                    var path = scheme + testCase.Item1;
                    var expected = testCase.Item2;

                    Assert.AreEqual(expected, PathUtils.GetFileOrDirectoryName(path), "Path: " + path);

                    if (scheme.Contains("://"))
                    {
                        // Path.GetFileName will always fail on these, so don't
                        // even bother testing.
                        continue;
                    }

                    string ioPathResult;
                    try
                    {
                        ioPathResult = PathUtils.GetFileOrDirectoryName(path);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    Assert.AreEqual(
                        PathUtils.GetFileOrDirectoryName(path),
                        ioPathResult ?? string.Empty,
                        "Did not match Path.GetDirectoryName(...) result for " + path
                    );
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestEnumerateDirectories()
        {
            // Use "Windows", as we won't be able to enumerate everything in
            // here, but we should still not crash.
            var windows = Environment.GetEnvironmentVariable("SYSTEMROOT");
            var dirs = PathUtils.EnumerateDirectories(windows).ToList();
            Assert.AreNotEqual(0, dirs.Count);

            // Expect all paths to be rooted
            AssertUtil.ContainsExactly(dirs.Where(d => !Path.IsPathRooted(d)));
            // Expect all paths to be within Windows
            AssertUtil.ContainsExactly(dirs.Where(d => !PathUtils.IsSubpathOf(windows, d)));

            dirs = PathUtils.EnumerateDirectories(windows, recurse: false, fullPaths: false).ToList();
            Assert.AreNotEqual(0, dirs.Count);

            // Expect all paths to be relative
            AssertUtil.ContainsExactly(dirs.Where(d => Path.IsPathRooted(d)));
            // Expect all paths to be within Windows
            AssertUtil.ContainsExactly(dirs.Where(d => !Directory.Exists(Path.Combine(windows, d))));
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestEnumerateFiles()
        {
            // Use "Windows", as we won't be able to enumerate everything in
            // here, but we should still not crash.
            var windows = Environment.GetEnvironmentVariable("SYSTEMROOT");
            var files = PathUtils.EnumerateFiles(windows).ToList();
            Assert.AreNotEqual(0, files.Count);

            // Expect all paths to be rooted
            AssertUtil.ContainsExactly(files.Where(f => !Path.IsPathRooted(f)));
            // Expect all paths to be within Windows
            AssertUtil.ContainsExactly(files.Where(f => !PathUtils.IsSubpathOf(windows, f)));
            // Expect multiple extensions
            Assert.AreNotEqual(1, files.Select(f => Path.GetExtension(f)).ToSet().Count);

            files = PathUtils.EnumerateFiles(windows, recurse: false, fullPaths: false).ToList();
            Assert.AreNotEqual(0, files.Count);

            // Expect all paths to be relative
            AssertUtil.ContainsExactly(files.Where(f => Path.IsPathRooted(f)));
            // Expect all paths to be only filenames
            AssertUtil.ContainsExactly(files.Where(f => f.IndexOfAny(new[] { '\\', '/' }) >= 0));
            // Expect all paths to be within Windows
            AssertUtil.ContainsExactly(files.Where(f => !File.Exists(Path.Combine(windows, f))));

            files = PathUtils.EnumerateFiles(windows, "*.exe", recurse: false, fullPaths: false).ToList();
            Assert.AreNotEqual(0, files.Count);

            // Expect all paths to be relative
            AssertUtil.ContainsExactly(files.Where(f => Path.IsPathRooted(f)));
            // Expect all paths to be within Windows
            AssertUtil.ContainsExactly(files.Where(f => !File.Exists(Path.Combine(windows, f))));
            // Expect only one extension
            AssertUtil.ContainsExactly(files.Select(f => Path.GetExtension(f).ToLowerInvariant()).ToSet(), ".exe");
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void TestFindFile()
        {
            var root = TestData.GetPath("TestData");
            // File is too deep - should not find
            Assert.IsNull(PathUtils.FindFile(root, "9E90EF25FCE648B397D0CCEC67305A68.txt", depthLimit: 0));

            // Find file with BFS
            Assert.IsNotNull(PathUtils.FindFile(root, "9E90EF25FCE648B397D0CCEC67305A68.txt", depthLimit: 1));

            // Find file with correct firstCheck
            Assert.IsNotNull(PathUtils.FindFile(root, "9E90EF25FCE648B397D0CCEC67305A68.txt", depthLimit: 1, firstCheck: new[] { "FindFile" }));

            // Find file with incorrect firstCheck
            Assert.IsNotNull(PathUtils.FindFile(root, "9E90EF25FCE648B397D0CCEC67305A68.txt", depthLimit: 1, firstCheck: new[] { "FindFileX" }));

            // File is too deep
            Assert.IsNull(PathUtils.FindFile(root, "D75BD8CE1BBA41A7A2547CF3652AD3AF.txt", depthLimit: 0));
            Assert.IsNull(PathUtils.FindFile(root, "D75BD8CE1BBA41A7A2547CF3652AD3AF.txt", depthLimit: 1));
            Assert.IsNull(PathUtils.FindFile(root, "D75BD8CE1BBA41A7A2547CF3652AD3AF.txt", depthLimit: 2));
            Assert.IsNull(PathUtils.FindFile(root, "D75BD8CE1BBA41A7A2547CF3652AD3AF.txt", depthLimit: 3));

            // File is found
            Assert.IsNotNull(PathUtils.FindFile(root, "D75BD8CE1BBA41A7A2547CF3652AD3AF.txt", depthLimit: 4));
        }

        private IEnumerable<Tuple<string, string>> Pairs(params string[] items)
        {
            using (var e = items.Cast<string>().GetEnumerator())
            {
                while (e.MoveNext())
                {
                    var first = e.Current;
                    if (!e.MoveNext())
                    {
                        yield break;
                    }
                    var second = e.Current;

                    yield return new Tuple<string, string>(first, second);
                }
            }
        }

        private IEnumerable<Tuple<string, string, string>> Triples(params string[] items)
        {
            using (var e = items.Cast<string>().GetEnumerator())
            {
                while (e.MoveNext())
                {
                    var first = e.Current;
                    if (!e.MoveNext())
                    {
                        yield break;
                    }
                    var second = e.Current;
                    if (!e.MoveNext())
                    {
                        yield break;
                    }
                    var third = e.Current;

                    yield return new Tuple<string, string, string>(first, second, third);
                }
            }
        }
    }
}
