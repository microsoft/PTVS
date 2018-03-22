// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Microsoft.DsTools.Core.Services;

namespace Microsoft.PythonTools.VsCode.Commands {
    internal interface ICommand {
        Task<object> ExecuteAsync(IServiceContainer services, params object[] args);
    }
}
