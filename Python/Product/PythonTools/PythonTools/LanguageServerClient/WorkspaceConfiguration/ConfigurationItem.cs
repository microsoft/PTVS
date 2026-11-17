using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceConfiguration {
    /// <summary>
    /// Until VSSDK supports this, this param is for receiving workspace/configuration events
    /// </summary>
    [DataContract]
    internal class ConfigurationItem {
        [DataMember]
        [JsonConverter(typeof(ProtocolJsonSerialization.DocumentUriConverter))]
        public Uri scopeUri { get; set; }

        [DataMember]
        public string section { get; set; }
    }
}
