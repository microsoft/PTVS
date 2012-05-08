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
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    class HtmlProjectionBuffer : IProjectionEditResolver {
        private readonly ITextBuffer _diskBuffer;           // the buffer as it appears on disk.
        private readonly IProjectionBuffer _projBuffer; // the buffer we project into        
        private readonly IProjectionBufferFactoryService _bufferFactory;
        private readonly List<SpanInfo> _spans = new List<SpanInfo>();
        private readonly IContentTypeRegistryService _contentRegistry;
        private readonly  IBufferGraph _bufferGraph;
        private static string[] _templateTags = new string[] { "{{", "{%", "{#", "#}", "%}", "}}" };

        public HtmlProjectionBuffer(IContentTypeRegistryService contentRegistry, IProjectionBufferFactoryService bufferFactory, ITextBuffer diskBuffer, IBufferGraphFactoryService bufferGraphFactory) {
            _bufferFactory = bufferFactory;
            _diskBuffer = diskBuffer;
            _contentRegistry = contentRegistry;
            
            
            _projBuffer = _bufferFactory.CreateProjectionBuffer(
                this,
                new object[0],
                ProjectionBufferOptions.None
            );
            _bufferGraph = bufferGraphFactory.CreateBufferGraph(_projBuffer);

            var reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(diskBuffer.CurrentSnapshot, new Span(0, diskBuffer.CurrentSnapshot.Length)));
            UpdateTemplateSpans(reader);
        }

        public IProjectionBuffer ProjectionBuffer {
            get {
                return _projBuffer;
            }
        }
        
        public void DiskBufferChanged(object sender, TextContentChangedEventArgs e) {
            foreach (var change in e.Changes) {
                int closest;
                
                if (TryGetSingleStartSpan(e, change, out closest)) {
                    var closestSpan = _spans[closest];
                    int recalcPosition = -1;
                    bool recalc = false;

                    if (closestSpan.Kind == TemplateTokenKind.Text) {
                        // we're in a section of HTML or whatever language the actual template is being applied to.

                        // First, check and see if the user has just pasted some code which includes a template,
                        // in which case we'll recalculate everything.

                        if (!(recalc = ContainsTemplateMarkup(change))) {
                            // Then see if they inserted/deleted text creates a template tag by being merged
                            // with the text around us.
                            if (change.NewSpan.Start != 0 && change.NewSpan.End < e.After.Length) {
                                // Check if the character before us plus the inserted character(s) makes a template tag
                                var newText = e.After.GetText(new Span(change.NewSpan.Start - 1, 2));
                                if (Array.IndexOf(_templateTags, newText) != -1) {
                                    if (closest != 0) {
                                        recalcPosition = _spans[--closest].DiskBufferSpan.GetStartPoint(e.After);
                                    } else {
                                        recalcPosition = _spans[closest].DiskBufferSpan.GetStartPoint(e.After);
                                    }
                                    recalc = true;
                                }
                            }

                            if (!recalc && change.NewSpan.End >= 2) {
                                // check if we inserted template tags at the end
                                var newText = e.After.GetText(new Span(change.NewSpan.End - 2, 2));
                                if (Array.IndexOf(_templateTags, newText) != -1) {
                                    recalcPosition = _spans[closest].DiskBufferSpan.GetStartPoint(e.After);
                                    recalc = true;
                                }
                            }

                            if (!recalc && change.NewSpan.Start + 2 <= e.After.Length) {
                                // check if the inserted char plus the char after us makes a template tag
                                var newText = e.After.GetText(new Span(change.NewSpan.Start, 2));
                                if (Array.IndexOf(_templateTags, newText) != -1) {
                                    recalcPosition = _spans[closest].DiskBufferSpan.GetStartPoint(e.After);
                                    recalc = true;
                                }
                            }
                        }

                        if (!recalc && 
                            change.NewSpan.Start == closestSpan.DiskBufferSpan.GetStartPoint(e.After) && 
                            change.NewSpan.Length == 0) {
                            // finally, if we are just deleting code (change.NewSpan.Length == 0 indicates there's no insertions) from
                            // the start of the buffer then we are definitely deleting a } which means we need to re-calc.  We know
                            // we're deleting the } because it's the last char in the buffer before us because it has
                            // to be the end of a template.
                            if (closest != 0) {
                                closestSpan = _spans[--closest];
                            }
                            recalc = true;
                        }
                    } else {
                        // check if the newly inserted text makes a tag, we include the character before
                        // our span and the character after.
                        Span changeBounds = change.NewSpan;
                        if (changeBounds.Start > 0) {
                            changeBounds = new Span(changeBounds.Start - 1, changeBounds.Length + 1);
                        }
                        if (changeBounds.End < e.After.Length) {
                            changeBounds = new Span(changeBounds.Start, changeBounds.Length + 1);
                        }
                        
                        var newText = e.After.GetText(changeBounds);                        
                        foreach (var tag in _templateTags) {
                            if (newText.Contains(tag)) {
                                recalc = true;
                                break;
                            }
                        }

                        if (!recalc) {
                            var templateStart = closestSpan.DiskBufferSpan.GetStartPoint(e.After);

                            if (change.NewSpan.Start <= templateStart + 1 ||
                                change.NewSpan.End == closestSpan.DiskBufferSpan.GetEndPoint(e.After) - 1) {
                                // we are altering one of the 1st two characters or the last character.  
                                // Because we're a template that could be messing us up.  
                                // We recalcuate from the previous text buffer
                                // because the act of deleting this character could have turned us into a text
                                // buffer, and we need to replace and extend the previous text buffer.
                                recalcPosition = _spans[--closest].DiskBufferSpan.GetStartPoint(e.After);
                                recalc = true;
                            }
                        }
                    }

                    if (recalc) {
                        var start = recalcPosition != -1 ? recalcPosition : closestSpan.DiskBufferSpan.GetStartPoint(e.After).Position;
                        var reader = new SnapshotSpanSourceCodeReader(new SnapshotSpan(e.After, Span.FromBounds(start, e.After.Length)));

                        UpdateTemplateSpans(reader, closest, start);
                    }
                } else {
                    UpdateTemplateSpans(new StringReader(e.After.GetText()));
                }
            }
        }

        private static bool ContainsTemplateMarkup(ITextChange change) {
            bool recalc = false;
            foreach (var tag in _templateTags) {
                if (change.NewText.Contains(tag)) {
                    recalc = true;
                    break;
                }
            }
            return recalc;
        }

        private bool TryGetSingleStartSpan(TextContentChangedEventArgs e, ITextChange change, out int closest) {
            // find the closest region
            var index = _spans.BinarySearch(
                new SpanInfo(new ComparisonTrackingSpan(change.NewSpan.Start, change.NewSpan.End), TemplateTokenKind.None),
                new TrackingSpanComparer(e.After)
            );

            if (index < 0) {
                // no exact match
                closest = ~index;
                if (closest < _spans.Count) {
                    // we're less than some element
                    if (closest != 0 && _spans[closest - 1].DiskBufferSpan.GetStartPoint(e.After) <= change.NewSpan.Start) {
                        // we start in the previous span
                        closest--;
                    }
                } else {
                    // we are in the very last span.
                    closest = _spans.Count - 1;
                }
            } else {
                // exact match, entering at the start of a span...
                closest = index;
            }

            if(closest + 1 < _spans.Count && 
                _spans[closest + 1].DiskBufferSpan.GetStartPoint(e.After).Position <= change.NewSpan.End) {
                // we go across multiple spans (the user is doing a paste with a selection)
                return false;
            }

            return true;
        }


        private void UpdateTemplateSpans(TextReader reader, int startSpan = 0, int offset = 0) {
            var tokenizer = new TemplateTokenizer(reader);
            TemplateToken? token;
            List<object> newProjectionSpans = new List<object>();   // spans in the projection buffer
            List<SpanInfo> newSpanInfos = new List<SpanInfo>();     // our SpanInfo's, with spans in the on-disk buffer
            

            while ((token = tokenizer.GetNextToken()) != null) {
                var curToken = token.Value;
                
                switch (curToken.Kind) {
                    case TemplateTokenKind.Block:
                    case TemplateTokenKind.Comment:
                    case TemplateTokenKind.Variable:
                        // template spans are setup to not grow.  We'll track the edits and update their
                        // text if something causes them to grow.
                        if (newSpanInfos.Count == 0 && startSpan == 0) {
                            // insert a zero-length span which will grow if the user inserts before the template tag
                            var emptySpan = new CustomTrackingSpan(
                                _diskBuffer.CurrentSnapshot,
                                new Span(0, 0),
                                PointTrackingMode.Negative,
                                PointTrackingMode.Positive
                            );
                            newProjectionSpans.Add(emptySpan);
                            newSpanInfos.Add(new SpanInfo(emptySpan, TemplateTokenKind.Text));
                        }

                        var span = new CustomTrackingSpan(
                            _diskBuffer.CurrentSnapshot,
                            Span.FromBounds(curToken.Start + offset, curToken.End + offset + 1),
                            PointTrackingMode.Positive,
                            PointTrackingMode.Negative
                        );

                        newSpanInfos.Add(new SpanInfo(span, curToken.Kind));

                        var contentType = _contentRegistry.GetContentType(TemplateContentType.ContentTypeName);
                        
                        // TODO: Should we only create one of these and share it amongst the buffers?
                        var tempProjectionBuffer = _bufferFactory.CreateProjectionBuffer(this, new object[] { span }, ProjectionBufferOptions.None, contentType);

                        var projSpan = tempProjectionBuffer.CurrentSnapshot.CreateTrackingSpan(
                            new Span(0, tempProjectionBuffer.CurrentSnapshot.Length),
                            SpanTrackingMode.EdgeInclusive
                        );

                        newProjectionSpans.Add(projSpan);
                        break;
                    case TemplateTokenKind.Text:
                        var sourceSpan = Span.FromBounds(curToken.Start + offset, curToken.End + offset + 1);
                        var textSpan = _diskBuffer.CurrentSnapshot.Version.CreateCustomTrackingSpan(
                            Span.FromBounds(curToken.Start + offset, curToken.End + offset + 1),
                            TrackingFidelityMode.Forward,
                            new LanguageSpanCustomState(sourceSpan.Start, sourceSpan.End),
                            TrackToVersion
                        );

                        newProjectionSpans.Add(textSpan);
                        newSpanInfos.Add(new SpanInfo(textSpan, TemplateTokenKind.Text));
                        break;
                }
            }

            if (newSpanInfos.Count == 0 || newSpanInfos[newSpanInfos.Count - 1].Kind != TemplateTokenKind.Text) {
                // insert an empty span at the end which will receive new text.
                var emptySpan = _diskBuffer.CurrentSnapshot.Version.CreateCustomTrackingSpan(
                    new Span(_diskBuffer.CurrentSnapshot.Length, 0),
                    TrackingFidelityMode.Forward,
                    new LanguageSpanCustomState(_diskBuffer.CurrentSnapshot.Length, _diskBuffer.CurrentSnapshot.Length),
                    TrackToVersion
                ); 
                newProjectionSpans.Add(emptySpan);
                newSpanInfos.Add(new SpanInfo(emptySpan, TemplateTokenKind.Text));
            }

            var oldSpanCount = _spans.Count;
            _spans.RemoveRange(startSpan, oldSpanCount - startSpan);
            _spans.AddRange(newSpanInfos);
            _projBuffer.ReplaceSpans(startSpan, oldSpanCount - startSpan, newProjectionSpans, EditOptions.DefaultMinimalChange, null);
        }

        private class LanguageSpanCustomState {
            public int start;       // current start of span
            public int end;         // current end of span
            public bool inelastic;  // when a span becomes inelastic, it will no longer grow due
            // to insertions at its edges
            public LanguageSpanCustomState(int start, int end) {
                this.start = start;
                this.end = end;
            }
        }

        // Taken from Venus HTML editor as-is
        public static Span TrackToVersion(ITrackingSpan customSpan, ITextVersion currentVersion, ITextVersion targetVersion, Span currentSpan, object customState) {
            // We want insertions at the edges of the span to cause it to grow (characters typed at the edge of a nugget or script block
            // should go inside that nugget or block). However, if a replacement overlaps the span but also deletes text outside the span,
            // we do not want the span to grow to include the new text. We will wait for the CBM to reparse the text to determine which parts
            // belong to the embedded language. We need to be conservative, because the embedded language compiler (e.g. VB) thinks it is free
            // to change the text inside the nugget, which is inappropriate if it is really just markup.
            // Once text at the boundary of a span has been deleted, the span can no longer grow on subsequent insertions -- further grow must wait
            // for reparsing.
            LanguageSpanCustomState state = (LanguageSpanCustomState)customState;
            ITextVersion v = currentVersion;
            int start = state.start;
            int end = state.end;
            Span current = Span.FromBounds(start, end);
            if (targetVersion.VersionNumber > currentVersion.VersionNumber) {
                // map forward in time
                while (v != targetVersion) {
                    int changeCount = v.Changes.Count;
                    for (int c = 0; c < changeCount; ++c) {
                        ITextChange textChange = v.Changes[c];
                        Span deletedSpan = new Span(textChange.NewPosition, textChange.OldLength);

                        if (deletedSpan.End < start) {
                            // the whole thing is before our span. shift.
                            start += textChange.Delta;
                            end += textChange.Delta;
                        } else if (current.Contains(deletedSpan)) {
                            // Our span subsumes the whole thing. shrink or grow, unless
                            // we are dead and this is an insertion at a boundary
                            if (state.inelastic && deletedSpan.Length == 0 && textChange.NewPosition == start) {
                                start += textChange.Delta;
                                end += textChange.Delta;
                            } else if (state.inelastic && deletedSpan.Length == 0 && textChange.NewPosition == end) {
                                break;
                            } else {
                                end += textChange.Delta;
                            }
                        } else if (end <= textChange.NewPosition) {
                            // the whole thing is to our right - no impact on us.
                            // since changes are sorted, we are done with this version.
                            break;
                        } else {
                            // there is overlap of the OldSpan and our span, but it is not
                            // a subset. We don't want to include any new text. 
                            // this span can never again absorb an insertion at its boundary
                            state.inelastic = true;
                            if (deletedSpan.Start <= start && deletedSpan.End >= end) {
                                // a proper superset of our span was deleted (we already handled
                                // the case where exactly our span was deleted).
                                start = textChange.NewEnd;
                                end = textChange.NewEnd;
                            } else if (deletedSpan.End < end) {
                                // the deletion overlaps start but not end. 
                                start = textChange.NewEnd;
                                end += textChange.Delta;
                            } else {
                                // the deletion overlaps end but not start.
                                // start doesn't change
                                end = textChange.NewPosition;
                            }
                        }
                        current = Span.FromBounds(start, end);
                    }
                    v = v.Next;
                }
            } else {
                Debug.Fail("Mapping language span backward in time!");
                // map backwards. we don't claim to do anything useful.
                return current;
            }
            state.start = start;
            state.end = end;
            return current;
        }

        struct SpanInfo {
            public readonly ITrackingSpan DiskBufferSpan;
            public readonly TemplateTokenKind Kind;

            public SpanInfo(ITrackingSpan diskBufferSpan, TemplateTokenKind kind) {
                DiskBufferSpan = diskBufferSpan;
                Kind = kind;
            }
        }

        class ComparisonTrackingSpan : ITrackingSpan {
            private readonly int _start, _end;
            public ComparisonTrackingSpan(int start, int end) {
                _start = start;
                _end = end;
            }

            #region ITrackingSpan Members

            public SnapshotPoint GetEndPoint(ITextSnapshot snapshot) {
                throw new NotImplementedException();
            }

            public Span GetSpan(ITextVersion version) {
                return Span.FromBounds(_start, _end);
            }

            public SnapshotSpan GetSpan(ITextSnapshot snapshot) {
                throw new NotImplementedException();
            }

            public SnapshotPoint GetStartPoint(ITextSnapshot snapshot) {
                throw new NotImplementedException();
            }

            public string GetText(ITextSnapshot snapshot) {
                throw new NotImplementedException();
            }

            public ITextBuffer TextBuffer {
                get { throw new NotImplementedException(); }
            }

            public TrackingFidelityMode TrackingFidelity {
                get { throw new NotImplementedException(); }
            }

            public SpanTrackingMode TrackingMode {
                get { throw new NotImplementedException(); }
            }

            #endregion;
        }

        class TrackingSpanComparer : IComparer<SpanInfo> {
            private readonly ITextSnapshot _snapshot;

            public TrackingSpanComparer(ITextSnapshot snapshot) {
                _snapshot = snapshot;
            }

            #region IComparer<ITrackingSpan> Members

            public int Compare(SpanInfo x, SpanInfo y) {
                var xSpan = x.DiskBufferSpan;
                var ySpan = y.DiskBufferSpan;

                return xSpan.GetSpan(_snapshot.Version).Start - ySpan.GetSpan(_snapshot.Version).Start;
            }

            #endregion
        }

        #region IProjectionEditResolver Members

        public void FillInInsertionSizes(SnapshotPoint projectionInsertionPoint, System.Collections.ObjectModel.ReadOnlyCollection<SnapshotPoint> sourceInsertionPoints, string insertionText, IList<int> insertionSizes) {
        }

        public void FillInReplacementSizes(SnapshotSpan projectionReplacementSpan, System.Collections.ObjectModel.ReadOnlyCollection<SnapshotSpan> sourceReplacementSpans, string insertionText, IList<int> insertionSizes) {            
            for (int i = 0; i < sourceReplacementSpans.Count; i++) {
                var span = sourceReplacementSpans[i];
                if (span.Snapshot.TextBuffer == _diskBuffer) {
                    insertionSizes[i] += insertionText.Length;
                    break;
                } 
            }
        }

        public int GetTypicalInsertionPosition(SnapshotPoint projectionInsertionPoint, System.Collections.ObjectModel.ReadOnlyCollection<SnapshotPoint> sourceInsertionPoints) {
            return sourceInsertionPoints[0];
        }

        #endregion
    }
}
