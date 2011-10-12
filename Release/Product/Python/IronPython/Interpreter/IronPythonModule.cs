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
using IronPython.Runtime;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonModule : PythonObject<PythonModule>, IPythonModule2 {
        private readonly string _name;

        public IronPythonModule(IronPythonInterpreter interpreter, PythonModule mod, string name)
            : base(interpreter, mod) {
            _name = name;
        }

        public override PythonMemberType MemberType {
            get {
                return PythonMemberType.Module;
            }
        }

        #region IPythonModule Members

        public string Name {
            get { return _name; }
        }

        public void Imported(IModuleContext context) {
            if (_name == "clr") {
                ((IronPythonModuleContext)context).ShowClr = true;
            } else if (_name == "wpf") {
                AddWpfReferences();
            }
        }

        public IEnumerable<string> GetChildrenModules() {
            return new string[0];
        }

        private void AddWpfReferences() {
            Interpreter.AddAssembly(typeof(System.Windows.Markup.XamlReader).Assembly);     // PresentationFramework
            Interpreter.AddAssembly(typeof(System.Windows.Clipboard).Assembly);             // PresentationCore
            Interpreter.AddAssembly(typeof(System.Windows.DependencyProperty).Assembly);    // WindowsBase
            Interpreter.AddAssembly(typeof(System.Xaml.XamlReader).Assembly);               // System.Xaml
        }


        #endregion

        #region IPythonModule2 Members

        public string Documentation {
            get { 
                object docValue;
                if (!Value.Get__dict__().TryGetValue("__doc__", out docValue) || !(docValue is string)) {
                    return String.Empty;
                }
                return (string)docValue;
            }
        }

        #endregion
    }
}
