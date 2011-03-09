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
using System.Text;
using Microsoft.PythonTools.Analysis;


namespace Microsoft.PythonTools.Project {

    public class PythonFileNode : CommonFileNode {
        internal PythonFileNode(CommonProjectNode root, ProjectElement e)
            : base(root, e) { }

        public override string Caption {
            get {
                var res = base.Caption;
                if (res == "__init__.py" && Parent != null) {
                    StringBuilder fullName = new StringBuilder(res);
                    fullName.Append(" (");

                    List<HierarchyNode> nodes = new List<HierarchyNode>();
                    var curNode = Parent;
                    do {
                        nodes.Add(curNode);
                        curNode = curNode.Parent;
                    } while (curNode != null && curNode.FindChild(Path.Combine(curNode.VirtualNodeName, "__init__.py")) != null);

                    for (int i = nodes.Count - 1; i >= 0; i--) {
                        fullName.Append(nodes[i].Caption);
                        if (i != 0) {
                            fullName.Append('.');
                        }
                    }

                    fullName.Append(")");
                    res = fullName.ToString();
                    //res += " (" + Parent.Caption + ")";
                }
                return res;
            }
        }

        public override void Remove(bool removeFromStorage) {
            ((PythonProjectNode)ProjectMgr).GetAnalyzer().UnloadFile(GetAnalysis());
            base.Remove(removeFromStorage);
        }

        public override string GetEditLabel() {
            return base.Caption;
        }

        public override string FileName {
            get {
                return base.Caption;
            }
            set {
                base.FileName = value;
            }
        }

        public IProjectEntry GetAnalysis() {
            var textBuffer = GetTextBuffer();

            IProjectEntry analysis;
            if (textBuffer != null && textBuffer.TryGetAnalysis(out analysis)) {
                return analysis;
            }

            return ((PythonProjectNode)this.ProjectMgr).GetAnalyzer().GetAnalysisFromFile(Url);
        }

    }
}
