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

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Defines the list of events which PTVS will log to a IPythonToolsLogger.
    /// </summary>
    enum PythonLogEvent {
        /// <summary>
        /// Logs a debug launch.  Data supplied should be 1 or 0 indicating whether
        /// the launch was without debugging or with.
        /// </summary>
        Launch,
        /// <summary>
        /// Logs the number of available interpreters.
        /// Data is an int indicating the number of interpreters.
        /// </summary>
        InstalledInterpreters,
        /// <summary>
        /// Logs the number of virtual environments in a project.
        /// Data is an int indicating the number of virtual environments.
        /// </summary>
        VirtualEnvironments,
        /// <summary>
        /// Logs the frequency at which users check for new Survey\News
        /// Data is an int enum mapping to SurveyNews* setting
        /// </summary>
        SurveyNewsFrequency,
        /// <summary>
        /// Logs installed package
        /// </summary>
        PythonPackage,
        /// <summary>
        /// Information about a debug REPL connection event
        /// </summary>
        DebugRepl,
        /// <summary>
        /// Information about enabled experimental features
        /// </summary>
        Experiments,
        /// <summary>
        /// Info about debugger launch or attach timeouts on slower machines
        /// </summary>
        DebugAdapterConnectionTimeout,
        /// <summary>
        /// Create conda environment info bar
        /// </summary>
        CondaEnvCreateInfoBar,
        /// <summary>
        /// Create virtual environment info bar
        /// </summary>
        VirtualEnvCreateInfoBar,
        /// <summary>
        /// Install packages info bar
        /// </summary>
        PackageInstallInfoBar,
        /// <summary>
        /// Configure test framework info bar
        /// </summary>
        ConfigureTestFrameworkInfoBar,
        /// <summary>
        /// Warn Python 38 support
        /// </summary>
        PythonNotSupportedInfoBar,
        /// <summary>
        /// Create conda environment
        /// </summary>
        CreateCondaEnv,
        /// <summary>
        /// Create virtual environment
        /// </summary>
        CreateVirtualEnv,
        /// <summary>
        /// Add existing environment
        /// </summary>
        AddExistingEnv,
        /// <summary>
        /// Install environment
        /// </summary>
        InstallEnv,
        /// <summary>
        /// Select an environment from the python toolbar
        /// </summary>
        SelectEnvFromToolbar,
        /// <summary>
        /// Add an environment from the python toolbar
        /// </summary>
        AddEnvFromToolbar,
        /// <summary>
        /// Format document
        /// </summary>
        FormatDocument,
        /// Publish using the button on Python 'Publish' project property page
        /// </summary>
        PythonSpecificPublish,
        /// <summary>
        /// Events from language server
        /// </summary>
        LanguageServer,
        /// Warn about untrusted workspace.
        /// </summary>
        UntrustedWorkspaceInfoBar,
    }
}
