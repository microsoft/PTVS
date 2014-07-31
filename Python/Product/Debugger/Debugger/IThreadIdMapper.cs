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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Debugger {
    /// <summary>
    /// Provides mapping between Python thread IDs (which can be 64-bit if running on a 64-bit Linux system), and
    /// VS 32-bit thread IDs (which are 32-bit, and are faked if a 64-bit Python ID does not fit into 32 bits).
    /// </summary>
    internal interface IThreadIdMapper {
        long? GetPythonThreadId(uint vsThreadId);
    }
}
