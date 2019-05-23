
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
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

            foreach (var t in result.Tests) {
                var sourceAndLineNum = t.Source.Replace(".\\", "");
                String[] sourceParts = sourceAndLineNum.Split(':');
                Debug.Assert(sourceParts.Length == 2);

                if (sourceParts.Length == 2 &&
                    Int32.TryParse(sourceParts[1], out int line) &&
                    !String.IsNullOrWhiteSpace(t.Name) &&
                    !String.IsNullOrWhiteSpace(t.Id)) {
                    var codeFilePath = rootPath + "\\" + sourceParts[0];
                    var source = sourceParts[0];

                    var fullyQualifiedName = t.Id.Replace(".\\", "");
                    var tc = new TestCase(fullyQualifiedName, executorUri, codeFilePath) {
                        DisplayName = t.Name,
                        LineNumber = line,
                        CodeFilePath = codeFilePath
                    };

                    yield return tc;
                }
            }
        }
    }
}
