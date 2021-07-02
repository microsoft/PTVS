// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.PythonTools.Django.TemplateParsing;

namespace Microsoft.PythonTools.Django.Intellisense
{
    internal class ProjectBlockCompletionContext : ProjectBlockCompletionContextBase
    {
        public ProjectBlockCompletionContext(VsProjectAnalyzer analyzer, ITextBuffer buffer)
            : base(analyzer, buffer.GetFileName())
        {

            var doc = HtmlEditorDocument.TryFromTextBuffer(buffer);
            if (doc == null)
            {
                return;
            }

            var artifacts = doc.HtmlEditorTree.ArtifactCollection;
            foreach (var artifact in artifacts.OfType<TemplateBlockArtifact>())
            {
                var artifactText = doc.HtmlEditorTree.ParseTree.Text.GetText(artifact.InnerRange.Start, artifact.InnerRange.Length);
                artifact.Parse(artifactText);
                if (artifact.Block != null)
                {
                    var varNames = artifact.Block.GetVariables();
                    foreach (var varName in varNames)
                    {
                        AddLoopVariable(varName);
                    }
                }
            }
        }
    }
}
