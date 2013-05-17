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
    class ModuleReference {
        public IModule Module;
        private Dictionary<ModuleInfo, int> EphmeralReferences; 

        public ModuleReference() {
        }

        public ModuleReference(IModule module) {
            Module = module;
        }

        public Namespace Namespace {
            get {
                return Module as Namespace;
            }
        }

        /// <summary>
        /// Adds an ephemeral reference for the declaring module.  Ephemeral references are modules
        /// referenced via import statements but that for a module that we don't know actually exists.
        /// As long as there are ephemeral references to the name we want to provide import completion
        /// for that module.  But once those have been removed we want to stop displaying those names.
        /// 
        /// Therefore we track the version of a module that accessed it and as long as the latest
        /// analyzed version knows about the module we'll include it in the analysis.
        /// </summary>
        /// <param name="module"></param>
        public void AddEphemeralReference(ModuleInfo module) {
            if (EphmeralReferences == null) {
                EphmeralReferences = new Dictionary<ModuleInfo, int>();
            }
            EphmeralReferences[module] = module.ProjectEntry.AnalysisVersion;
        }

        public bool HasEphemeralReferences {
            get {
                bool res = false;
                if (EphmeralReferences != null) {
                    List<ModuleInfo> toRemove = null;
                    foreach (var keyValue in EphmeralReferences) {
                        if (keyValue.Key.ProjectEntry.AnalysisVersion == keyValue.Value) {
                            res = true;
                            break;
                        } else if (toRemove == null) {
                            toRemove = new List<ModuleInfo>();
                        }
                        toRemove.Add(keyValue.Key);
                    }

                    if (toRemove != null) {
                        foreach (var value in toRemove) {
                            EphmeralReferences.Remove(value);
                        }
                    }
                }
                return res;
            }
        }
    }
}
