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
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Repl {
    /// <summary>
    /// Any <see cref="ITextBuffer"/> with content type <see cref="ReplConstants.ReplContentTypeName"/>, role <see cref="ReplConstants.ReplTextViewRole"/> 
    /// and our ReplWindow object gets prompt glyphs in its glyph margin.
    ///
    /// Implements prompt glyphs in a GlyphMargin. 
    /// </summary>
    internal static class ReplGlyphPrompts {
        internal const string GlyphName = "ReplPromptGlyph";

        internal sealed class ReplGlyphTag : IGlyphTag {
            internal static readonly ReplGlyphTag MainPrompt = new ReplGlyphTag();
            internal static readonly ReplGlyphTag SecondaryPrompt = new ReplGlyphTag();
            internal static readonly ReplGlyphTag InputPrompt = new ReplGlyphTag();
        }

        internal sealed class Tagger : ITagger<ReplGlyphTag> {
            private readonly ReplWindow/*!*/ _promptProvider;

            public Tagger(ReplWindow/*!*/ promptProvider) {
                Contract.Assert(promptProvider != null);
                _promptProvider = promptProvider;
                _promptProvider.PromptChanged += new Action<SnapshotSpan>((span) => {
                    var tagsChanged = TagsChanged;
                    if (tagsChanged != null) {
                        tagsChanged(this, new SnapshotSpanEventArgs(span));
                    }
                });
            }

            public IEnumerable<ITagSpan<ReplGlyphTag>>/*!*/ GetTags(NormalizedSnapshotSpanCollection/*!*/ spans) {
                foreach (SnapshotSpan span in spans) {
                    switch (_promptProvider.HasPromptForLine(span.Snapshot, span.Start.GetContainingLine().LineNumber)) {
                        case ReplSpanKind.Prompt:
                            yield return new TagSpan<ReplGlyphTag>(span, ReplGlyphTag.MainPrompt);
                            break;
                        case ReplSpanKind.SecondaryPrompt:
                            yield return new TagSpan<ReplGlyphTag>(span, ReplGlyphTag.SecondaryPrompt);
                            break;
                        case ReplSpanKind.StandardInputPrompt:
                            yield return new TagSpan<ReplGlyphTag>(span, ReplGlyphTag.InputPrompt);
                            break;
                    }
                }
            }

            public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        }

        [Export(typeof(ITaggerProvider))]
        [TagType(typeof(ReplGlyphTag))]
        [ContentType(ReplConstants.ReplContentTypeName)]
        [TextViewRole(ReplConstants.ReplTextViewRole)]
        internal sealed class TaggerProvider : ITaggerProvider {
            public ITagger<T> CreateTagger<T>(ITextBuffer/*!*/ buffer) where T : ITag {
                ReplWindow promptProvider;
                if (buffer.Properties.TryGetProperty(typeof(ReplWindow), out promptProvider)) {
                    return (ITagger<T>)(object)new Tagger(promptProvider);
                }
                return null;
            }
        }

        internal sealed class GlyphFactory : IGlyphFactory {
            private readonly ReplWindow/*!*/ _promptProvider;
            private static readonly FontFamily _Consolas = new FontFamily("Consolas");

            public GlyphFactory(ReplWindow/*!*/ promptProvider) {
                Contract.Assert(promptProvider != null);
                _promptProvider = promptProvider;
            }

            public UIElement/*!*/ GenerateGlyph(IWpfTextViewLine/*!*/ line, IGlyphTag tag) {                
                TextBlock block = new TextBlock();
                if (tag == ReplGlyphTag.MainPrompt) {
                    block.Text = _promptProvider.Prompt;
                } else if (tag == ReplGlyphTag.SecondaryPrompt) {
                    block.Text = _promptProvider.SecondaryPrompt;
                } else {
                    block.Text = _promptProvider.InputPrompt;
                }
                block.Foreground = _promptProvider.HostControl.Foreground;
                block.FontSize = _promptProvider.HostControl.FontSize;
                block.FontFamily = _Consolas; // TODO: get the font family from the editor?
                return block;
            }
        }

        [Export(typeof(IGlyphFactoryProvider))]
        [Name(GlyphName)]
        [Order(After = "VsTextMarker")]
        [TagType(typeof(ReplGlyphTag))]
        [ContentType(ReplConstants.ReplContentTypeName)]
        [TextViewRole(ReplConstants.ReplTextViewRole)]
        internal sealed class GlyphFactoryProvider : IGlyphFactoryProvider {
            public IGlyphFactory GetGlyphFactory(IWpfTextView/*!*/ view, IWpfTextViewMargin/*!*/ margin) {
                ReplWindow promptProvider;
                if (view.TextBuffer.Properties.TryGetProperty(typeof(ReplWindow), out promptProvider)) {
                    return new GlyphFactory(promptProvider);
                }
                return null;
            }
        }
    }
}
