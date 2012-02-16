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
using System.Diagnostics;

namespace Microsoft.PythonTools {
    struct DebugTimer : IDisposable {
#if DEBUG
        internal static Stopwatch _timer = MakeStopwatch();
        private readonly long _start;        
        private readonly string _description;

        private static Stopwatch MakeStopwatch() {
            var res = new Stopwatch();
            res.Start();
            return res;
        } 
#endif

        public DebugTimer(string description) {
#if DEBUG
            // TODO: Complete member initialization
            _start = _timer.ElapsedMilliseconds;
            _description = description;
#endif
        }
        

        #region IDisposable Members

        public void Dispose() {
#if DEBUG
            Debug.WriteLine(String.Format("{0}: {1}ms elapsed", _description, _timer.ElapsedMilliseconds - _start));
#endif
        }

        #endregion
    }
}
