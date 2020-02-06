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

namespace Microsoft.PythonTools.Debugger {

    [JsonObject(MemberSerialization.OptIn)]
    internal abstract class DebugInfo {
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

        public string GetJsonString() {
            var jsonSettings = new JsonSerializerSettings() {
                TypeNameHandling = TypeNameHandling.None,
                NullValueHandling = NullValueHandling.Ignore
            };

            return JsonConvert.SerializeObject(this, Formatting.Indented, jsonSettings);
        }

    }

    [JsonObject(MemberSerialization.OptIn)]
    internal class DebugLaunchInfo : DebugInfo {
        [JsonProperty("cwd")]
        public string CurrentWorkingDirectory { get; set; }

        [JsonProperty("python")]
        public List<string> InterpreterPathAndArguments { get; set; }
        //public string Python { get; set; }

        [JsonProperty("console")]
        public string Console { get; set; }

        [JsonProperty("program")]
        //public string Program { get; set; }
        public List<string> ScriptPathAndArguments { get; set; }

        [JsonProperty("django")]
        public bool DebugDjango { get; set; }

        public string LaunchWebPageUrl { get; set; }

    }

    [JsonObject(MemberSerialization.OptIn)]
    internal class DebugAttachInfo : DebugInfo {
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        public Uri RemoteUri { get; set; }

    }

    //TODO will be added later
    //public class Rule {
    //    [JsonProperty("path")]
    //    public string Foo { get; set; }

    //    [JsonProperty("D")]
    //    public bool? Include { get; set; }
    //}

}
