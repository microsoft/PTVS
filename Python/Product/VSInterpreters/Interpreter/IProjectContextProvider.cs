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
        /// <summary>
        /// Raised when the list of projects has changed.
        /// </summary>
        event EventHandler ProjectsChanged;
        /// <summary>
        /// Raised when one of the individual projects may have had interpreters added or removed.
        /// </summary>
        event EventHandler<ProjectChangedEventArgs> ProjectChanged;

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

    public class ProjectChangedEventArgs : EventArgs {
        private readonly object _project;

        public ProjectChangedEventArgs(object project) {
            _project = project;
        }

        public object Project => _project;
    }
}
