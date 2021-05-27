using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceConfiguration {
    /// <summary>
    /// Until VSSDK supports this, this param is for receiving workspace/configuration events
    /// </summary>
    [DataContract]
    internal class ConfigurationParams {
        [DataMember]
        public ConfigurationItem[] items;
    }
}
