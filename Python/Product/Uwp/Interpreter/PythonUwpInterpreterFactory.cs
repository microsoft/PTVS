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

namespace Microsoft.PythonTools.Uwp.Interpreter {
    class PythonUwpInterpreterFactory : PythonInterpreterFactoryWithDatabase {
        public const string InterpreterGuidString = "{86767848-40B4-4007-8BCC-A3835EDF0E69}";
        public static readonly Guid InterpreterGuid = new Guid(InterpreterGuidString);

        public PythonUwpInterpreterFactory(InterpreterConfiguration configuration, string description) 
            : base(
                  InterpreterGuid,
                  description,
                  configuration,
                  true) {
        }

        public override string Description {
            get {
                return base.Description;
            }
        }
        public override IPythonInterpreter MakeInterpreter(PythonInterpreterFactoryWithDatabase factory) {
            return new PythonUwpInterpreter(factory);
        }
    }
}