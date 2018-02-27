// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.DsTools.Core.Logging;
using System.IO;

namespace Microsoft.DsTools.Core.Services {
    internal sealed class ServiceContainer : ServiceManager {
        public ServiceContainer() {
             AddService<IActionLog>(s => new Logger("VSCode-Python", Path.GetTempPath(), s))
            .AddService(new Application())
            .AddEditorServices();
        }
    }
}
