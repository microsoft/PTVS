using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceConfiguration {
    /// <summary>
    /// Until VSSDK supports this, this param is for receiving workspace/configuration events
    /// </summary>
    [DataContract]
    internal class ConfigurationItem {
        [DataMember]
        [JsonConverter(typeof(Microsoft.VisualStudio.LanguageServer.Protocol.DocumentUriConverter))]
        public Uri scopeUri;

        [DataMember]
        public string section;
    }
}
