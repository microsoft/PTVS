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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Options {
    public sealed class GlobalInterpreterOptions {
        private readonly PythonToolsService _pyService;
        private readonly IInterpreterOptionsService _interpreterOptions;

        internal GlobalInterpreterOptions(PythonToolsService pyService, IInterpreterOptionsService interpreterOptions) {
            _pyService = pyService;
            _interpreterOptions = interpreterOptions;
        }

        internal Guid DefaultInterpreter {
            get;
            set;
        }

        internal Version DefaultInterpreterVersion {
            get;
            set;
        }

        public void Load() {
            if (_interpreterOptions != null) {
                DefaultInterpreter = _interpreterOptions.DefaultInterpreter.Id;
                DefaultInterpreterVersion = _interpreterOptions.DefaultInterpreter.Configuration.Version;
            }
        }

        public void Save() {
            _interpreterOptions.DefaultInterpreter =
                _interpreterOptions.FindInterpreter(DefaultInterpreter, DefaultInterpreterVersion) ??
                _interpreterOptions.Interpreters.LastOrDefault();
            DefaultInterpreter = _interpreterOptions.DefaultInterpreter.Id;
            DefaultInterpreterVersion = _interpreterOptions.DefaultInterpreter.Configuration.Version;
        }        

        public void Reset() {
            DefaultInterpreter = Guid.Empty;
            DefaultInterpreterVersion = new Version();
        }

        internal void UpdateInterpreter() {
            var interpreter = _interpreterOptions.FindInterpreter(DefaultInterpreter, DefaultInterpreterVersion);
            if (interpreter != null) {
                _interpreterOptions.DefaultInterpreter = interpreter;
            }
        }
    }
}
