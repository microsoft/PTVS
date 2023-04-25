using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    public interface IPythonInterpreterFactoryProviderAsync {
        /// <summary>
        /// Raised when interpreter discovery is completed for this provider
        /// </summary>
        event EventHandler InterpreterDiscoveryCompleted;
    }
}
