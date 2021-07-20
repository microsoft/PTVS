using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.LanguageServerClient.FileWatcher {
    /// <summary>
    /// Class representing the options for registering workspace/didChangeWatchedFiles support.
    /// </summary>
    [DataContract]
    public class DidChangeWatchedFilesRegistrationOptions {
        /// <summary>
        /// Gets or sets the watchers that should be registered.
        /// </summary>
        [DataMember]
        public FileSystemWatcher[] Watchers {
            get;
            set;
        }
    }
}
