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

using System.IO;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Debugger {
    class PythonModule {
        private readonly int _moduleId;
        private readonly string _filename;

        public PythonModule(int moduleId, string filename) {
            _moduleId = moduleId;
            _filename = filename;
        }

        public int ModuleId {
            get {
                return _moduleId;
            }
        }

        public string Name {
            get {
                
                if (CommonUtils.IsValidPath(_filename)) {
                    return Path.GetFileNameWithoutExtension(_filename);
                }
                return _filename;
            }
        }

        public string Filename {
            get {
                return _filename;
            }
        }
    }
}
