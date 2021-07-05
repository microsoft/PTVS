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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Provides an interface for loading/saving of options for the project.
    /// </summary>
    public interface IPythonProject {
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
        string GetProperty(string name);

        /// <summary>
        /// Sets a property for the project.  See GetProperty for more information on common properties.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        void SetProperty(string name, string value);
        void SetOrAddPropertyAfter(string name, string value, string afterProperty);

        /// <summary>
        /// Gets the working directory that the program should be launched in.  This is either
        /// a directory configured by the user or the project directory.
        /// </summary>
        /// <returns></returns>
        string GetWorkingDirectory();

        /// <summary>
        /// Gets the .py file which should be the initial entry point of the Python project.
        /// </summary>
        /// <returns></returns>
        string GetStartupFile();

        /// <summary>
        /// Gets the directory which the project lives in.
        /// </summary>
        string ProjectDirectory {
            get;
        }

        /// <summary>
        /// Gets the name of the project.
        /// </summary>
        string ProjectName {
            get;
        }

        /// <summary>
        /// Gets the interpreter factory that this project has been configured to use.
        /// </summary>
        /// <returns></returns>
        IPythonInterpreterFactory GetInterpreterFactory();

        /// <summary>
        /// Publishes the project as configured in the Publish dialog of project properties.
        /// 
        /// If publishing is not configured this function returns false. If publishing succeeds this function returns true.
        /// 
        /// If publishing fails this function raises a PublishFailedException with an inner exception indicating the 
        /// precise reason for failure.
        /// 
        /// </summary>
        bool Publish(PublishProjectOptions options);

        /// <summary>
        /// Gets a property for the project.  Users can get/set their own properties, also these properties
        /// are available:
        /// 
        ///     CommandLineArguments -> arguments to be passed to the debugged program.
        ///     InterpreterPath  -> gets a configured directory where the interpreter should be launched from.
        ///     IsWindowsApplication -> determines whether or not the application is a windows application (for which no console window should be created)
        ///     
        /// The returned property does not have any MSBuild syntax evaluated.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <returns>The property value without evaluating any MSBuild syntax.</returns>
        /// <remarks>New in 1.5.</remarks>
        string GetUnevaluatedProperty(string name);

        /// <summary>
        /// Gets the analyzer used for this project.
        /// 
        /// New in 2.0.
        /// </summary>
        Projects.ProjectAnalyzer GetProjectAnalyzer();

        /// <summary>
        /// Raised when the analyzer for the project has changed.
        /// 
        /// New in 2.0.
        /// </summary>
        event EventHandler ProjectAnalyzerChanged;

        /// <summary>
        /// Returns a command with the provided name if one is available. The
        /// name matches the target name in the associated MSBuild project.
        /// Otherwise, returns null.
        /// </summary>
        IAsyncCommand FindCommand(string canonicalName);

        /// <summary>
        /// Gets the MSBuild project instance to evaluate.
        /// </summary>
        ProjectInstance GetMSBuildProjectInstance();

        /// <summary>
        /// Gets the root project directory.
        /// </summary>
        string ProjectHome { get; }

        /// <summary>
        /// Gets the full path to the project file.
        /// </summary>
        string ProjectFile { get; }

        /// <summary>
        /// Gets the project site.
        /// </summary>
        IServiceProvider Site { get; }

        /// <summary>
        /// Specifies an action to execute prior to closing the provider.
        /// </summary>
        /// <param name="key">
        /// An object identifying the action. If multiple actions have identical
        /// keys, only the last added should be executed.
        /// </param>
        /// <param name="action">
        /// The action to execute. The parameter is <paramref name="key"/>.
        /// </param>
        void AddActionOnClose(object key, Action<object> action);

        /// <summary>
        /// Raised when the analyzer for the project is about to change. The
        /// new analyzer is not yet ready for use when this event is raised.
        /// Handlers should only modify internal state or add specializations
        /// (which will be resolved later).
        /// </summary>
        event EventHandler<AnalyzerChangingEventArgs> ProjectAnalyzerChanging;

        /// <summary>
        /// Gets the interpreter factory for this project and throws if it is
        /// unusable.
        /// </summary>
        /// <exception cref="NoInterpretersException">No interpreters are
        /// available.</exception>
        /// <exception cref="MissingInterpreterException">The active interpreter
        /// is not available.</exception>
        IPythonInterpreterFactory GetInterpreterFactoryOrThrow();

        /// <summary>
        /// Returns the current launch configuration or throws an appropriate
        /// exception. These exceptions have localized strings that may be
        /// shown to the user.
        /// </summary>
        /// <returns>The active interpreter factory.</returns>
        /// <exception cref="NoInterpretersException">
        /// No interpreters are available at all.
        /// </exception>
        /// <exception cref="MissingInterpreterException">
        /// The specified interpreter is not suitable for use.
        /// </exception>
        /// <exception cref="DirectoryNotFoundException">
        /// The working directory specified by the project does not exist.
        /// </exception>
        LaunchConfiguration GetLaunchConfigurationOrThrow();
    }
}
