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

namespace Microsoft.PythonTools.Debugger {
    class PythonThread {
        private readonly int _identity;
        private readonly PythonProcess _process;

        internal PythonThread(PythonProcess process, int identity) {
            _process = process;
            _identity = identity;
        }

        public void StepInto() {
            _process.SendStepInto(_identity);
        }

        public void StepOver() {
            _process.SendStepOver(_identity);
        }

        public void StepOut() {
            _process.SendStepOut(_identity);
        }

        public void Resume() {
            _process.SendResumeThread(_identity);
        }


        internal void ClearSteppingState() {
            _process.SendClearStepping(_identity);
        }

        public IList<PythonStackFrame> GetFrames() {
            return _process.GetThreadFrames(_identity);
        }

        public PythonProcess Process {
            get {
                return _process;
            }
        }

        internal int Id {
            get {
                return _identity;
            }
        }
    }
}
