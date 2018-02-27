// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Linq;
using JsonRpc.Standard.Contracts;

namespace Microsoft.PythonTools.VsCode.Server {
    [JsonRpcScope(MethodPrefix = "information/")]
    public sealed class InformationService : LanguageServiceBase {
        [JsonRpcMethod]
        public string getInterpreterPath() {
            if(!IsRInstalled()) {
                return null;
            }
        }

        private bool IsRInstalled() {
            var ris = Services.GetService<IRInstallationService>();
            var engines = ris
                .GetCompatibleEngines(new SupportedRVersionRange(3, 2, 3, 9))
                .OrderByDescending(x => x.Version)
                .ToList();

            return engines.Count > 0;
        }
    }
}
