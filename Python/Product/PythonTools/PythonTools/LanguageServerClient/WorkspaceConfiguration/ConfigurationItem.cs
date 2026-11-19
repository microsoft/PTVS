using System;
using System.Runtime.Serialization;
#if DEV18_OR_LATER
using System.Text.Json.Serialization;
using DocumentUriConverter = Microsoft.PythonTools.LanguageServerClient.ProtocolJsonSerialization.DocumentUriConverter;
#else
using Newtonsoft.Json;
using DocumentUriConverter = Microsoft.VisualStudio.LanguageServer.Protocol.DocumentUriConverter;
#endif

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceConfiguration {
    /// <summary>
    /// Until VSSDK supports this, this param is for receiving workspace/configuration events
    /// </summary>
    [DataContract]
    internal class ConfigurationItem {
        [DataMember]
        [JsonConverter(typeof(DocumentUriConverter))]
        public Uri scopeUri { get; set; }

        [DataMember]
        public string section { get; set; }
    }
}
