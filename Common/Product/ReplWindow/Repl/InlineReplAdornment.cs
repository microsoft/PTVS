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
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Repl {
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(IntraTextAdornmentTag))]
    [ContentType(ReplConstants.ReplContentTypeName)]
    internal class InlineReplAdornmentProvider : IViewTaggerProvider {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
            if (buffer == null || textView == null || typeof(T) != typeof(IntraTextAdornmentTag)) {
                return null;
            }
            
            return (ITagger<T>)textView.Properties.GetOrCreateSingletonProperty<InlineReplAdornmentManager>(
                typeof(InlineReplAdornmentManager),
                () => new InlineReplAdornmentManager(textView)
                );
        }

        class InlineReplAdornmentManager : ITagger<IntraTextAdornmentTag> {
            private readonly ITextView _textView;
            private readonly List<Tuple<int, ZoomableInlineAdornment>> _tags;
            private readonly Dispatcher _dispatcher;

            internal InlineReplAdornmentManager(ITextView textView) {
                _textView = textView;
                _tags = new List<Tuple<int, ZoomableInlineAdornment>>();
                _dispatcher = Dispatcher.CurrentDispatcher;
            }

            public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
                var result = new List<TagSpan<IntraTextAdornmentTag>>();
                foreach (var t in _tags) {
                    var span = new SnapshotSpan(_textView.TextSnapshot, t.Item1, 0);
                    t.Item2.UpdateSize();
                    var tag = new IntraTextAdornmentTag(t.Item2, null);
                    result.Add(new TagSpan<IntraTextAdornmentTag>(span, tag));
                }
                return result;
            }

            public void AddAdornment(ZoomableInlineAdornment uiElement) {
                if (Dispatcher.CurrentDispatcher != _dispatcher) {
                    _dispatcher.BeginInvoke(new Action(() => AddAdornment(uiElement)));
                    return;
                }
                var caretPos = _textView.Caret.Position.BufferPosition;
                var caretLine = caretPos.GetContainingLine();
                _tags.Add(new Tuple<int, ZoomableInlineAdornment>(caretPos.Position, uiElement));
                var handler = TagsChanged;
                if (handler != null) {
                    var span = new SnapshotSpan(_textView.TextSnapshot, caretLine.Start, caretLine.Length);
                    var args = new SnapshotSpanEventArgs(span);
                    handler(this, args);
                }
            }

            public IList<Tuple<int, ZoomableInlineAdornment>> Adornments {
                get { return _tags; }
            }

            public void RemoveAll() {
                _tags.Clear();
            }

            public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        }

        private static InlineReplAdornmentManager GetManager(ITextView view) {
            InlineReplAdornmentManager result;
            if (!view.Properties.TryGetProperty<InlineReplAdornmentManager>(typeof(InlineReplAdornmentManager), out result)) {
                return null;
            }
            return result;
        }

        public static void AddInlineAdornment(ITextView view, UIElement uiElement, RoutedEventHandler onLoaded) {
            var manager = GetManager(view);
            if (manager != null) {
                var adornment = new ZoomableInlineAdornment(uiElement, view);
                // Original Python code unhooked this event after load was complete
                // I don't think this should be needed... we'll see.
                adornment.Loaded += onLoaded;
                manager.AddAdornment(adornment);
            }
        }

        public static void ZoomInlineAdornments(ITextView view, double zoomFactor) {
            var manager = GetManager(view);
            if (manager != null) {
                foreach (var t in manager.Adornments) {
                    t.Item2.Zoom(zoomFactor);
                }
            }
        }

        public static void MinimizeLastInlineAdornment(ITextView view) {
            var manager = GetManager(view);
            if (manager != null && manager.Adornments.Count > 0) {
                var adornment = manager.Adornments[manager.Adornments.Count - 1].Item2;
                adornment.Zoom(adornment.MinimizedZoom);
            }
        }

        public static void RemoveAllAdornments(ITextView view) {
            var manager = GetManager(view);
            if (manager != null) {
                manager.RemoveAll();
            }
        }
    }
}
