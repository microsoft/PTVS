// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.DsTools.Core.Logging {
    /// <summary>
    /// Defines verbosity of logging the application performs
    /// </summary>
    public enum LogVerbosity {
        /// <summary>
        /// Logging is completely off
        /// </summary>
        None,

        /// <summary>
        /// Limited set of events is recorded in the OS events log such as
        /// application processes lifetime, version of R and tools, success
        /// and failure connecting to remote machines.
        /// </summary>
        Minimal,

        /// <summary>
        /// In addition to the events recorded in the OS Application log
        /// a log file is created in the TEMP folder. The log file receives
        /// additional information on the internal events which may come
        /// useful in case of troubleshooting connections or the product
        /// installation.
        /// </summary>
        Normal,

        /// <summary>
        /// Application creates additional log file that receive complete traffic 
        /// between host the and tools (Visual Studio) processes. The traffic
        /// log may contain private data and other private information.
        /// </summary>
        Traffic
    }
}
