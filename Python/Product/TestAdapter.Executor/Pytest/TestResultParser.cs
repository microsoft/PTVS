using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    public class TestResultParser {


        /// <summary>
        /// Parses the junit xml for test results and matches them to the corresponding original TestCase using 
        /// </summary>
        /// <param name="junitXmlPath"></param>
        /// <param name="tests"></param>
        /// <returns></returns>
        public static IEnumerable<TestResult> Parse(string junitXmlPath, IEnumerable<TestCase> tests) {
            var testResults = tests.Select(t => new TestResult(t) { Outcome = TestOutcome.NotFound }).ToList();

            if (!File.Exists(junitXmlPath)) {
                return testResults;
            }

            var doc = Read(junitXmlPath);
            XPathNodeIterator xmlTestCases = doc.CreateNavigator().Select("/testsuite/testcase");

            foreach (XPathNavigator testcase in xmlTestCases) {
                var file = testcase.GetAttribute("file", "");
                var name = testcase.GetAttribute("name", "");
                var classname = testcase.GetAttribute("classname", "");

                if (String.IsNullOrEmpty(file) ||
                    String.IsNullOrEmpty(name) ||
                    String.IsNullOrEmpty(classname) ||
                    !Int32.TryParse(testcase.GetAttribute("line", String.Empty), out int line))
                {
                    var message = String.Empty;
                    if(testcase.HasChildren) {
                        testcase.MoveToFirstChild();
                        message = testcase.GetAttribute("message", String.Empty) + testcase.Value;
                    }
                    Debug.WriteLine("Test result parse failed: {0}".FormatInvariant(message));
                    continue;
                }
       

                // Match on classname and function name for now
                var result = testResults
                    .Where(x =>
                    String.Compare(x.TestCase.DisplayName, name, StringComparison.InvariantCultureIgnoreCase) == 0 &&
                    String.Compare(x.TestCase.GetPropertyValue<string>(Pytest.Constants.PyTestXmlClassNameProperty, default(string)), classname, StringComparison.InvariantCultureIgnoreCase) == 0) 
                    .FirstOrDefault();

                if (result != null) {
                    UpdateTestResult(testcase, result);
                } else {
                    var message = String.Empty;
                    if (testcase.HasChildren) {
                        testcase.MoveToFirstChild();
                        message = testcase.GetAttribute("message", String.Empty) + testcase.Value;
                    }
                    Debug.WriteLine("Testcase for result not found: {0}".FormatInvariant(message));
                }
            }
                     
            return testResults;
        }

        private static void UpdateTestResult(XPathNavigator navNode, TestResult result) {
            result.Outcome = TestOutcome.Passed;

            var timeStr = navNode.GetAttribute("time", "");
            if (Double.TryParse(timeStr, out Double time)) {
                result.Duration = TimeSpan.FromSeconds(time);
            }

            if (navNode.HasChildren) {
             
                navNode.MoveToFirstChild();

                do {
                    switch (navNode.Name) {
                        case "skipped":
                            result.Outcome = TestOutcome.Skipped;
                            break;
                        case "failure":
                            result.Outcome = TestOutcome.Failed;
                            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, $"{navNode.GetAttribute("message", "")}\n{navNode.Value}\n"));
                            break;
                        case "error":
                            result.Outcome = TestOutcome.None;
                            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, $"{navNode.GetAttribute("message", "")}\n{navNode.Value}\n"));
                            break;
                        case "system-out":
                            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, $"{navNode.Value}\n"));
                            break;
                        case "system-err":
                            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, $"{navNode.Value}\n"));
                            break;
                    }
                } while (navNode.MoveToNext());
            }
        }

        public static XPathDocument Read(string xml) {
            var settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            return new XPathDocument(XmlReader.Create(new StreamReader(xml, new UTF8Encoding(false)), settings));
        }
    }
}
