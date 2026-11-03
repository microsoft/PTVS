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
#if DEV18_OR_LATER
using System.Text.Json;
#endif
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace PythonToolsTests {
    [TestClass]
    public class ResultConverterTests {
        private const string SingleLocationJson = @"{
  ""uri"": ""file:///C:/project/sample.py"",
  ""range"": {
    ""start"": { ""line"": 4, ""character"": 8 },
    ""end"": { ""line"": 4, ""character"": 16 }
  }
}";

        private const string LocationArrayJson = @"[
  {
    ""uri"": ""file:///C:/project/sample.py"",
    ""range"": {
      ""start"": { ""line"": 4, ""character"": 8 },
      ""end"": { ""line"": 4, ""character"": 16 }
    }
  },
  {
    ""uri"": ""file:///C:/project/other.py"",
    ""range"": {
      ""start"": { ""line"": 10, ""character"": 2 },
      ""end"": { ""line"": 10, ""character"": 7 }
    }
  }
]";

        private const string CompletionListJson = @"{
  ""isIncomplete"": false,
  ""items"": [
    { ""label"": ""value"", ""kind"": 6 }
  ]
}";

        [TestMethod, Priority(0)]
        public void ConvertsNewtonsoftLocation() {
            var locations = ConvertLocations(JToken.Parse(SingleLocationJson), 1);

            AssertLocation(locations[0], "file:///C:/project/sample.py", 4, 8, 4, 16);
        }

        [TestMethod, Priority(0)]
        public void ConvertsNewtonsoftLocationArray() {
            var locations = ConvertLocations(JToken.Parse(LocationArrayJson), 2);

            AssertLocation(locations[0], "file:///C:/project/sample.py", 4, 8, 4, 16);
            AssertLocation(locations[1], "file:///C:/project/other.py", 10, 2, 10, 7);
        }

        [TestMethod, Priority(0)]
        public void ConvertsNewtonsoftCompletionList() {
            AssertCompletionList(ResultConverter.ConvertResult(JToken.Parse(CompletionListJson)));
        }

#if DEV18_OR_LATER
        [TestMethod, Priority(0)]
        public void ConvertsSystemTextJsonLocation() {
            var result = JsonSerializer.Deserialize<object>(SingleLocationJson);
            var locations = ConvertLocations(result, 1);

            AssertLocation(locations[0], "file:///C:/project/sample.py", 4, 8, 4, 16);
        }

        [TestMethod, Priority(0)]
        public void ConvertsSystemTextJsonLocationArray() {
            var result = JsonSerializer.Deserialize<object>(LocationArrayJson);
            var locations = ConvertLocations(result, 2);

            AssertLocation(locations[0], "file:///C:/project/sample.py", 4, 8, 4, 16);
            AssertLocation(locations[1], "file:///C:/project/other.py", 10, 2, 10, 7);
        }

        [TestMethod, Priority(0)]
        public void ConvertsSystemTextJsonDocument() {
            using (var document = JsonDocument.Parse(SingleLocationJson)) {
                var locations = ConvertLocations(document, 1);

                AssertLocation(locations[0], "file:///C:/project/sample.py", 4, 8, 4, 16);
            }
        }

        [TestMethod, Priority(0)]
        public void ConvertsSystemTextJsonCompletionList() {
            var result = JsonSerializer.Deserialize<object>(CompletionListJson);
            AssertCompletionList(ResultConverter.ConvertResult(result));
        }
#endif

        private static Location[] ConvertLocations(object result, int expectedCount) {
            var locations = ResultConverter.ConvertResult(result) as Location[];

            Assert.IsNotNull(locations);
            Assert.AreEqual(expectedCount, locations.Length);
            return locations;
        }

        private static void AssertCompletionList(object[] result) {
            var completionLists = result as CompletionList[];

            Assert.IsNotNull(completionLists);
            Assert.AreEqual(1, completionLists.Length);
            Assert.IsFalse(completionLists[0].IsIncomplete);
            Assert.AreEqual(1, completionLists[0].Items.Length);
            Assert.AreEqual("value", completionLists[0].Items[0].Label);
        }

        private static void AssertLocation(
            Location location,
            string expectedUri,
            int startLine,
            int startCharacter,
            int endLine,
            int endCharacter) {
            Assert.AreEqual(new Uri(expectedUri), location.Uri);
            Assert.AreEqual(startLine, location.Range.Start.Line);
            Assert.AreEqual(startCharacter, location.Range.Start.Character);
            Assert.AreEqual(endLine, location.Range.End.Line);
            Assert.AreEqual(endCharacter, location.Range.End.Character);
        }
    }
}
