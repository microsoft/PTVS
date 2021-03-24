using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceFolderChanged {
    [DataContract]
    class WorkspaceFoldersChangeEvent {
        /**
	     * The array of added workspace folders
	     */
        [DataMember]
        public WorkspaceFolder[] added;

        /**
         * The array of the removed workspace folders
         */
        [DataMember]
        public WorkspaceFolder[] removed;
    }
}
