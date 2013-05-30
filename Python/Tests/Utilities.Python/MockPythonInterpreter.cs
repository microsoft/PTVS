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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell;

namespace TestUtilities.Python {
    public class MockPythonInterpreter : IPythonInterpreter {
        public readonly List<string> _modules;
        public bool IsDatabaseInvalid;

        public MockPythonInterpreter(IPythonInterpreterFactory factory) {
            _modules = new List<string>();
        }


        public void Initialize(PythonAnalyzer state) {
            throw new NotImplementedException();
        }

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            throw new NotImplementedException();
        }

        public IList<string> GetModuleNames() {
            return _modules;
        }

        public event EventHandler ModuleNamesChanged { add { } remove { } }

        public IPythonModule ImportModule(string name) {
            if (_modules.Contains(name)) {
                return null;
            }
            return null;
        }

        public IModuleContext CreateModuleContext() {
            throw new NotImplementedException();
        }

        public System.Threading.Tasks.Task AddReferenceAsync(ProjectReference reference, CancellationToken cancellationToken = default(CancellationToken)) {
            throw new NotImplementedException();
        }

        public void RemoveReference(ProjectReference reference) {
            throw new NotImplementedException();
        }
    }
}
