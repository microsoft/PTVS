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

        [JsonProperty("subProcess")]
        public bool? SubProcess { get; set; }

        [JsonProperty("env")]
        public Dictionary<string, string> Env { get; set; }

        [JsonProperty("rules")]
        public IList<PathRule> Rules { get; set; }

        [JsonProperty("variablePresentation")]
        public VariablePresentation VariablePresentation { get; set; }

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

        [JsonProperty("console")]
        public string Console { get; set; }

        [JsonProperty("program")]
        public string Script { get; set; }

        [JsonProperty("args")]
        public List<string> ScriptArguments { get; set; }

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

    public class PathRule {
        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("include")]
        public bool? Include { get; set; }
    }

    public class VariablePresentation {

        // default to group, which is the current vscode behavior
        public static PresentationMode DefaultPresentationMode = PresentationMode.Group;

        [JsonProperty("class_")]
        public PresentationMode Class { get; set; }

        [JsonProperty("function")]
        public PresentationMode Function { get; set; }

        [JsonProperty("protected")]
        public PresentationMode Protected { get; set; }

        [JsonProperty("special")]
        public PresentationMode Special { get; set; }

        public VariablePresentation() {

            Class = DefaultPresentationMode;
            Function = DefaultPresentationMode;
            Protected = DefaultPresentationMode;
            Special = DefaultPresentationMode;
        }
    }
}
