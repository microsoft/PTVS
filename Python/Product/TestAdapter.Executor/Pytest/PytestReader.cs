using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    static class PyTestReader {

        public static IEnumerable<TestCase> ParseDiscovery(PytestDiscoveryResults result, ITestCaseDiscoverySink discoverySink) {
            var testcases = new List<TestCase>();
            var parentMap = new Dictionary<string, PytestParent>();
            result.Parents.ForEach(p => parentMap[p.Id] = p);

            foreach (var t in result.Tests) {
                var sourceAndLineNum = t.Source.Replace(".\\", "");
                String[] sourceParts = sourceAndLineNum.Split(':');
                Debug.Assert(sourceParts.Length == 2);

                if (sourceParts.Length == 2 &&
                    Int32.TryParse(sourceParts[1], out int line) &&
                    !String.IsNullOrWhiteSpace(t.Name) &&
                    !String.IsNullOrWhiteSpace(t.Id)) {

                    //bschnurr todo: fix codepath for files outside of project
                    var source = sourceParts[0];
                    var codeFilePath = Path.Combine(result.Root, source);

                    var fullyQualifiedName = CreateFullyQualifiedTestNameFromId(t.Id);
                    var tc = new TestCase(fullyQualifiedName, Constants.PytestUri, codeFilePath) {
                        DisplayName = t.Name,
                        LineNumber = line,
                        CodeFilePath = codeFilePath
                    };

                    tc.SetPropertyValue(Constants.PytestFileProptery, source);
                    tc.SetPropertyValue(Constants.PytestIdProperty, t.Id);
                    tc.SetPropertyValue(Constants.PyTestXmlClassNameProperty, CreateXmlClassName(t, parentMap));

                    if (discoverySink != null) {
                        discoverySink.SendTestCase(tc);
                    }
                    testcases.Add(tc);
                } else {
                    Debug.WriteLine("Testcase parse failed:\n {0}".FormatInvariant(t.Id));
                }
            }

            return testcases;
        }

        /// <summary>
        /// Creates a classname that matches the junit testresult generated one so that we can match testresults with testcases
        /// Note if a function doesn't have a class, its classname appears to be the filename without an extension
        /// </summary>
        /// <param name="t"></param>
        /// <param name="parentMap"></param>
        /// <returns></returns>
        private static string CreateXmlClassName(PytestTest t, Dictionary<string, PytestParent> parentMap) {
            var parentList = new List<string>();
            var currId = t.Parentid;
            while (parentMap.TryGetValue(currId, out PytestParent parent)) {
                // class names for functions dont append the direct parent 
                if (String.Compare(parent.Kind, "function", StringComparison.OrdinalIgnoreCase) != 0) {
                    parentList.Add(Path.GetFileNameWithoutExtension(parent.Name));
                }
                currId = parent.Parentid;
            }
            parentList.Reverse();

            var xmlClassName = String.Join(".", parentList);
            return xmlClassName;
        }

        public static string CreateFullyQualifiedTestNameFromId(string pytestId) {
            var fullyQualifiedName = pytestId.Replace(".\\", "");
            String[] parts = fullyQualifiedName.Split(new string[] { "::" }, StringSplitOptions.None); ;

            // set classname as filename, without extension for test functions outside of classes,
            // so test explorer doesn't use .py as the classname
            if (parts.Length == 2) {
                var className = Path.GetFileNameWithoutExtension(parts[0]);
                return $"{parts[0]}::{className}::{parts[1]}";
            }
            return fullyQualifiedName;
        }
    }
}
