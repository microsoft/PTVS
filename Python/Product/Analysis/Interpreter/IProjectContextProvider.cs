using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Interpreter {
    public interface IProjectContextProvider {
        event EventHandler ProjectContextsChanged;

        IEnumerable<object> ProjectContexts {
            get;
        }

        /// <summary>
        /// Called whe an interpreter was created for one of the contexts provided
        /// by this provider.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="factory"></param>
        void InterpreterLoaded(object context, InterpreterConfiguration factory);

        void InterpreterUnloaded(object context, InterpreterConfiguration factory);
    }
}
