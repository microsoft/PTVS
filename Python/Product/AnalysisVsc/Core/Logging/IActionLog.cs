// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.DsTools.Core.Logging {
    /// <summary>
    /// Represents action logger. Log can be a text file,
    /// an application output window or telemetry.
    /// </summary>
    public interface IActionLog {
        void Write(LogVerbosity verbosity, MessageCategory category, string message);
        void WriteFormat(LogVerbosity verbosity, MessageCategory category, string format, params object[] arguments);
        void WriteLine(LogVerbosity verbosity, MessageCategory category, string message);
        void Flush();

        LogVerbosity LogVerbosity { get; }
        string Folder { get; }
    }
}
