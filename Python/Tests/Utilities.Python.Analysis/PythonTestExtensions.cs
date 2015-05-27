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
using System.IO;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;

namespace TestUtilities.Python {
    public static class PythonTestExtensions {
        public static HashSet<string> FindModules(this IPythonInterpreterFactory factory, params string[] moduleNames) {
            return factory.FindModulesAsync(moduleNames).GetAwaiter().GetResult();
        }

        public static void Parse(this IPythonProjectEntry entry, PythonLanguageVersion version, string code) {
            using (var parser = Parser.CreateParser(new StringReader(code), version)) {
                entry.UpdateTree(parser.ParseFile(), null);
            }
        }

        public static void ParseFormat(this IPythonProjectEntry entry, PythonLanguageVersion version, string format, params object[] args) {
            entry.Parse(version, string.Format(format, args));
        }
    }
}
