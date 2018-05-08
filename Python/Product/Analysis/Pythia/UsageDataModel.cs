using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Analysis.Pythia {
    public class UsageDataModel {
        [JsonProperty("Repo")]
        public string Repo;

        [JsonProperty("Project")]
        public string Project;

        [JsonProperty("Document")]
        public string Document;

        [JsonProperty("References")]
        public Dictionary<string, Dictionary<string, Invocations>> References;
    }

    public class Invocations {
        //public string MethodName { get; set; }

        [JsonProperty("spanStart")]
        public List<int> SpanStart { get; set; }

        [JsonProperty("isInConditional")]
        public List<int> IsInConditional { get; set; }

        [JsonProperty("isInLoop")]
        public List<int> IsInLoop { get; set; }
    }
}
