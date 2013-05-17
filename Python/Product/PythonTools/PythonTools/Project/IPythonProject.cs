/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
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
        VsProjectAnalyzer GetProjectAnalyzer();

        /// <summary>
        /// Raised when the analyzer for the project has changed.
        /// 
        /// New in 2.0.
        /// </summary>
        event EventHandler ProjectAnalyzerChanged;
    }

    public static class IPythonProjectExtensions {
        /// <summary>
        /// Returns a sequence of absolute search paths for the provided project.
        /// </summary>
        public static IEnumerable<string> GetSearchPaths(this IPythonProject project) {
            var paths = project.GetProperty(CommonConstants.SearchPath);
            if (!string.IsNullOrEmpty(paths)) {
                var seen = new HashSet<string>();
                foreach (var path in paths.Split(';')) {
                    if (string.IsNullOrEmpty(path)) {
                        continue;
                    }

                    var absPath = CommonUtils.GetAbsoluteFilePath(project.ProjectDirectory, path);
                    if (seen.Add(absPath)) {
                        yield return absPath;
                    }
                }
            }
        }
    }
}
