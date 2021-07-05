// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for opening an arbitrary URL when selected.
    /// </summary>
    internal sealed class OpenWebUrlCommand : Command {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _url;

        public OpenWebUrlCommand(
            IServiceProvider serviceProvider,
            string url,
            uint commandId
        ) {
            _serviceProvider = serviceProvider;
            _url = url;
            CommandId = (int)commandId;
        }

        public override void DoCommand(object sender, EventArgs args) {
            CommonPackage.OpenWebBrowser(_serviceProvider, _url);
        }

        public override int CommandId { get; }
    }
}
