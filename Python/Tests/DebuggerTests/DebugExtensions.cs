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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace DebuggerTests
{
    static class DebugExtensions
    {
        internal static PythonProcess DebugProcess(this PythonDebugger debugger, PythonVersion version, string filename, TextWriter debugLog, Func<PythonProcess, PythonThread, Task> onLoaded = null, bool resumeOnProcessLoaded = true, string interpreterOptions = null, PythonDebugOptions debugOptions = PythonDebugOptions.RedirectOutput, string cwd = null, string arguments = "")
        {
            string fullPath = Path.GetFullPath(filename);
            string dir = cwd ?? Path.GetFullPath(Path.GetDirectoryName(filename));
            if (!String.IsNullOrEmpty(arguments))
            {
                arguments = "\"" + fullPath + "\" " + arguments;
            }
            else
            {
                arguments = "\"" + fullPath + "\"";
            }
            var process = debugger.CreateProcess(version.Version, version.InterpreterPath, arguments, dir, "", interpreterOptions, debugOptions, debugLog);
            process.DebuggerOutput += (sender, args) =>
            {
                Console.WriteLine("{0}: {1}", args.Thread?.Id, args.Output);
            };
            process.ProcessLoaded += async (sender, args) =>
            {
                if (onLoaded != null)
                {
                    await onLoaded(process, args.Thread);
                }
                if (resumeOnProcessLoaded)
                {
                    await process.ResumeAsync(default(CancellationToken));
                }
            };

            return process;
        }

        internal static PythonBreakpoint AddBreakpointByFileExtension(this PythonProcess newproc, int line, string finalBreakFilename)
        {
            PythonBreakpoint breakPoint;
            var ext = Path.GetExtension(finalBreakFilename);

            if (String.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(ext, ".djt", StringComparison.OrdinalIgnoreCase))
            {
                breakPoint = newproc.AddDjangoBreakpoint(finalBreakFilename, line);
            }
            else
            {
                breakPoint = newproc.AddBreakpoint(finalBreakFilename, line);
            }
            return breakPoint;
        }
    }
}
