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

extern alias pt;
extern alias ta;
using System;
using System.IO;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using ta::Microsoft.VisualStudioTools;
using TestUtilities;
using PythonConstants = pt::Microsoft.PythonTools.PythonConstants;

namespace TestAdapterTests {
    class TestInfo {
        public string ClassName { get; private set; }
        public string MethodName { get; private set; }
        public string ClassFilePath { get; private set; }
        public string RelativeClassFilePath { get; private set; }
        public string ProjectFilePath { get; private set; }
        public string SourceCodeFilePath { get; private set; }
        public int SourceCodeLineNumber { get; private set; }
        public TestOutcome Outcome { get; private set; }
        public TimeSpan MinDuration { get; private set; }

        private TestInfo() {
        }

        public static TestInfo FromRelativePaths(string className, string methodName, string projectFilePath, string sourceCodeFilePath, int sourceCodeLineNumber, TestOutcome outcome, string classFilePath = null, TimeSpan? minDuration = null) {
            return FromAbsolutePaths(className,
                methodName,
                TestData.GetPath(projectFilePath),
                TestData.GetPath(sourceCodeFilePath),
                sourceCodeLineNumber,
                outcome,
                classFilePath != null ? TestData.GetPath(classFilePath) : null,
                minDuration);
        }

        public static TestInfo FromAbsolutePaths(string className, string methodName, string projectFilePath, string sourceCodeFilePath, int sourceCodeLineNumber, TestOutcome outcome, string classFilePath = null, TimeSpan? minDuration = null) {
            TestInfo ti = new TestInfo();
            ti.ClassName = className;
            ti.MethodName = methodName;
            ti.ProjectFilePath = projectFilePath;
            ti.SourceCodeFilePath = sourceCodeFilePath;
            ti.SourceCodeLineNumber = sourceCodeLineNumber;
            ti.Outcome = outcome;
            ti.ClassFilePath = classFilePath ?? sourceCodeFilePath;
            ti.RelativeClassFilePath = CommonUtils.GetRelativeFilePath(Path.GetDirectoryName(ti.ProjectFilePath), ti.ClassFilePath);
            ti.MinDuration = minDuration ?? TimeSpan.Zero;
            return ti;
        }

        public TestCase TestCase {
            get {
                var expectedFullyQualifiedName = TestReader.MakeFullyQualifiedTestName(RelativeClassFilePath, ClassName, MethodName);
                var tc = new TestCase(expectedFullyQualifiedName, new Uri(PythonConstants.TestExecutorUriString), this.ProjectFilePath);
                tc.CodeFilePath = SourceCodeFilePath;
                tc.LineNumber = SourceCodeLineNumber;
                return tc;
            }
        }

        private static TestInfo OarSuccess = TestInfo.FromRelativePaths("OarTests", "test_calculate_pass", @"TestData\TestAdapterTestA\TestAdapterTestA.pyproj", @"TestData\TestAdapterTestA\OarTest.py", 5, TestOutcome.Passed);
        private static TestInfo OarFailure = TestInfo.FromRelativePaths("OarTests", "test_calculate_fail", @"TestData\TestAdapterTestA\TestAdapterTestA.pyproj", @"TestData\TestAdapterTestA\OarTest.py", 11, TestOutcome.Failed);
        private static TestInfo BaseSuccess = TestInfo.FromRelativePaths("BaseClassTests", "test_base_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceBaseTest.py", 4, TestOutcome.Passed);
        private static TestInfo BaseFailure = TestInfo.FromRelativePaths("BaseClassTests", "test_base_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceBaseTest.py", 7, TestOutcome.Failed);
        private static TestInfo DerivedBaseSuccess = TestInfo.FromRelativePaths("DerivedClassTests", "test_base_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceBaseTest.py", 4, TestOutcome.Passed, @"TestData\TestAdapterTestB\InheritanceDerivedTest.py");
        private static TestInfo DerivedBaseFailure = TestInfo.FromRelativePaths("DerivedClassTests", "test_base_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceBaseTest.py", 7, TestOutcome.Failed, @"TestData\TestAdapterTestB\InheritanceDerivedTest.py");
        private static TestInfo DerivedSuccess = TestInfo.FromRelativePaths("DerivedClassTests", "test_derived_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceDerivedTest.py", 5, TestOutcome.Passed);
        private static TestInfo DerivedFailure = TestInfo.FromRelativePaths("DerivedClassTests", "test_derived_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\InheritanceDerivedTest.py", 8, TestOutcome.Failed);
        private static TestInfo RenamedImportSuccess = TestInfo.FromRelativePaths("RenamedImportTests", "test_renamed_import_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\RenameImportTest.py", 5, TestOutcome.Passed);
        private static TestInfo RenamedImportFailure = TestInfo.FromRelativePaths("RenamedImportTests", "test_renamed_import_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\RenameImportTest.py", 8, TestOutcome.Failed);
        private static TestInfo TimeoutSuccess = TestInfo.FromRelativePaths("TimeoutTest", "test_wait_10_secs", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\TimeoutTest.py", 5, TestOutcome.Passed);
        private static TestInfo TestInPackageSuccess = TestInfo.FromRelativePaths("TestsInPackage", "test_in_package_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\tests\TestsInPackage.py", 4, TestOutcome.Passed);
        private static TestInfo TestInPackageFailure = TestInfo.FromRelativePaths("TestsInPackage", "test_in_package_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\tests\TestsInPackage.py", 7, TestOutcome.Failed);
        private static TestInfo LinkedSuccess = TestInfo.FromRelativePaths("LinkedTests", "test_linked_pass", @"TestData\TestAdapterTestA\TestAdapterTestA.pyproj", @"TestData\TestAdapterTestB\LinkedTest.py", 4, TestOutcome.Passed);
        private static TestInfo LinkedFailure = TestInfo.FromRelativePaths("LinkedTests", "test_linked_fail", @"TestData\TestAdapterTestA\TestAdapterTestA.pyproj", @"TestData\TestAdapterTestB\LinkedTest.py", 7, TestOutcome.Failed);

        public static string TestAdapterAProjectFilePath = TestData.GetPath(@"TestData\TestAdapterTestA\TestAdapterTestA.pyproj");
        public static string TestAdapterBProjectFilePath = TestData.GetPath(@"TestData\TestAdapterTestB\TestAdapterTestB.pyproj");
        public static string TestAdapterLibProjectFilePath = TestData.GetPath(@"TestData\TestAdapterLibraryOar\TestAdapterLibraryOar.pyproj");

        public static TestInfo[] TestAdapterATests {
            get {
                return new TestInfo[] {
                    OarSuccess,
                    OarFailure,
                    LinkedSuccess,
                    LinkedFailure
                };
            }
        }

        public static TestInfo[] TestAdapterBTests {
            get {
                return new TestInfo[] {
                    RenamedImportSuccess,
                    RenamedImportFailure,
                    TimeoutSuccess,
                    TestInPackageSuccess,
                    TestInPackageFailure
                };
            }
        }

        public static TestInfo[] TestAdapterBInheritanceTests {
            get {
                return new TestInfo[] {
                    BaseSuccess,
                    BaseFailure,
                    DerivedBaseSuccess,
                    DerivedBaseFailure,
                    DerivedSuccess,
                    DerivedFailure,
                };
            }
        }

        private static TestInfo MultiprocessingSuccess = TestInfo.FromRelativePaths("Multiprocessing", "test_pool", @"TestData\TestAdapterTests\Multiprocessing.pyproj", @"TestData\TestAdapterTests\Multiprocessing.py", 6, TestOutcome.Passed);

        public static string TestAdapterMultiprocessingProjectFilePath = TestData.GetPath(@"TestData\TestAdapterTests\Multiprocessing.pyproj");

        public static TestInfo[] TestAdapterMultiprocessingTests {
            get {
                return new[] { MultiprocessingSuccess };
            }
        }

        private static TestInfo GetLoadErrorImportError(string projectName) => TestInfo.FromRelativePaths("ImportErrorTests", "test_import_error", $"TestData\\TestAdapterTests\\{projectName}.pyproj", @"TestData\TestAdapterTests\LoadErrorTestImportError.py", 5, TestOutcome.Failed);
        private static TestInfo GetLoadErrorNoError(string projectName) => TestInfo.FromRelativePaths("NoErrorTests", "test_no_error", $"TestData\\TestAdapterTests\\{projectName}.pyproj", @"TestData\TestAdapterTests\LoadErrorTestNoError.py", 4, TestOutcome.Passed);

        public static TestInfo[] GetTestAdapterLoadErrorTests(string projectName) {
            return new[] { GetLoadErrorImportError(projectName), GetLoadErrorNoError(projectName) };
        }

        public static string TestAdapterEnvironmentProject = TestData.GetPath(@"TestData\TestAdapterTests\EnvironmentTest.pyproj");
        public static TestInfo EnvironmentTestSuccess = TestInfo.FromRelativePaths("EnvironmentTests", "test_environ", @"TestData\TestAdapterTests\EnvironmentTest.pyproj", @"TestData\TestAdapterTests\EnvironmentTest.py", 5, TestOutcome.Passed);

        public static string TestAdapterExtensionReferenceProject = TestData.GetPath(@"TestData\TestAdapterTests\ExtensionReferenceTest.pyproj");
        public static TestInfo ExtensionReferenceTestSuccess = TestInfo.FromRelativePaths("SpamTests", "test_spam", @"TestData\TestAdapterTests\ExtensionReferenceTest.pyproj", @"TestData\TestAdapterTests\ExtensionReferenceTest.py", 5, TestOutcome.Passed);

        public static string TestAdapterDurationProject = TestData.GetPath(@"TestData\TestAdapterTests\DurationTest.pyproj");
        public static TestInfo DurationSleep01TestSuccess = TestInfo.FromRelativePaths("DurationTests", "test_sleep_0_1", @"TestData\TestAdapterTests\DurationTest.pyproj", @"TestData\TestAdapterTests\DurationTest.py", 5, TestOutcome.Passed, minDuration: TimeSpan.FromSeconds(0.1));
        public static TestInfo DurationSleep03TestSuccess = TestInfo.FromRelativePaths("DurationTests", "test_sleep_0_3", @"TestData\TestAdapterTests\DurationTest.pyproj", @"TestData\TestAdapterTests\DurationTest.py", 8, TestOutcome.Passed, minDuration: TimeSpan.FromSeconds(0.3));
        public static TestInfo DurationSleep05TestSuccess = TestInfo.FromRelativePaths("DurationTests", "test_sleep_0_5", @"TestData\TestAdapterTests\DurationTest.pyproj", @"TestData\TestAdapterTests\DurationTest.py", 11, TestOutcome.Passed, minDuration: TimeSpan.FromSeconds(0.5));
        public static TestInfo DurationSleep08TestSuccess = TestInfo.FromRelativePaths("DurationTests", "test_sleep_0_8", @"TestData\TestAdapterTests\DurationTest.pyproj", @"TestData\TestAdapterTests\DurationTest.py", 14, TestOutcome.Passed, minDuration: TimeSpan.FromSeconds(0.8));
        public static TestInfo DurationSleep15TestFailure = TestInfo.FromRelativePaths("DurationTests", "test_sleep_1_5", @"TestData\TestAdapterTests\DurationTest.pyproj", @"TestData\TestAdapterTests\DurationTest.py", 17, TestOutcome.Failed, minDuration: TimeSpan.FromSeconds(1.5));

        public static string TestAdapterStackTraceProject = TestData.GetPath(@"TestData\TestAdapterTests\StackTraceTest.pyproj");
        public static TestInfo StackTraceTestFailure = TestInfo.FromRelativePaths("StackTraceTests", "test_bad_import", @"TestData\TestAdapterTests\StackTraceTest.pyproj", @"TestData\TestAdapterTests\StackTraceTest.py", 4, TestOutcome.Failed);
    }
}
