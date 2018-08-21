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

using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.LanguageServer;

namespace Microsoft.DsTools.Core.Services.Shell {
    /// <summary>
    /// Service that represents the application user interface.
    /// </summary>
    public interface IUIService {
        /// <summary>
        /// Displays error message in a host-specific UI
        /// </summary>
        Task ShowMessage(string message, MessageType messageType);

        /// <summary>
        /// Displays message with specified buttons in a host-specific UI
        /// </summary>
        Task<MessageActionItem?> ShowMessage(string message, MessageActionItem[] actions, MessageType messageType);

        /// <summary>
        /// Writes message to the host application output log
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageType"></param>
        Task LogMessage(string message, MessageType messageType);

        /// <summary>
        /// Writes message to the host application status bar
        /// </summary>
        /// <param name="message"></param>
        Task SetStatusBarMessage(string message);

        /// <summary>
        /// Sets log level for output in the host application.
        /// </summary>
        /// <param name="logLevel"></param>
        void SetLogLevel(MessageType logLevel);
    }
}
