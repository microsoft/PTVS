// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.DsTools.Core {
    public static class CharExtensions {
        public static char[] LineBreakChars { get; } = new char[] { '\n', '\r' };

        public static bool IsLineBreak(this char ch) => ch == '\r' || ch == '\n';
    }
}
