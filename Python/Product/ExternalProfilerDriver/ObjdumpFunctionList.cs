// Python Tools for Visual Studio
// Copyright(c) 2018 Intel Corporation.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {
    /// <summary>
    /// A symbol reader based on the output of `objdump -d -C -l --no-show-raw-insn`
    /// This is largely a port of the symbolizer in google/pprof
    /// </summary>
    public class SymbolReaderLinux
    {
        string _sourceFile;
        
        private Regex objdumpAsmOutputRE    = new Regex(@"^\s*([0-9a-dA-D]+):\s+(.*)");
        private Regex objdumpOutputFileLine = new Regex(@"^(.*):([0-9]+)");
        private Regex objdumpOutputFunction = new Regex(@"^(\S.*)\(\):");

        private SymbolReaderLinux()
        {
            /* empty */
        }

        public static SymbolReaderLinux Load(string pdbpath)
        {
            try {
                if (!File.Exists(pdbpath)) throw new SymbolReaderException($"Cannot find specified file: [{pdbpath}]");
                var loader = new SymbolReaderLinux();
                loader._sourceFile = pdbpath;
                return loader;
            } catch (Exception ex) {
                throw;
            }
        }

        public IEnumerable<FunctionSourceLocation> FunctionLocations()
        {
            string line;
            using (var reader = File.OpenText(_sourceFile)) {

                FunctionSourceLocation fsl = null;
                while ((line = reader.ReadLine()) != null) {
                    Match mf = objdumpOutputFunction.Match(line);
                    if (mf.Success) {
                        fsl = new FunctionSourceLocation() {
                            Function = mf.Groups[1].ToString()
                        };
                    } else {
                        Match ml = objdumpOutputFileLine.Match(line);
                        if (!ml.Success) {
                            continue;
                        }
                        if (fsl == null) {
                            continue;
                        } else {
                            fsl.SourceFile = ml.Groups[1].ToString();
                            fsl.LineNumber = Int32.Parse(ml.Groups[2].ToString());
                            yield return fsl;
                            fsl = null;
                        }
                    }
                }
            }
        }
    }
}