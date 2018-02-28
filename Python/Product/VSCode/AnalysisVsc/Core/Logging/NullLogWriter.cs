// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.DsTools.Core.Logging {
    internal sealed class NullLogWriter : IActionLogWriter {
        public static IActionLogWriter Instance { get; } = new NullLogWriter();

        private NullLogWriter() { }
        public void Write(MessageCategory category, string message) {}
        public void Flush() { }
    }
}