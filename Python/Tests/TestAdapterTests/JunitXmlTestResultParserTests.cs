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

namespace TestAdapterTests
{

    [TestClass]
    public class JunitXmlTestResultParserTests
    {

        private const string _junitXmlResultsFormat =
@"<?xml version=""1.0"" encoding=""utf - 8""?>
  <testsuites>
    <testsuite errors = ""0"" failures = ""0"" hostname = ""computer"" name = ""pytest"" skipped = ""0"" tests = ""1"" time = ""0.014"" timestamp = ""2019-08-23T08:38:53.347672"" >
      {0}
    </testsuite>
  </testsuites>";

        [TestMethod]
        public void CreatePytestId_FuncInsideClass()
        {
            Assert.AreEqual(
                ".\\test2.py::Test_test2::test_A",
                JunitXmlTestResultParser.CreatePytestId("test2.py", "test2.Test_test2", "test_A")
            );
        }

        [TestMethod]
        public void CreatePytestId_GlobalFunc()
        {
            Assert.AreEqual(
                ".\\test_sample.py::test_answer",
                JunitXmlTestResultParser.CreatePytestId("test_sample.py", "test_sample", "test_answer")
            );
        }

        [TestMethod]
        public void CreatePytestId_GlobalFuncRelative()
        {
            Assert.AreEqual(
                ".\\tests\\unit\\test_statistics.py::test_key_creation",
                JunitXmlTestResultParser.CreatePytestId("tests\\unit\\test_statistics.py", "tests.unit.test_statistics", "test_key_creation")
            );
        }

        [TestMethod]
        public void CreatePytestId_ClassFuncWithRelativeFilename()
        {
            Assert.AreEqual(
                ".\\package1\\packageA\\test1.py::Test_test1::test_A",
                JunitXmlTestResultParser.CreatePytestId("package1\\packageA\\test1.py", "package1.packageA.test1.Test_test1", "test_A")
            );
        }

        [TestMethod]
        public void CreatePytestIdMatchesDiscoveryPytestId()
        {
            var projectRoot = "c:\\home\\";
            var filename = "Package1\\packageA\\Test1.py";
            var pytestId = ".\\package1\\packageA\\test1.py::Test_test1::test_A";

            //Note: ignoring case since vsTestResult lookup ignores
            Assert.AreEqual(
                string.Compare(
                    PyTestExtensions.CreateProperCasedPytestId(projectRoot + filename, projectRoot, pytestId),
                    JunitXmlTestResultParser.CreatePytestId(filename.ToLower(), "package1.packageA.test1.Test_test1", "test_A"),
                    ignoreCase: true),
                0
            );
        }

        [TestMethod]
        public void PytestPassResult()
        {
            var testPass = @"<testcase classname=""test_Parameters"" file=""test_Parameters.py"" line=""42"" name=""test_timedistance_v3[forward]"" time=""0.001""></testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, testPass);
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome);
            Assert.AreEqual(TimeSpan.FromSeconds(0.001), result.Duration);
        }

        [TestMethod]
        public void PytestFailureResult()
        {
            var testPass = @"<testcase classname=""test_failures"" file=""test_failures.py"" line=""12"" name=""test_fail"" time=""0.001"">
            <failure message = ""assert 0"" >
                 def test_fail():
                &gt; assert 0
                E assert 0

                test_failures.py:14: AssertionError
            </failure>
        </testcase> ";
            var xmlResults = string.Format(_junitXmlResultsFormat, testPass);
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TestOutcome.Failed, result.Outcome);
            Assert.AreEqual(TimeSpan.FromSeconds(0.001), result.Duration);
            Assert.AreEqual("assert 0", result.ErrorMessage);
            Assert.IsTrue(result.Messages.Any(item => item.Text.Contains("test_failures.py:14: AssertionError")));
        }

        [TestMethod]
        public void PytestErrorResult()
        {
            var testPass = @" <testcase classname=""test_failures"" file=""test_failures.py"" line=""16"" name=""test_error"" time=""0.001"">
            <error message = ""test setup failure"" >
                 @pytest.fixture
                def error_fixture():
                &gt; assert 0
                E assert 0

                test_failures.py:6: AssertionError
            </error>
        </testcase> ";
            var xmlResults = string.Format(_junitXmlResultsFormat, testPass);
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TestOutcome.Failed, result.Outcome);
            Assert.AreEqual(TimeSpan.FromSeconds(0.001), result.Duration);
            Assert.AreEqual("test setup failure", result.ErrorMessage);
            Assert.IsTrue(result.Messages.Any(item => item.Text.Contains("test_failures.py:6: AssertionError")));
        }

        [TestMethod]
        public void PytestSkipResult()
        {
            var testPass = @"<testcase classname=""test_failures"" file=""test_failures.py"" line=""20"" name=""test_skip"" time=""0.001"">
            <skipped message = ""skipping this test"" type = ""pytest.skip"" > C:\Users\bschnurr\source\repos\Parameters\Parameters\test_failures.py:22: skipping this test </skipped >
          </testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, testPass);
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TestOutcome.Skipped, result.Outcome);
            Assert.AreEqual(TimeSpan.FromSeconds(0.001), result.Duration);
            Assert.AreEqual("skipping this test", result.ErrorMessage);
        }

        [TestMethod]
        public void PytestSkipXFailedResult()
        {
            var testPass = @"<testcase classname = ""test_failures"" file=""test_failures.py"" line=""24"" name=""test_xfail"" time=""0.200"">
            <skipped message = ""xfailing this test"" type = ""pytest.xfail"" ></skipped>
         </testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, testPass);
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TestOutcome.Skipped, result.Outcome);
            Assert.AreEqual(TimeSpan.FromSeconds(0.200), result.Duration);
            Assert.AreEqual("xfailing this test", result.ErrorMessage);
            //todo: added check for skipped message type = "pytest.xfail" when added
        }

        [TestMethod]
        public void PytestResultsStdOutAppendsToMessages()
        {
            var testPass = @"<testcase classname=""test_failures"" file=""test_failures.py"" line=""8"" name=""test_ok"" time=""0.002"">
            <system-out>
                  ok
              </system-out>
    
            </testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, testPass);
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome);
            Assert.AreEqual(TimeSpan.FromSeconds(0.002), result.Duration);
            Assert.IsTrue(result.Messages.Any(item => item.Text.Contains("ok")));
        }

        [TestMethod]
        public void PytestResultsStdErrorAppendsToMessages()
        {
            var testPass = @"<testcase classname=""test_failures"" file=""test_failures.py"" line=""8"" name=""test_ok"" time=""0.002"">
            <system-err>
                  bad
              </system-err>
    
            </testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, testPass);
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome);
            Assert.AreEqual(TimeSpan.FromSeconds(0.002), result.Duration);
            Assert.IsTrue(result.Messages.Any(item => item.Text.Contains("bad")));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void PytestWrongXmlNodeThrowsException()
        {
            var testPass = @"<testcase classname=""test_Parameters"" file=""test_Parameters.py"" line=""42"" name=""test_timedistance_v3[forward]"" time=""0.001""></testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, testPass);

            var doc = new XmlDocument();
            doc.LoadXml(xmlResults);
            var result = new TestResult(new TestCase());
            var rootNode = doc.CreateNavigator();

            JunitXmlTestResultParser.UpdateVsTestResult(result, rootNode);
        }

        [TestMethod]
        public void PytestHandlesCultureDurationWithPeriod()
        {
            var test = @"<testcase classname=""test_failures"" file=""test_failures.py"" line=""8"" name=""test_ok"" time=""1.000""></testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, test);

            var currentCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TimeSpan.FromSeconds(1), result.Duration);

            Thread.CurrentThread.CurrentCulture = currentCulture;
        }

        [TestMethod]
        public void PytestHandlesCultureDurationThousand()
        {
            var test = @"<testcase classname=""test_failures"" file=""test_failures.py"" line=""8"" name=""test_ok"" time=""1000""></testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, test);

            var currentCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TimeSpan.FromSeconds(1000), result.Duration);

            Thread.CurrentThread.CurrentCulture = currentCulture;
        }

        [TestMethod]
        public void PytestBadDurationShouldNotThrow()
        {
            var test = @"<testcase classname=""test_failures"" file=""test_failures.py"" line=""8"" name=""test_ok"" time=""baddata""></testcase>";
            var xmlResults = string.Format(_junitXmlResultsFormat, test);

            TestResult result = ParseXmlTestUtil(xmlResults);

            Assert.AreEqual(TimeSpan.FromSeconds(0), result.Duration);
        }

        private static TestResult ParseXmlTestUtil(string xmlResults)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlResults);
            var result = new TestResult(new TestCase());
            var testCaseIter = doc.CreateNavigator().SelectDescendants("testcase", "", false);
            testCaseIter.MoveNext();
            JunitXmlTestResultParser.UpdateVsTestResult(result, testCaseIter.Current);
            return result;
        }
    }
}
