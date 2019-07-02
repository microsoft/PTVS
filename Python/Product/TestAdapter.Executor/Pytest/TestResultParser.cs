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
            var vsTestResults = tests.Select(t => new TestResult(t) { Outcome = TestOutcome.NotFound }).ToList();

            if (!File.Exists(junitXmlPath)) {
                return vsTestResults;
            }

            var doc = Read(junitXmlPath);
            XPathNodeIterator xmlTestCases = doc.CreateNavigator().Select("/testsuite/testcase");

            foreach (XPathNavigator pytestcase in xmlTestCases) {
                var file = pytestcase.GetAttribute("file", "");
                var name = pytestcase.GetAttribute("name", "");
                var classname = pytestcase.GetAttribute("classname", "");

                if (String.IsNullOrEmpty(file) ||
                    String.IsNullOrEmpty(name) ||
                    String.IsNullOrEmpty(classname) ||
                    !Int32.TryParse(pytestcase.GetAttribute("line", String.Empty), out int line))
                {
                    var message = String.Empty;
                    if(pytestcase.HasChildren) {
                        pytestcase.MoveToFirstChild();
                        message = pytestcase.GetAttribute("message", String.Empty) + pytestcase.Value;
                    }
                    Debug.WriteLine("Test result parse failed: {0}".FormatInvariant(message));
                    continue;
                }

                // Match on classname and function name for now
                var result = vsTestResults
                    .Where(tr =>
                    String.Compare(tr.TestCase.DisplayName, name, StringComparison.InvariantCultureIgnoreCase) == 0 &&
                    String.Compare(tr.TestCase.GetPropertyValue<string>(Pytest.Constants.PyTestXmlClassNameProperty, default(string)), classname, StringComparison.InvariantCultureIgnoreCase) == 0) 
                    .FirstOrDefault();

                if (result != null) {
                    UpdateTestResult(pytestcase, result);
                } else {
                    var message = String.Empty;
                    if (pytestcase.HasChildren) {
                        pytestcase.MoveToFirstChild();
                        message = pytestcase.GetAttribute("message", String.Empty) + pytestcase.Value;
                    }
                    Debug.WriteLine("Testcase for result not found: {0}".FormatInvariant(message));
                }
            }
                     
            return vsTestResults;
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
                            result.ErrorMessage = navNode.GetAttribute("message", "");
                            break;
                        case "failure":
                            result.Outcome = TestOutcome.Failed;
                            result.ErrorMessage = navNode.GetAttribute("message", "");
                            result.ErrorStackTrace = navNode.Value; // added to show in stacktrace column
                            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, $"{navNode.Value}\n")); //Test Explorer Detail summary wont show link to stacktrace without adding a TestResultMessage
                            break;
                        case "error":
                            result.Outcome = TestOutcome.None; // occurs when a pytest framework or parse error happens
                            result.ErrorMessage = navNode.GetAttribute("message", "");
                            result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, $"{navNode.Value}\n"));
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
