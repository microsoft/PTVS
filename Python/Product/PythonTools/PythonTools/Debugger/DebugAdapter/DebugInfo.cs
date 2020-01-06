using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.Debugger {

    [JsonObject(MemberSerialization.OptIn)]
    public abstract class DebugInfo {
        [JsonProperty("stopOnEntry")] 
        public bool? StopOnEntry { get; set; }

        [JsonProperty("promptBeforeRunningWithBuildError")] 
        public bool? PromptBeforeRunningWithBuildError { get; set; }

        [JsonProperty("redirectOutput")] 
        public bool? RedirectOutput { get; set; }

        [JsonProperty("waitOnAbnormalExit")] 
        public bool? WaitOnAbnormalExit { get; set; }

        [JsonProperty("waitOnNormalExit")] 
        public bool? WaitOnNormalExit { get; set; }

        [JsonProperty("breakOnSystemExitZero")] 
        public bool? BreakOnSystemExitZero { get; set; }

        [JsonProperty("debugStdLib")]
        public bool? DebugStdLib { get; set; }

        [JsonProperty("showReturnValue")] 
        public bool? ShowReturnValue { get; set; }

        [JsonProperty("env")]
        public Dictionary<string, string> Env { get; set; }

        //[JsonProperty("rules")]
        //public Rule[] Rules { get; set; }//TODO Will be added later

        public abstract string GetJsonString();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DebugLaunchInfo : DebugInfo {
        [JsonProperty("cwd")]
        public string Cwd { get; set; }

        [JsonProperty("python")]
        public List<string> Python { get; set; } //Interpreter path and arguments
        //public string Python { get; set; }

        [JsonProperty("console")]
        public string Console { get; set; }

        [JsonProperty("program")]
        //public string Program { get; set; }
        public List<string> Program { get; set; } //Script path and arguments

        [JsonProperty("code")] 
        public string Code { get; set; }

        [JsonProperty("django")] 
        public bool DebugDjango { get; set; }

        public string WebPageUrl { get; set; }

        public override string GetJsonString() {
            var jsonSettings = new JsonSerializerSettings() {
                TypeNameHandling = TypeNameHandling.None,
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(this, Formatting.Indented, jsonSettings);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DebugAttachInfo : DebugInfo {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        public Uri RemoteUri { get; set; }

        public override string GetJsonString() {
            var jsonSettings = new JsonSerializerSettings() {
                TypeNameHandling = TypeNameHandling.None,
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(this, Formatting.Indented, jsonSettings);
        }
    }

    //TODO will be added later
    //public class Rule {
    //    [JsonProperty("path")]
    //    public string Foo { get; set; }

    //    [JsonProperty("D")]
    //    public bool? Include { get; set; }
    //}

}
