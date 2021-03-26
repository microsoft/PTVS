using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceFolderChanged {
    [DataContract]
    class WorkspaceFolder {
        /**
         * The associated URI for this workspace folder.
         */
        [DataMember]
        [Newtonsoft.Json.JsonConverter(typeof(DocumentUriConverter))]
        public Uri uri;

        /**
         * The name of the workspace folder. Used to refer to this
         * workspace folder in the user interface.
         */
        [DataMember]
        public string name;
    }
}
