using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.PythonTools {
    /// <summary>
    /// Provides an interface for interacting with Python Tools for Visual Studio via COM automation.
    /// </summary>
    public interface IVsPython {
        /// <summary>
        /// Opens the Python interactive window with given description.  Equivalent to doing View->Other Windows and selecting
        /// the window with the same name.
        /// </summary>
        /// <param name="description"></param>
        void OpenInteractive(string description);
    }
}
