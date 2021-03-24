using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceFolderChanged {
    [DataContract]
    class DidChangeWorkspaceFoldersParams {
        /**
	    * The actual workspace folder change event.
	    */
        [DataMember(Name = "event")]
        public WorkspaceFoldersChangeEvent changeEvent;
    }
}
