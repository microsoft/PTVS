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

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    public class JunitXmlTestResultParser {
        /// <summary>
        /// Parses the junit xml for test results and matches them to the corresponding original vsTestCase using 
        /// </summary>
        /// <param name="junitXmlPath"></param>
        /// <param name="tests"></param>
        /// <returns></returns>
        internal static string GetPytestId(XPathNavigator result) {
            var file = result.GetAttribute("file", "");
            var funcname = result.GetAttribute("name", "");
            var classname = result.GetAttribute("classname", "");
            var line = result.GetAttribute("line", "");

            if (IsPytestResultValid(file, classname, funcname, line)) {
                var pytestId = CreatePytestId(file, classname, funcname);
                return pytestId;
            }
            return null;
        }

        /// <summary>
        /// Handles two cases:
        /// case A: Function inside a class - classname drops the filename portion 
        ///  <testcase classname="test2.Test_test2" file="test2.py" name="test_A" >
        ///  pytestId .\test2.py::Test_test2::test_A
        ///  
        /// case B: Global function - classname dropped entirely
        ///   <testcase classname="test_sample" file="test_sample.py" name="test_answer" >
        ///   pytestId .\test_sample.py::test_answer 
        ///   
        /// case C: Function inside a class inside a package - classsname drops the relative path portion
        ///   <testcase classname="package1.packageA.test1.Test_test1" file="package1\packageA\test1.py" line="3" name="test_A" time="0.003">
        ///   pytestID .\package1\packageA\test1.py::Test_test1::test_A
        /// 
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="classname"></param>
        /// <param name="funcname"></param>
        /// <returns></returns>
        internal static string CreatePytestId(string filename, string classname, string funcname) {
            var classNameWithoutFilename = String.Empty;
            var filenamePortion = Path.ChangeExtension(filename, null).Replace("\\", ".");
            if (filenamePortion.Length < classname.Length) {
                classNameWithoutFilename = classname.Substring(filenamePortion.Length + 1);
            }

            if (!String.IsNullOrEmpty(classNameWithoutFilename)) {
                return $".\\{filename}::{classNameWithoutFilename}::{funcname}";
            } else {
                return $".\\{filename}::{funcname}";
            }
        }

        internal static bool IsPytestResultValid(string file, string classname, string funcname, string line) {
            return !String.IsNullOrEmpty(file)
                   && !String.IsNullOrEmpty(classname)
                   && !String.IsNullOrEmpty(funcname)
                   && Int32.TryParse(line, out _);
        }

        internal static void UpdateVsTestResult(TestResult result, XPathNavigator navNode) {
            if (navNode.Name != "testcase") {
                throw new ArgumentException("navNode.Name {0} not equal to testcase", navNode.Name);
            }

            result.Outcome = TestOutcome.Passed;

            try {
                var timeStr = navNode.GetAttribute("time", "");
                var time = Double.Parse(timeStr, CultureInfo.InvariantCulture);
                result.Duration = TimeSpan.FromSeconds(time);
            } catch (FormatException) {
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
                            result.Outcome = TestOutcome.Failed; // occurs when a pytest framework or parse error happens
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

        internal static XPathDocument Read(string xml) {
            var settings = new XmlReaderSettings {
                XmlResolver = null
            };
            return new XPathDocument(XmlReader.Create(new StreamReader(xml, new UTF8Encoding(false)), settings));
        }
    }
}
