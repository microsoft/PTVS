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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.WebTools.Languages.Json.Schema;

namespace Microsoft.Settings {
    [Export(typeof(IJsonSchemaSelector))]
    internal sealed class SchemaSelector : IJsonSchemaSelector {
#pragma warning disable 0067
        public event EventHandler AvailableSchemasChanged { add { } remove { } }
#pragma warning restore

        public Task<IEnumerable<string>> GetAvailableSchemasAsync()
            => Task.FromResult(Enumerable.Empty<string>());

        public string GetSchemaFor(string fileLocation) {
            if(!Path.GetFileName(fileLocation).Equals("mspythonconfig.json", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }
            var asm = Assembly.GetExecutingAssembly().Location;
            var folder = Path.GetDirectoryName(asm);
            return Path.Combine(folder, "pyrightconfig-schema.json");
        }
    }
}
