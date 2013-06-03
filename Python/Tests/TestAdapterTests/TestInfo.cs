using System;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using TestUtilities;

namespace TestAdapterTests {
    class TestInfo {
        public string ClassName { get; private set; }
        public string MethodName { get; private set; }
        public string ClassFilePath { get; private set; }
        public string ProjectFilePath { get; private set; }
        public string SourceCodeFilePath { get; private set; }
        public int SourceCodeLineNumber { get; private set; }
        public TestOutcome Outcome { get; private set; }

        private TestInfo() {
        }

        public static TestInfo FromRelativePaths(string className, string methodName, string projectFilePath, string sourceCodeFilePath, int sourceCodeLineNumber, TestOutcome outcome, string classFilePath = null) {
            return FromAbsolutePaths(className,
                methodName,
                TestData.GetPath(projectFilePath),
                TestData.GetPath(sourceCodeFilePath),
                sourceCodeLineNumber,
                outcome,
                classFilePath != null ? TestData.GetPath(classFilePath) : null);
        }

        public static TestInfo FromAbsolutePaths(string className, string methodName, string projectFilePath, string sourceCodeFilePath, int sourceCodeLineNumber, TestOutcome outcome, string classFilePath = null) {
            TestInfo ti = new TestInfo();
            ti.ClassName = className;
            ti.MethodName = methodName;
            ti.ProjectFilePath = projectFilePath;
            ti.SourceCodeFilePath = sourceCodeFilePath;
            ti.SourceCodeLineNumber = sourceCodeLineNumber;
            ti.Outcome = outcome;
            if (classFilePath == null) {
                ti.ClassFilePath = sourceCodeFilePath;
            } else {
                ti.ClassFilePath = classFilePath;
            }
            return ti;
        }

        public TestCase TestCase {
            get {
                var expectedFullyQualifiedName = TestDiscoverer.MakeFullyQualifiedTestName(this.ClassFilePath, this.ClassName, this.MethodName);
                var tc = new TestCase(expectedFullyQualifiedName, new Uri(TestExecutor.ExecutorUriString), this.ProjectFilePath);
                tc.CodeFilePath = this.SourceCodeFilePath;
                tc.LineNumber = this.SourceCodeLineNumber;
                return tc;
            }
        }

        private static TestInfo BarSuccess = TestInfo.FromRelativePaths("BarTests", "test_calculate_pass", @"TestData\TestAdapterTestA\TestAdapterTestA.pyproj", @"TestData\TestAdapterTestA\BarTest.py", 5, TestOutcome.Passed);
        private static TestInfo BarFailure = TestInfo.FromRelativePaths("BarTests", "test_calculate_fail", @"TestData\TestAdapterTestA\TestAdapterTestA.pyproj", @"TestData\TestAdapterTestA\BarTest.py", 11, TestOutcome.Failed);
        private static TestInfo BaseSuccess = TestInfo.FromRelativePaths("BaseClassTests", "test_base_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceBaseTest.py", 4, TestOutcome.Passed);
        private static TestInfo BaseFailure = TestInfo.FromRelativePaths("BaseClassTests", "test_base_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceBaseTest.py", 7, TestOutcome.Failed);
        private static TestInfo DerivedBaseSuccess = TestInfo.FromRelativePaths("DerivedClassTests", "test_base_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceBaseTest.py", 4, TestOutcome.Passed, @"TestData\TestAdapterTestB\InheritanceDerivedTest.py");
        private static TestInfo DerivedBaseFailure = TestInfo.FromRelativePaths("DerivedClassTests", "test_base_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceBaseTest.py", 7, TestOutcome.Failed, @"TestData\TestAdapterTestB\InheritanceDerivedTest.py");
        private static TestInfo DerivedSuccess = TestInfo.FromRelativePaths("DerivedClassTests", "test_derived_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceDerivedTest.py", 5, TestOutcome.Passed);
        private static TestInfo DerivedFailure = TestInfo.FromRelativePaths("DerivedClassTests", "test_derived_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceDerivedTest.py", 8, TestOutcome.Failed);
        private static TestInfo RenamedImportSuccess = TestInfo.FromRelativePaths("RenamedImportTests", "test_renamed_import_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\RenameImportTest.py", 5, TestOutcome.Passed);
        private static TestInfo RenamedImportFailure = TestInfo.FromRelativePaths("RenamedImportTests", "test_renamed_import_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\RenameImportTest.py", 8, TestOutcome.Failed);
        private static TestInfo TimeoutSuccess = TestInfo.FromRelativePaths("TimeoutTest", "test_wait_10_secs", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\TimeoutTest.py", 5, TestOutcome.Passed);

        public static string TestAdapterAProjectFilePath = TestData.GetPath(@"TestData\TestAdapterTestA\TestAdapterTestA.pyproj");
        public static string TestAdapterBProjectFilePath = TestData.GetPath(@"TestData\TestAdapterTestB\TestAdapterTestB.pyproj");
        public static string TestAdapterLibProjectFilePath = TestData.GetPath(@"TestData\TestAdapterLibraryBar\TestAdapterLibraryBar.pyproj");

        public static TestInfo[] TestAdapterATests {
            get {
                return new TestInfo[] {
                    BarSuccess,
                    BarFailure,
                };
            }
        }

        public static TestInfo[] TestAdapterBTests {
            get {
                return new TestInfo[] {
                    BaseSuccess,
                    BaseFailure,
                    DerivedBaseSuccess,
                    DerivedBaseFailure,
                    DerivedSuccess,
                    DerivedFailure,
                    RenamedImportSuccess,
                    RenamedImportFailure,
                    TimeoutSuccess
                };
            }
        }
    }
}
