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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DsTools.Core.Services;

namespace Microsoft.Python.LanguageServer.Commands {
    internal sealed class Controller : IController {
        private static readonly Dictionary<string, ICommand> _commands = new Dictionary<string, ICommand> {
        };

        private readonly IServiceContainer _services;
        public Controller(IServiceContainer services) {
            _services = services;
        }

        public static string[] Commands => _commands.Keys.ToArray();

        public Task<object> ExecuteAsync(string command, params object[] args) {
            if (_commands.TryGetValue(command, out var cmd)) {
                return cmd.ExecuteAsync(_services, args);
            }
            Debug.Fail($"Unknown command {command}");
            return Task.FromResult(default(object));
        }
    }
}
