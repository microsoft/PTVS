// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.PythonTools.VsCode.Commands {
    internal interface IController {
        Task<object> ExecuteAsync(string command, params object[] args);
    }
}
