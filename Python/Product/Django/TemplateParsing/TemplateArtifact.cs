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

using Microsoft.PythonTools.Django.Intellisense;

namespace Microsoft.PythonTools.Django.TemplateParsing
{
    /// <summary>
    /// A single Django template construct, such as <c>{{ var }}</c> or <c>{% endcomment %}</c>.
    /// </summary>
    /// <remarks>
    /// Specific template constructs are represented by non-abstract derived classes such as <see cref="TemplateBlockArtifact"/>.
    /// Those also provide the parse data for the artifact, such as a <see cref="DjangoBlock"/>. That data is not kept in sync
    /// automatically, but has to be refreshed before querying for it by calling <see cref="TemplateArtifact.Parse"/> and providing
    /// the string that corresponds to the artifact. The artifact knows its position, but does not store references to the text
    /// buffer from which it was created, and so it does not know its current text.
    /// </remarks>
    internal abstract class TemplateArtifact : Artifact
    {
        /// <summary>
        /// The text of the artifact. This is <c>null</c> if the artifact was changed since the last parse request,
        /// which means that it does not know its text anymore.
        /// </summary>
        protected string _text;

        /// <param name="isClosed">Whether the artifact has a closing separator, or is terminated by EOF.</param>
        public TemplateArtifact(ArtifactTreatAs t, ITextRange range, bool isClosed) :
            base(t, range, 2, (isClosed ? 2 : 0), DjangoPredefinedClassificationTypeNames.TemplateTag, wellFormed: isClosed)
        {
        }

        /// <summary>
        /// Indicates whether the parse data associated with this artifact is up to date, or <see cref="Parse"/>
        /// should be called before querying for it.
        /// </summary>
        public bool IsUpToDate
        {
            get { return _text != null; }
        }

        public abstract TemplateTokenKind TokenKind { get; }

        public override bool IsEndInclusive
        {
            get { return false; }
        }

        public override bool IsStartInclusive
        {
            get { return false; }
        }

        /// <summary>
        /// Updates the parse data associated with this artifact if necessary (i.e. if we no longer know the text of this artifact,
        /// or if it changed from the last parse).
        /// </summary>
        /// <param name="text">New text of the artifact to parse.</param>
        /// <remarks>
        /// Calls <see cref="Reparse"/> if the parse data needs to be updated.
        /// </remarks>
        public void Parse(string text)
        {
            if (_text != text)
            {
                _text = text;
                Reparse(text);
            }
        }

        /// <summary>
        /// Updates the parse data associated with the artifact.
        /// </summary>
        protected abstract void Reparse(string text);

        /// <summary>
        /// Provides classifications for the contents of this artifact.
        /// </summary>
        public abstract IEnumerable<BlockClassification> GetClassifications();

        public override void Shift(int offset)
        {
            base.Shift(offset);
            _text = null;
        }

        public override void Expand(int startOffset, int endOffset)
        {
            base.Expand(startOffset, endOffset);
            _text = null;
        }

        public static TemplateArtifact Create(TemplateTokenKind kind, ITextRange range, bool isClosed)
        {
            switch (kind)
            {
                case TemplateTokenKind.Block:
                    return new TemplateBlockArtifact(range, isClosed);
                case TemplateTokenKind.Variable:
                    return new TemplateVariableArtifact(range, isClosed);
                case TemplateTokenKind.Comment:
                    return new TemplateCommentArtifact(range, isClosed);
                default:
                    throw new ArgumentException(Resources.UnsupportedTemplateTokenKind, nameof(kind));
            }
        }
    }

    /// <summary>
    /// An artifact representing a Django block, e.g. <c>{% endcomment %}</c>.
    /// </summary>
    internal class TemplateBlockArtifact : TemplateArtifact
    {
        public TemplateBlockArtifact(ITextRange range, bool isClosed)
            : base(ArtifactTreatAs.Code, range, isClosed)
        {
        }

        public override TemplateTokenKind TokenKind
        {
            get { return TemplateTokenKind.Block; }
        }

        /// <summary>
        /// Parsed block for this artifact. 
        /// </summary>
        /// <remarks>
        /// <see cref="TemplateArtifact.Parse"/> must be called to refresh this value before querying for it.
        /// </remarks>
        public DjangoBlock Block { get; private set; }

        protected override void Reparse(string text)
        {
            Block = DjangoBlock.Parse(text, trim: true);
        }

        public override IEnumerable<BlockClassification> GetClassifications()
        {
            return Block != null ? Block.GetSpans() : Enumerable.Empty<BlockClassification>();
        }
    }

    /// <summary>
    /// An artifact representing a Django variable, e.g. <c>{{ content }}</c>.
    /// </summary>
    internal class TemplateVariableArtifact : TemplateArtifact
    {
        public TemplateVariableArtifact(ITextRange range, bool isClosed)
            : base(ArtifactTreatAs.Code, range, isClosed)
        {
        }

        public override TemplateTokenKind TokenKind
        {
            get { return TemplateTokenKind.Variable; }
        }

        /// <summary>
        /// Parsed variable for this artifact. 
        /// </summary>
        /// <remarks>
        /// <see cref="TemplateArtifact.Parse"/> must be called to refresh this value before querying for it.
        /// </remarks>
        public DjangoVariable Variable { get; private set; }

        protected override void Reparse(string text)
        {
            Variable = DjangoVariable.Parse(text);
        }

        public override IEnumerable<BlockClassification> GetClassifications()
        {
            return Variable != null ? Variable.GetSpans() : Enumerable.Empty<BlockClassification>();
        }
    }

    /// <summary>
    /// An artifact representing a Django comment.
    /// </summary>
    internal class TemplateCommentArtifact : TemplateArtifact
    {
        public TemplateCommentArtifact(ITextRange range, bool isClosed)
            : base(ArtifactTreatAs.Comment, range, isClosed)
        {
        }

        public override TemplateTokenKind TokenKind
        {
            get { return TemplateTokenKind.Comment; }
        }

        protected override void Reparse(string text)
        {
        }

        public override IEnumerable<BlockClassification> GetClassifications()
        {
            // HTML editor will automatically classify the entire artifact as "HTML comment" based on the values of
            // <see cref="TemplateArtifactProcessor.LeftCommentSeparator"/> and <see cref="TemplateArtifactProcessor.RightCommentSeparator"/>,
            // so nothing to do here.
            return Enumerable.Empty<BlockClassification>();
        }
    }
}
