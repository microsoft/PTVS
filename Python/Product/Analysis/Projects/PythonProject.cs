// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Projects {
    /// <summary>
    /// Provides information about a Python project.  This is an abstract base class that
    /// different project systems can implement.  Tools which want to plug in an extend the
    /// Python analysis system can work with the PythonProject to get information about
    /// the project.
    /// 
    /// This differs from the ProjectAnalyzer class in that it contains more rich information
    /// about the configuration of the project related to running and executing.
    /// </summary>
    public abstract class PythonProject {
        /// <summary>
        /// Gets a property for the project.  Users can get/set their own properties, also these properties
        /// are available:
        /// 
        ///     CommandLineArguments -> arguments to be passed to the debugged program.
        ///     InterpreterPath  -> gets a configured directory where the interpreter should be launched from.
        ///     IsWindowsApplication -> determines whether or not the application is a windows application (for which no console window should be created)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public abstract string GetProperty(string name);

        public abstract string GetUnevaluatedProperty(string name);

        /// <summary>
        /// Sets a property for the project.  See GetProperty for more information on common properties.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public abstract void SetProperty(string name, string value);

        public abstract IPythonInterpreterFactory GetInterpreterFactory();

        /// <summary>
        /// Gets the current analyzer for the project, or null if no analyzer is available.
        /// </summary>
        [Obsolete("Use the async version if possible")]
        public abstract ProjectAnalyzer Analyzer { get; }

        /// <summary>
        /// Gets the current analyzer for the project. May wait while creating an analyzer
        /// if necessary, where the <see cref="Analyzer"/> property would return null.
        /// </summary>
        public abstract Task<ProjectAnalyzer> GetAnalyzerAsync();

        public abstract event EventHandler ProjectAnalyzerChanged;

        public abstract string ProjectHome { get; }

        public abstract LaunchConfiguration GetLaunchConfigurationOrThrow();

        /// <summary>
        /// Attempts to retrieve a PythonProject from the provided object, which
        /// should implement <see cref="IPythonProjectProvider"/>.
        /// </summary>
        public static PythonProject FromObject(object source) {
            return (source as IPythonProjectProvider)?.Project;
        }
    }
}
