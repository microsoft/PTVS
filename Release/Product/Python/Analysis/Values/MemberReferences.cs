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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Maintains a list of references keyed off of name.
    /// </summary>
    class MemberReferences {
        private Dictionary<string, ReferenceDict> _references;
        
        public void AddReference(Node node, AnalysisUnit unit, string name) {
            if (!unit.ForEval) {
                if (_references == null) {
                    _references = new Dictionary<string, ReferenceDict>();
                }
                ReferenceDict refs;
                if (!_references.TryGetValue(name, out refs)) {
                    _references[name] = refs = new ReferenceDict();
                }
                refs.GetReferences(unit.DeclaringModule.ProjectEntry).AddReference(new SimpleSrcLocation(node.Span));
            }
        }

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            ReferenceDict references;
            if (_references != null && _references.TryGetValue(name, out references)) {
                return references.Values;
            }
            return new IReferenceable[0];
        }
    }

    /// <summary>
    /// A collection of references which are keyd off of project entry.
    /// </summary>
    class ReferenceDict : Dictionary<IProjectEntry, ReferenceList> {
        public ReferenceList GetReferences(ProjectEntry project) {
            ReferenceList builtinRef;
            if (!TryGetValue(project, out builtinRef) || builtinRef.Version != project.Version) {
                this[project] = builtinRef = new ReferenceList(project);
            }
            return builtinRef;
        }

        public IEnumerable<LocationInfo> AllReferences {
            get {
                foreach (var keyValue in this) {
                    if (keyValue.Value.References != null) {
                        foreach (var reference in keyValue.Value.References) {
                            yield return new LocationInfo(keyValue.Key, reference.Line, reference.Column);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// A list of references as stored for a single project entry.
    /// </summary>
    class ReferenceList : IReferenceable {
        public readonly int Version;
        public readonly IProjectEntry Project;
        public ISet<SimpleSrcLocation> References;

        public ReferenceList(IProjectEntry project) {
            Version = project.Version;
            Project = project;
            References = new HashSet<SimpleSrcLocation>();
        }

        public void AddReference(SimpleSrcLocation location) {
            HashSetExtensions.AddValue(ref References, location);
        }

        #region IReferenceable Members

        public IEnumerable<KeyValuePair<IProjectEntry, SimpleSrcLocation>> Definitions {
            get { yield break; }
        }

        IEnumerable<KeyValuePair<IProjectEntry, SimpleSrcLocation>> IReferenceable.References {
            get {
                if (References != null) {
                    foreach (var location in References) {
                        yield return new KeyValuePair<IProjectEntry, SimpleSrcLocation>(Project, location);
                    }
                }
            }
        }

        #endregion
    }

}
