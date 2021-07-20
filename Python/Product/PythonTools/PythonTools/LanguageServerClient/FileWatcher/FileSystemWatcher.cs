using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.LanguageServerClient.FileWatcher {
    /// <summary>
    /// Enum representing the type of changes to watch for.
    /// </summary>
    [Flags]
    [DataContract]
    public enum WatchKind {
        /// <summary>
        /// Create events.
        /// </summary>
        Create = 1,
        /// <summary>
        /// Change events.
        /// </summary>
        Change = 2,
        /// <summary>
        /// Delete events.
        /// </summary>
        Delete = 4
    }

    [DataContract]
    public class FileSystemWatcher {
        /// <summary>
        /// Gets or sets the glob pattern to watch.
        /// </summary>
        [DataMember]
        public string GlobPattern {
            [CompilerGenerated]
            get;
            [CompilerGenerated]
            set;
        }

        /// <summary>
        /// Gets or sets the <see cref="T:Microsoft.VisualStudio.LanguageServer.Protocol.WatchKind" /> values that are of interest.
        /// </summary>
        [DataMember]
        public WatchKind Kind {
            [CompilerGenerated]
            get;
            [CompilerGenerated]
            set;
        } = WatchKind.Create | WatchKind.Change | WatchKind.Delete;
    }
}
