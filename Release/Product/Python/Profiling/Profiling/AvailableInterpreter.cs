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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Profiling {
    public class AvailableInterpreter {
        private readonly IPythonInterpreterFactory _factory;

        public AvailableInterpreter(IPythonInterpreterFactory factory) {
            _factory = factory;
        }

        public string Description {
            get {
                return _factory.GetInterpreterDisplay();
            }
        }

        public Guid Id {
            get {
                return _factory.Id;
            }
        }

        public Version Version {
            get {
                return _factory.Configuration.Version;
            }
        }

        public string Path {
            get {
                return _factory.Configuration.InterpreterPath;
            }
        }

        public override string ToString() {
            return Description;
        }
    }
}
