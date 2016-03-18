using System;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Interpreter {
    /// <summary>
    /// Provides project information to IPythonInterpreterFactoryProviders which can consume projects.
    /// 
    /// The context can be an arbitrary object - e.g. a filename, an MSBuild project file, etc...
    /// 
    /// IPythonInterpreterFactoryProviders which are project-aware should import the context provider
    /// and see if they can handle any of the supported projects.  They should also hook the ProjectContextsChanged
    /// event to be informed when new projects become available.
    /// 
    /// When a IPythonInterpreterFactoryProviders discovers an interpreter within the project context it should
    /// call InterpreterLoaded.  When the interpreter configuration is no longer available it should call
    /// InterpreterUnloaded.
    /// </summary>
    public interface IProjectContextProvider {
        event EventHandler ProjectsChanaged;

        IEnumerable<object> Projects {
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
