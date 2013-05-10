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

using System.ComponentModel;
using Microsoft.IronPythonTools.Interpreter;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.IronPythonTools {
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Description("Python Tools IronPython Interpreter")]
    [ProvidePythonInterpreterFactoryProvider("{80659AB7-4D53-4E0C-8588-A766116CBD46}", typeof(IronPythonInterpreterFactoryProvider))]
    [ProvidePythonInterpreterFactoryProvider("{FCC291AA-427C-498C-A4D7-4502D6449B8C}", typeof(IronPythonInterpreterFactoryProvider))]
    class IpyToolsPackage : Package {
        public IpyToolsPackage() {
        }
    }
}
