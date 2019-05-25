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
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/testsuite/testcase");

            foreach (XPathNavigator t in nodes) {
                var file = t.GetAttribute("file", "");
                var name = t.GetAttribute("name", "");
               
                if (String.IsNullOrEmpty(file) ||
                    String.IsNullOrEmpty(name) ||
                    !Int32.TryParse(t.GetAttribute("line", String.Empty), out int line))
                {
                    var message = String.Empty;
                    if(t.HasChildren) {
                        t.MoveToFirstChild();
                        message = t.GetAttribute("message", String.Empty) + t.Value;
                    }
                    Debug.WriteLine("Test result parse failed: {0}".FormatInvariant(message));
                    continue;
                }

                var qualifiedName = String.Format("{0}::{1}", file, name);

                var foundResult = testResults
                    .Where(x => String.Compare(x.TestCase.FullyQualifiedName, qualifiedName, StringComparison.InvariantCultureIgnoreCase) == 0)
                    .FirstOrDefault();

                if (foundResult != null) {

                    foundResult.Outcome = TestOutcome.Passed;

                    var timeStr = t.GetAttribute("time", "");
                    Double time = 0.0;
                    foundResult.Duration = Double.TryParse(timeStr, out time) ? TimeSpan.FromSeconds(time) : TimeSpan.Zero;
                      
                    if (t.HasChildren) {
                        t.MoveToFirstChild();

                        if ((String.Compare(t.Name, "failure") == 0) ||
                            (String.Compare(t.Name, "error") == 0)) {
                            foundResult.Outcome = TestOutcome.Failed;

                            foundResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, $"{t.GetAttribute("message", "")}\n{t.Value}"));
                        }
                    }
                }
                else {
                    var message = String.Empty;
                    if (t.HasChildren) {
                        t.MoveToFirstChild();
                        message = t.GetAttribute("message", String.Empty) + t.Value;
                    }
                    Debug.WriteLine("Testcase for result not found: {0}".FormatInvariant(message));
                }
            }
                     
            return testResults;
        }


        public static XPathDocument Read(string xml) {
            var settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            return new XPathDocument(XmlReader.Create(new StreamReader(xml, new UTF8Encoding(false)), settings));
        }
    }
}
