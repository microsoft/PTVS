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
#if DJANGO_HTML_EDITOR
using System.IO;
using Microsoft.WebTools.Languages.Html.Artifacts;
using Microsoft.WebTools.Languages.Shared.Text;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    /// <summary>
    /// Produces the <see cref="TemplateArtifactCollection"/> for a given text document.
    /// </summary>
    internal class TemplateArtifactProcessor : IArtifactProcessor {
        private class TextProviderReader : TextReader {
            private readonly ITextProvider _text;
            private int _pos;

            public TextProviderReader(ITextProvider text) {
                _text = text;
            }

            public override int Read() {
                if (_pos >= _text.Length) {
                    return -1;
                }

                return _text[_pos++];
            }
        }

        public void GetArtifacts(ITextProvider text, ArtifactCollection artifactCollection) {
            var reader = new TextProviderReader(text);
            var tokenizer = new TemplateTokenizer(reader);
            foreach (var token in tokenizer.GetTokens()) {
                if (token.Kind != TemplateTokenKind.Text) {
                    var range = TextRange.FromBounds(token.Start, token.End + 1);
                    var artifact = TemplateArtifact.Create(token.Kind, range, token.IsClosed);
                    artifactCollection.Add(artifact);
                }
            }
        }

        public ArtifactCollection CreateArtifactCollection() {
            return new TemplateArtifactCollection();
        }

        public bool IsReady {
            get { return true; }
        }

        public string LeftSeparator {
            get { return ""; }
        }

        public string RightSeparator {
            get { return ""; }
        }

        public string LeftCommentSeparator {
            get { return "{#"; }
        }

        public string RightCommentSeparator {
            get { return "#}"; }
        }
    }
}
#endif
