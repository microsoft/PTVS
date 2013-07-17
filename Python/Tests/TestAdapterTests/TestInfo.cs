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

extern alias ta;
using System;
using System.IO;
using Microsoft.PythonTools.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using ta::Microsoft.VisualStudioTools;
using TestUtilities;

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
            ti.ClassFilePath = classFilePath ?? sourceCodeFilePath;
            ti.RelativeClassFilePath = CommonUtils.GetRelativeFilePath(Path.GetDirectoryName(ti.ProjectFilePath), ti.ClassFilePath);
            return ti;
        }

        public TestCase TestCase {
            get {
                var expectedFullyQualifiedName = TestDiscoverer.MakeFullyQualifiedTestName(RelativeClassFilePath, ClassName, MethodName);
                var tc = new TestCase(expectedFullyQualifiedName, new Uri(TestExecutor.ExecutorUriString), this.ProjectFilePath);
                tc.CodeFilePath = SourceCodeFilePath;
                tc.LineNumber = SourceCodeLineNumber;
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
        private static TestInfo TestInPackageSuccess = TestInfo.FromRelativePaths("TestsInPackage", "test_in_package_pass", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\tests\TestsInPackage.py", 4, TestOutcome.Passed);
        private static TestInfo TestInPackageFailure = TestInfo.FromRelativePaths("TestsInPackage", "test_in_package_fail", @"TestData\TestAdapterTestB\TestAdapterTestB.pyproj", @"TestData\TestAdapterTestB\tests\TestsInPackage.py", 7, TestOutcome.Failed);
        private static TestInfo LinkedSuccess = TestInfo.FromRelativePaths("LinkedTests", "test_linked_pass", @"TestData\TestAdapterTestA\TestAdapterTestA.pyproj", @"TestData\TestAdapterTestB\LinkedTest.py", 4, TestOutcome.Passed);
        private static TestInfo LinkedFailure = TestInfo.FromRelativePaths("LinkedTests", "test_linked_fail", @"TestData\TestAdapterTestA\TestAdapterTestA.pyproj", @"TestData\TestAdapterTestB\LinkedTest.py", 7, TestOutcome.Failed);

        public static string TestAdapterAProjectFilePath = TestData.GetPath(@"TestData\TestAdapterTestA\TestAdapterTestA.pyproj");
        public static string TestAdapterBProjectFilePath = TestData.GetPath(@"TestData\TestAdapterTestB\TestAdapterTestB.pyproj");
        public static string TestAdapterLibProjectFilePath = TestData.GetPath(@"TestData\TestAdapterLibraryBar\TestAdapterLibraryBar.pyproj");

        public static TestInfo[] TestAdapterATests {
            get {
                return new TestInfo[] {
                    BarSuccess,
                    BarFailure,
                    LinkedSuccess,
                    LinkedFailure
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
                    TimeoutSuccess,
                    TestInPackageSuccess,
                    TestInPackageFailure
                };
            }
        }
    }
}
