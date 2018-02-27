// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using LanguageServer.VsCode.Contracts.Client;

namespace Microsoft.PythonTools.VsCode.Client {
    /// <summary>
    /// Represents Vs Code
    /// </summary>
    internal interface IVsCodeClient {
        IClient Client { get; }
        ITextDocument TextDocument { get; }
        IWindow Window { get; }
    }
}
