
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    struct PytestParent {

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("parentid")]
        public string Parentid { get; set; }
    }

    struct PytestTest {

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("markers")]
        public List<object> Markers { get; set; }

        [JsonProperty("parentid")]
        public string Parentid { get; set; }
    }

    struct PytestDiscoveryResults {

        [JsonProperty("rootid")]
        public string Rootid { get; set; }

        [JsonProperty("root")]
        public string Root { get; set; }

        [JsonProperty("parents")]
        public List<PytestParent> Parents { get; set; }

        [JsonProperty("tests")]
        public List<PytestTest> Tests { get; set; }
    }

    static class PyTestReader {

        public static IEnumerable<TestCase> ParseTestCase(string rootPath, PytestDiscoveryResults result, Uri executorUri) {
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
                    var codeFilePath = Path.Combine(rootPath, source);

                    var fullyQualifiedName = CreateFullyQualifiedTestNameFromId(t.Id);
                    var tc = new TestCase(fullyQualifiedName, executorUri, codeFilePath) {
                        DisplayName = t.Name,
                        LineNumber = line,
                        CodeFilePath = codeFilePath
                    };
                    
                    tc.SetPropertyValue(Constants.PytestFileProptery, source);
                    tc.SetPropertyValue(Constants.PytestIdProperty, t.Id);
                    tc.SetPropertyValue(Constants.PyTestXmlClassNameProperty, CreateXmlClassName(t, parentMap));
                    yield return tc;
                }
                else {
                    Debug.WriteLine("Testcase parse failed:\n {0}".FormatInvariant(t.Id));
                }
            }
        }

        /// <summary>
        /// Creates a classname that matches the junit testresult generated one so that we can match testresults with testcases
        /// Note if a function doesn't have a class, its classname appears to be the filename without an extension
        /// </summary>
        /// <param name="t"></param>
        /// <param name="parentMap"></param>
        /// <returns></returns>
        private static string CreateXmlClassName(PytestTest t, Dictionary<string, PytestParent> parentMap) {
            var classList = new List<string>();
            var currId = t.Parentid;
            while (parentMap.TryGetValue(currId, out PytestParent parent)) {
                // class names for functions dont append the direct parent 
                if (String.Compare(parent.Kind, "function", StringComparison.OrdinalIgnoreCase) != 0) {
                    classList.Add(Path.GetFileNameWithoutExtension(parent.Name));
                }
                currId = parent.Parentid;
            }

            var builder = new StringBuilder();
            classList.Reverse();
            classList.ForEach(s => builder.Append($"{s}."));

            var xmlClassName = String.Empty;
            if (builder.Length > 0) {
                xmlClassName = builder.ToString(0, builder.Length - 1);
            }
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
