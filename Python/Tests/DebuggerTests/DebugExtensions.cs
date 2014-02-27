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
using System.IO;
using Microsoft.PythonTools.Debugger;
using TestUtilities;

namespace DebuggerTests {
    static class DebugExtensions {
        internal static PythonProcess DebugProcess(this PythonDebugger debugger, PythonVersion version, string filename, Action<PythonProcess, PythonThread> onLoaded = null, bool resumeOnProcessLoaded = true, string interpreterOptions = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput, string cwd = null, string arguments = "") {
            string fullPath = Path.GetFullPath(filename);
            string dir = cwd ?? Path.GetFullPath(Path.GetDirectoryName(filename));
            if (!String.IsNullOrEmpty(arguments)) {
                arguments = "\"" + fullPath + "\" " + arguments;
            } else {
                arguments = "\"" + fullPath + "\"";
            }
            var process = debugger.CreateProcess(version.Version, version.InterpreterPath, arguments, dir, "", interpreterOptions, debugOptions);
            process.DebuggerOutput += (sender, args) => {
                Console.WriteLine("{0}: {1}", args.Thread.Id, args.Output);
            };
            process.ProcessLoaded += (sender, args) => {
                if (onLoaded != null) {
                    onLoaded(process, args.Thread);
                }
                if (resumeOnProcessLoaded) {
                    process.Resume();
                }
            };

            return process;
        }

        internal static PythonBreakpoint AddBreakPointByFileExtension(this PythonProcess newproc, int line, string finalBreakFilename) {
            PythonBreakpoint breakPoint;
            var ext = Path.GetExtension(finalBreakFilename);

            if (String.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, ".djt", StringComparison.OrdinalIgnoreCase)) {
                breakPoint = newproc.AddDjangoBreakPoint(finalBreakFilename, line);
            } else {
                breakPoint = newproc.AddBreakPoint(finalBreakFilename, line);
            }
            return breakPoint;
        }


    }
}
