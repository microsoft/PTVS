using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.LanguageServerClient.WorkspaceConfiguration {
    /// <summary>
    /// This object is used to handle workspace/configuration requests
    /// </summary>
    internal class ConfigurationArgs {
        public ConfigurationParams requestParams;
        public object requestResult;
    }
}
