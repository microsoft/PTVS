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
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis {
    public struct CallInfo {
        private readonly ISet<Namespace>[] _args;

        internal CallInfo(ISet<Namespace>[] args) {
            _args = args;
        }

        public int NormalArgumentCount {
            get {
                return _args.Length;
            }
        }

        public IEnumerable<AnalysisValue> GetArgument(int arg) {
            return _args[arg];
        }
    }
}
