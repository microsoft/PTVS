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

using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Values {
    interface IModule {
        IModule GetChildPackage(IModuleContext context, string name);
        IEnumerable<KeyValuePair<string, Namespace>> GetChildrenPackages(IModuleContext context);

        void SpecializeFunction(string name, System.Func<Parsing.Ast.CallExpression, Interpreter.AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> dlg);
    }
}
