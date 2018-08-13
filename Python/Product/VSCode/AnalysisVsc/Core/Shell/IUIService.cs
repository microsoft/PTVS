// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    }
}
