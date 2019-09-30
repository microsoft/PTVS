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
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.TestAdapter.Pytest {
    sealed class PytestParent {

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("parentid")]
        public string Parentid { get; set; }
    }

    sealed class PytestTest {

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

        public override string ToString() {
            return String.Format("{0}: Id:{1} Source:{2}", this.GetType().Name, this.Id, this.Source);
        }
    }

    sealed class PytestDiscoveryResults {

        [JsonProperty("rootid")]
        public string Rootid { get; set; }

        [JsonProperty("root")]
        public string Root { get; set; }

        [JsonProperty("parents")]
        public List<PytestParent> Parents { get; set; }

        [JsonProperty("tests")]
        public List<PytestTest> Tests { get; set; }
    }
}
