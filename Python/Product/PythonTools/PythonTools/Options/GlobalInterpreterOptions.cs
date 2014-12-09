/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
            DefaultInterpreter = _interpreterOptions.DefaultInterpreter.Id;
            DefaultInterpreterVersion = _interpreterOptions.DefaultInterpreter.Configuration.Version;
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
