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
using Microsoft.PythonTools;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;

namespace TestUtilities.UI.Python {
    public sealed class PythonReplWindowProxySettings : ReplWindowProxySettings {
        public PythonReplWindowProxySettings() {
            SourceFileName = "stdin";
            IntFirstMember = "bit_length";
            RawInput = "raw_input";
            IPythonIntDocumentation = Python2IntDocumentation;
            ExitHelp = Python2ExitHelp;
            Print42Output = "42";
            ImportError = "ImportError: No module named {0}";
        }

        public new PythonReplWindowProxySettings Clone() {
            return (PythonReplWindowProxySettings)MemberwiseClone();
        }

        public override void AssertValid() {
            Version.AssertInstalled();
        }

        public override VisualStudioApp CreateApp() {
            return new PythonVisualStudioApp();
        }

        public override ToolWindowPane ActivateInteractiveWindow(VisualStudioApp app, string projectName, string backend) {
            string description = null;
            if (Version.IsCPython) {
                description = string.Format("{0} {1}",
                    Version.Isx64 ? "Python 64-bit" : "Python 32-bit",
                    Version.Version.ToVersion()
                );
            } else if (Version.IsIronPython) {
                description = string.Format("{0} {1}",
                    Version.Isx64 ? "IronPython 64-bit" : "IronPython",
                    Version.Version.ToVersion()
                );
            }
            Assert.IsNotNull(description, "Unknown interpreter");

            var automation = (IVsPython)app.Dte.GetObject("VsPython");
            var options = (IPythonOptions)automation;
            var replOptions = options.Interactive;
            Assert.IsNotNull(replOptions, "Could not find options for " + description);

            var oldAddNewLineAtEndOfFullyTypedWord = options.Intellisense.AddNewLineAtEndOfFullyTypedWord;
            app.OnDispose(() => options.Intellisense.AddNewLineAtEndOfFullyTypedWord = oldAddNewLineAtEndOfFullyTypedWord);
            options.Intellisense.AddNewLineAtEndOfFullyTypedWord = AddNewLineAtEndOfFullyTypedWord;

            var interpreters = app.ComponentModel.GetService<IInterpreterRegistryService>();
            var replId = PythonReplEvaluatorProvider.GetEvaluatorId(
                interpreters.FindConfiguration(Version.Id)
            );

            if (!string.IsNullOrEmpty(projectName)) {
                var dteProj = app.GetProject(projectName);
                var proj = (PythonProjectNode)dteProj.GetCommonProject();
                replId = PythonReplEvaluatorProvider.GetEvaluatorId(proj);
            }

            return app.ServiceProvider.GetUIThread().Invoke(() => {
                app.ServiceProvider.GetPythonToolsService().InteractiveBackendOverride = backend;
                var provider = app.ComponentModel.GetService<InteractiveWindowProvider>();
                return (ToolWindowPane)provider.OpenOrCreate(replId);
            });
        }

        public const string IronPython27ExitHelp = @"Help on Quitter in module site object:

class Quitter(__builtin__.object)
 |  Methods defined here:
 |  
 |  __call__(self, code=None)
 |  
 |  __dict__ = <dictproxy object>
 |  __init__(self, name)
 |  
 |  __repr__(self)
 |  
 |  __weakref__ = <attribute '__weakref__' of <class 'site.Quitter'> objects
";

        public const string Python2ExitHelp = @"Help on Quitter in module site object:

class Quitter(__builtin__.object)
 |  Methods defined here:
 |  
 |  __call__(self, code=None)
 |  
 |  __init__(self, name)
 |  
 |  __repr__(self)
 |  
 |  ----------------------------------------------------------------------
 |  Data descriptors defined here:
 |  
 |  __dict__
 |      dictionary for instance variables (if defined)
 |  
 |  __weakref__
 |      list of weak references to the object (if defined)
";

        public const string Python3ExitHelp = @"Help on Quitter in module site object:

class Quitter(builtins.object)
 |  Methods defined here:
 |  
 |  __call__(self, code=None)
 |  
 |  __init__(self, name)
 |  
 |  __repr__(self)
 |  
 |  ----------------------------------------------------------------------
 |  Data descriptors defined here:
 |  
 |  __dict__
 |      dictionary for instance variables (if defined)
 |  
 |  __weakref__
 |      list of weak references to the object (if defined)
";

        public const string Python34ExitHelp = @"Help on Quitter in module _sitebuiltins object:

class Quitter(builtins.object)
 |  Methods defined here:
 |  
 |  __call__(self, code=None)
 |  
 |  __init__(self, name, eof)
 |  
 |  __repr__(self)
 |  
 |  ----------------------------------------------------------------------
 |  Data descriptors defined here:
 |  
 |  __dict__
 |      dictionary for instance variables (if defined)
 |  
 |  __weakref__
 |      list of weak references to the object (if defined)
";

        public const string Python35ExitHelp = @"Help on Quitter in module _sitebuiltins object:

class Quitter(builtins.object)
 |  Methods defined here:
 |  
 |  __call__(self, code=None)
 |      Call self as a function.
 |  
 |  __init__(self, name, eof)
 |      Initialize self.  See help(type(self)) for accurate signature.
 |  
 |  __repr__(self)
 |      Return repr(self).
 |  
 |  ----------------------------------------------------------------------
 |  Data descriptors defined here:
 |  
 |  __dict__
 |      dictionary for instance variables (if defined)
 |  
 |  __weakref__
 |      list of weak references to the object (if defined)
";

        public const string Python2IntDocumentation = @"Type:        int
String form: 42
Docstring:
int(x=0) -> int or long
int(x, base=10) -> int or long

Convert a number or string to an integer, or return 0 if no arguments
are given.  If x is floating point, the conversion truncates towards zero.
If x is outside the integer range, the function returns a long instead.

If x is not a number or if base is given, then x must be a string or
Unicode object representing an integer literal in the given base.  The
literal can be preceded by '+' or '-' and be surrounded by whitespace.
The base defaults to 10.  Valid bases are 0 and 2-36.  Base 0 means to
interpret the base from the string as an integer literal.
>>> int('0b100', base=0)
4";

        public const string Python3IntDocumentation = @"Type:        int
String form: 42
Docstring:
int(x=0) -> integer
int(x, base=10) -> integer

Convert a number or string to an integer, or return 0 if no arguments
are given.  If x is a number, return x.__int__().  For floating point
numbers, this truncates towards zero.

If x is not a number or if base is given, then x must be a string,
bytes, or bytearray instance representing an integer literal in the
given base.  The literal can be preceded by '+' or '-' and be surrounded
by whitespace.  The base defaults to 10.  Valid bases are 0 and 2-36.
Base 0 means to interpret the base from the string as an integer literal.
>>> int('0b100', base=0)
4";

        public PythonVersion Version { get; set; }

        public string SourceFileName { get; set; }

        public string IPythonIntDocumentation { get; set; }

        public string ExitHelp { get; set; }

        public string IntFirstMember { get; set; }

        public string RawInput { get; set; }

        public string Print42Output { get; set; }

        public bool KeyboardInterruptHasTracebackHeader { get; set; }

        public string ImportError { get; set; }

        public bool AddNewLineAtEndOfFullyTypedWord { get; set; }
    }
}
