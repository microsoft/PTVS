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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

using System.Runtime.InteropServices;

using Dia2Lib;

namespace Microsoft.PythonTools.Profiling.ExternalProfilerDriver {

    class SymbolReader {
        IDiaDataSource _ds;
        IDiaSession _session;

        private SymbolReader() {
            /* empty */
        }

        public static SymbolReader Load(string pdbpath) {
            try {
                if (!File.Exists(pdbpath)){
                    throw new FileNotFoundException(string.Format(CultureInfo.CurrentCulture, Strings.ErrorMsgPathDoesNotExist, pdbpath));
                }
                var loader = new SymbolReader();
                loader._ds = CoCreateDiaDataSource();
                loader._ds.loadDataFromPdb(pdbpath);
                loader._ds.openSession(out loader._session);

                return loader;
            } catch (Exception) {
                throw;
            }
        }

         private static readonly Guid[] s_msdiaGuids = new[] {
            new Guid("e6756135-1e65-4d17-8576-610761398c3c"), // VS 2017 (msdia140.dll)
            new Guid("3BFCEA48-620F-4B6B-81F7-B9AF75454C7D"), // VS 2013 (msdia120.dll)
            new Guid("761D3BCD-1304-41D5-94E8-EAC54E4AC172"), // VS 2012 (msdia110.dll)
            new Guid("B86AE24D-BF2F-4AC9-B5A2-34B14E4CE11D"), // VS 2010 (msdia100.dll)
            new Guid("4C41678E-887B-4365-A09E-925D28DB33C2")  // VS 2008 (msdia90.dll)
        };

        public static IDiaDataSource CoCreateDiaDataSource() {
            uint i = 0;
            while (true) {
                try {
                    return (IDiaDataSource)Activator.CreateInstance(Type.GetTypeFromCLSID(s_msdiaGuids[i]));
                } catch (COMException) {
                    if (++i >= s_msdiaGuids.Length) throw;
                }
            }
        }

        public IEnumerable<FunctionSourceLocation> FunctionLocations() {
            IDiaEnumSymbols results;

            // findChildren(IDiaSymbol parent, SymTagEnum symTag, string name, uint compareFlags, out IDiaEnumSymbols ppResult);
            //_session.findChildren(_session.globalScope, SymTagEnum.SymTagCompiland, null, 0, out results); // can't find the symbolic name for nsnone
            _session.findChildren(_session.globalScope, SymTagEnum.SymTagFunction, null, 0, out results); // can't find the symbolic name for nsnone

            foreach (IDiaSymbol item in results) {
                IDiaEnumLineNumbers sourceLocs;
                _session.findLinesByRVA(item.relativeVirtualAddress, 0, out sourceLocs);
                foreach (IDiaLineNumber ln in sourceLocs) {
                    yield return new FunctionSourceLocation
                    {
                        Function = item.name,
                        SourceFile = ln.sourceFile.fileName,
                        LineNumber = ln.lineNumber
                    };
                }
            }
        }

    }

#if false
    class SymbolReaderException : System.Exception {
        public SymbolReaderException() {
            /* empty */
        }

        public SymbolReaderException(string message): base(message) {
            /* empty */
        }
    }
#endif

    public class FunctionSourceLocation {
        public string Function { get; set; }
        public string SourceFile { get; set; }
        public long LineNumber { get; set; }

        override public string ToString() {
            return $"Function {Function} defined at {SourceFile}:{LineNumber}";
        }
    }
}
