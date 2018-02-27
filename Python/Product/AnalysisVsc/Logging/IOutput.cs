// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.PythonTools.VsCode.Logging {
    internal interface IOutput {
        void Write(string text);
        void WriteError(string text);
    }
}
