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

using System.Linq;
using System.Text;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a coroutine instance
    /// </summary>
    internal class CoroutineInfo : BuiltinInstanceInfo {
        private readonly IPythonProjectEntry _declaringModule;
        private readonly int _declaringVersion;
        public readonly VariableDef Returns;

        public CoroutineInfo(PythonAnalyzer projectState, IPythonProjectEntry entry)
            : base(projectState.ClassInfos[BuiltinTypeId.Generator]) {
            // Internally, coroutines are represented by generators with a CO_*
            // flag on the code object. Here we represent it as a separate info,
            // but reuse the underlying class info.

            _declaringModule = entry;
            _declaringVersion = entry.AnalysisVersion;
            Returns = new VariableDef();
        }

        public override IPythonProjectEntry DeclaringModule { get { return _declaringModule; } }
        public override int DeclaringVersion { get { return _declaringVersion; } }

        public override string Description {
            get {
                // Generator lies about its name when it represents a coroutine
                var sb = new StringBuilder("coroutine");
                FunctionInfo.AddReturnTypeString(sb, Returns.TypesNoCopy.AsUnion);
                return sb.ToString();
            }
        }

        public override IAnalysisSet Await(Node node, AnalysisUnit unit) {
            Returns.AddDependency(unit);
            return Returns.TypesNoCopy;
        }

        public void AddReturn(Node node, AnalysisUnit unit, IAnalysisSet returnValue, bool enqueue = true) {
            Returns.MakeUnionStrongerIfMoreThan(ProjectState.Limits.ReturnTypes, returnValue);
            Returns.AddTypes(unit, returnValue, enqueue);
        }
    }
}
