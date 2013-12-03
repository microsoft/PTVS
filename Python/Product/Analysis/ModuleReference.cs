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
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis {
    class ModuleReference {
        public IModule Module;

        private readonly Lazy<HashSet<ModuleInfo>> _references = new Lazy<HashSet<ModuleInfo>>();

        public ModuleReference(IModule module = null) {
            Module = module;
        }

        public AnalysisValue AnalysisModule {
            get {
                return Module as AnalysisValue;
            }
        }

        public bool AddReference(ModuleInfo module) {
            return _references.Value.Add(module);
        }

        public bool RemoveReference(ModuleInfo module) {
            return _references.IsValueCreated && _references.Value.Remove(module);
        }

        public bool HasReferences {
            get {
                return _references.IsValueCreated && _references.Value.Any();
            }
        }

        public IEnumerable<ModuleInfo> References {
            get {
                return _references.IsValueCreated ? _references.Value : Enumerable.Empty<ModuleInfo>();
            }
        }
    }
}
