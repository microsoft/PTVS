// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
// Licensed under the Apache License, Version 2.0 (the License).

using System;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    internal sealed class ResetSuppressedDialogsCommand : Command {
        private readonly IServiceProvider _provider;

        public ResetSuppressedDialogsCommand(IServiceProvider provider) {
            _provider = provider;
        }

        public override int CommandId => (int)PkgCmdIDList.cmdidResetSuppressedDialogs;

        public override void DoCommand(object sender, EventArgs args) {
            PythonGeneralOptionsPage.ResetSuppressedDialogs(_provider.GetPythonToolsService());
        }
    }
}