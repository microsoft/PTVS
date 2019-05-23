using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System;
using System.Collections.Generic;
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
            var results = tests.Select(t => new TestResult(t) { Outcome = TestOutcome.NotFound }).ToList();

            if (!File.Exists(junitXmlPath)) {
                return results;
            }

            var doc = Read(junitXmlPath);
            XPathNodeIterator nodes = doc.CreateNavigator().Select("/testsuite/testcase");

            foreach (XPathNavigator t in nodes) {

                var lineStr = t.GetAttribute("line", "");
                int line = 0;
                if(!Int32.TryParse(lineStr,out line)) {
                    continue;
                }

                var file = t.GetAttribute("file", "");

                var testResult = results
                    .Where(x => (x.TestCase.LineNumber == line) && String.Compare(x.TestCase.Source, file, StringComparison.InvariantCultureIgnoreCase) == 0).FirstOrDefault();

                if (testResult != null) {
                  
                    testResult.Outcome = TestOutcome.Passed;

                    var timeStr = t.GetAttribute("time", "");
                    Double time = 0.0;
                    testResult.Duration = Double.TryParse(timeStr, out time) ? TimeSpan.FromSeconds(time) : TimeSpan.Zero;
                      
                    if (t.HasChildren) {
                        t.MoveToFirstChild();

                        if(String.Compare(t.Name, "failure") == 0) {
                            testResult.Outcome = TestOutcome.Failed;
                            testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, t.GetAttribute("message", "")));
                        }
                    }

                    results.Add(testResult);
                }
            }
                     
            return results;
        }


        public static XPathDocument Read(string xml) {
            var settings = new XmlReaderSettings();
            settings.XmlResolver = null;
            return new XPathDocument(XmlReader.Create(new StreamReader(xml), settings));
        }
    }
}
