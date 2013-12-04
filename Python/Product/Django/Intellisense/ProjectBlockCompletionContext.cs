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

#if DEV12_OR_LATER

using System.Linq;
using Microsoft.Html.Editor;
using Microsoft.PythonTools.Django.Project;
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.VisualStudio.Text;
using Microsoft.Web.Editor;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal class ProjectBlockCompletionContext : ProjectBlockCompletionContextBase {
        public ProjectBlockCompletionContext(DjangoAnalyzer analyzer, ITextBuffer buffer)
            : base(analyzer, buffer, TextBufferExtensions.GetFileName(buffer)) {

            var doc = HtmlEditorDocument.FromTextBuffer(buffer);
            if (doc == null) {
                return;
            }

            var artifacts = doc.HtmlEditorTree.ArtifactCollection;
            foreach (var artifact in artifacts.OfType<TemplateBlockArtifact>()) {
                var artifactText = doc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange);
                artifact.Parse(artifactText);
                if (artifact.Block != null) {
                    var varNames = artifact.Block.GetVariables();
                    foreach (var varName in varNames) {
                        AddLoopVariable(varName);
                    }
                }
            }
        }
    }
}

#endif