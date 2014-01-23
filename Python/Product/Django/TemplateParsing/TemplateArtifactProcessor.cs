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

using System.IO;
using Microsoft.Html.Core;
using Microsoft.Web.Core;

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