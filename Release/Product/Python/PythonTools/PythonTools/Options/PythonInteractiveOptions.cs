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

using Microsoft.PythonTools.Repl;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Stores options related to the interactive window for a single Python interpreter instance.
    /// </summary>
    class PythonInteractiveOptions : PythonInteractiveCommonOptions {
        private bool _enableAttach;
        private string _startupScript, _executionMode, _interperterOptions;

        public bool EnableAttach {
            get { return _enableAttach; }
            set { _enableAttach = value; }
        }

        public string StartupScript {
            get { return _startupScript; }
            set { _startupScript = value; }
        }

        public string ExecutionMode {
            get { return _executionMode; }
            set { _executionMode = value; }
        }

        public string InterpreterOptions {
            get { return _interperterOptions; }
            set { _interperterOptions = value; }
        }
    }
}
