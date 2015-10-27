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

namespace Microsoft.PythonTools.Logging {
    /// <summary>
    /// Defines the list of events which PTVS will log to a IPythonToolsLogger.
    /// </summary>
    public enum PythonLogEvent {
        /// <summary>
        /// Logs a debug launch.  Data supplied should be 1 or 0 indicating whether
        /// the launch was without debugging or with.
        /// </summary>
        Launch,
        /// <summary>
        /// Logs the number of installed (picked up automatically) interpreters.
        /// 
        /// Data is an int indicating the number of interpreters.
        /// </summary>
        InstalledInterpreters,
        /// <summary>
        /// Logs the number of configured (user added) interpreters.
        /// 
        /// Data is an int indicating the number of interpreters.
        /// </summary>
        ConfiguredInterpreters,
        /// <summary>
        /// Logs the frequency at which users check for new Survey\News
        /// 
        /// Data is an int enum mapping to SurveyNews* setting
        /// </summary>
        SurveyNewsFrequency,

        /// <summary>
        /// Logs package installs
        /// </summary>
        PackageInstalled

    }
}
