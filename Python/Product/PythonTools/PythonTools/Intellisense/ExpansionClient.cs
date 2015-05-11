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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Intellisense {
    class ExpansionClient : IVsExpansionClient {
        private readonly IVsTextLines _lines;
        private readonly IVsExpansion _expansion;
        private readonly IVsTextView _view;
        private readonly ITextView _textView;
        private readonly IVsEditorAdaptersFactoryService _adapterFactory;
        private readonly IServiceProvider _serviceProvider;
        private IVsExpansionSession _session;
        private bool _sessionEnded, _selectEndSpan;
        private ITrackingPoint _selectionStart, _selectionEnd;

        public const string SurroundsWith = "SurroundsWith";
        public const string SurroundsWithStatement = "SurroundsWithStatement";
        public const string Expansion = "Expansion";

        public ExpansionClient(ITextView textView, IVsEditorAdaptersFactoryService adapterFactory, IServiceProvider serviceProvider) {
            _textView = textView;
            _serviceProvider = serviceProvider;
            _adapterFactory = adapterFactory;
            _view = _adapterFactory.GetViewAdapter(_textView);
            _lines = (IVsTextLines)_adapterFactory.GetBufferAdapter(_textView.TextBuffer);
            _expansion = _lines as IVsExpansion;
            if (_expansion == null) {
                throw new ArgumentException("TextBuffer does not support expansions");
            }
        }

        public bool InSession {
            get {
                return _session != null;
            }
        }

        public int EndExpansion() {
            _session = null;
            _sessionEnded = true;
            _selectionStart = _selectionEnd = null;
            return VSConstants.S_OK;
        }

        class ImportWalker : PythonWalker {
            public readonly HashSet<string> Imports = new HashSet<string>();
            
            public override bool Walk(ImportStatement node) {
                for (int i = 0; i < node.Names.Count; i++) {
                    // if it's an asname we don't understand it
                    if (node.Names[i] == null) {
                        // bad import...
                        continue;
                    }
                    if (node.AsNames[i] == null) { 
                        Imports.Add(node.Names[i].MakeString());
                    }
                }
                return base.Walk(node);
            }
        }

        public int FormatSpan(IVsTextLines pBuffer, TextSpan[] ts) {
            MSXML.IXMLDOMNode codeNode, snippetTypes, declarations, imports;

            int hr;
            if (ErrorHandler.Failed(hr = _session.GetSnippetNode("CodeSnippet:Code", out codeNode))) {
                return hr;
            }

            if (ErrorHandler.Failed(hr = _session.GetHeaderNode("CodeSnippet:SnippetTypes", out snippetTypes))) {
                return hr;
            }

            List<string> declList = new List<string>();
            if (ErrorHandler.Succeeded(hr = _session.GetSnippetNode("CodeSnippet:Declarations", out declarations)) 
                && declarations != null) {
                foreach (MSXML.IXMLDOMNode declType in declarations.childNodes) {
                    var id = declType.selectSingleNode("./CodeSnippet:ID");
                    if (id != null) {
                        declList.Add(id.text);
                    }
                }
            }

            List<string> importList = new List<string>();
            if (ErrorHandler.Succeeded(hr = _session.GetSnippetNode("CodeSnippet:Imports", out imports)) 
                && imports != null) {
                foreach (MSXML.IXMLDOMNode import in imports.childNodes) {
                    var id = import.selectSingleNode("./CodeSnippet:Namespace");
                    if (id != null) {
                        importList.Add(id.text);
                    }
                }
            }

            bool surroundsWith = false, surroundsWithStatement = false;
            foreach (MSXML.IXMLDOMNode snippetType in snippetTypes.childNodes) {
                if (snippetType.nodeName == "SnippetType") {
                    if (snippetType.text == SurroundsWith) {
                        surroundsWith = true;
                    } else if (snippetType.text == SurroundsWithStatement) {
                        surroundsWithStatement = true;
                    }
                }
            }

            // get the indentation of where we're inserting the code...
            string baseIndentation = GetBaseIndentation(ts);

            TextSpan? endSpan = null;
            using (var edit = _textView.TextBuffer.CreateEdit()) {
                if (surroundsWith || surroundsWithStatement) {
                    // this is super annoyning...  Most languages can do a surround with and $selected$ can be
                    // an empty string and everything's the same.  But in Python we can't just have something like
                    // "while True: " without a pass statement.  So if we start off with an empty selection we
                    // need to insert a pass statement.  This is the purpose of the "SurroundsWithStatement"
                    // snippet type.
                    //
                    // But, to make things even more complicated, we don't have a good indication of what's the 
                    // template text vs. what's the selected text.  We do have access to the original template,
                    // but all of the values have been replaced with their default values when we get called
                    // here.  So we need to go back and re-apply the template, except for the $selected$ part.
                    //
                    // Also, the text has \n, but the inserted text has been replaced with the appropriate newline
                    // character for the buffer.
                    var templateText = codeNode.text.Replace("\n", _textView.Options.GetNewLineCharacter());
                    foreach (var decl in declList) {
                        string defaultValue;
                        if (ErrorHandler.Succeeded(_session.GetFieldValue(decl, out defaultValue))) {
                            templateText = templateText.Replace("$" + decl + "$", defaultValue);
                        }
                    }
                    templateText = templateText.Replace("$end$", "");

                    // we can finally figure out where the selected text began witin the original template...
                    int selectedIndex = templateText.IndexOf("$selected$");
                    if (selectedIndex != -1) {
                        var selection = _textView.Selection;
                        
                        // now we need to get the indentation of the $selected$ element within the template,
                        // as we'll need to indent the selected code to that level.
                        string indentation = GetTemplateSelectionIndentation(templateText, selectedIndex);

                        var start = _selectionStart.GetPosition(_textView.TextBuffer.CurrentSnapshot);
                        var end = _selectionEnd.GetPosition(_textView.TextBuffer.CurrentSnapshot);
                        if (end < start) {
                            // we didn't actually have a selction, and our negative tracking pushed us
                            // back to the start of the buffer...
                            end = start;
                        }
                        var selectedSpan = Span.FromBounds(start, end);

                        if (surroundsWithStatement && 
                            String.IsNullOrWhiteSpace(_textView.TextBuffer.CurrentSnapshot.GetText(selectedSpan))) {
                            // we require a statement here and the user hasn't selected any code to surround,
                            // so we insert a pass statement (and we'll select it after the completion is done)
                            int startPosition;
                            pBuffer.GetPositionOfLineIndex(ts[0].iStartLine, ts[0].iStartIndex, out startPosition);
                            edit.Replace(new Span(startPosition + selectedIndex, end - start), "pass");

                            // Surround With can be invoked with no selection, but on a line with some text.
                            // In that case we need to inject an extra new line.
                            var endLine = _textView.TextBuffer.CurrentSnapshot.GetLineFromPosition(end);
                            var endText = endLine.GetText().Substring(end - endLine.Start);
                            if (!String.IsNullOrWhiteSpace(endText)) {
                                edit.Insert(end, _textView.Options.GetNewLineCharacter());
                            }

                            // we want to leave the pass statement selected so the user can just
                            // continue typing over it...
                            var startLine = _textView.TextBuffer.CurrentSnapshot.GetLineFromPosition(startPosition + selectedIndex);                            
                            _selectEndSpan = true;
                            endSpan = new TextSpan() {
                                iStartLine = startLine.LineNumber,
                                iEndLine = startLine.LineNumber,
                                iStartIndex = baseIndentation.Length + indentation.Length,
                                iEndIndex = baseIndentation.Length + indentation.Length + 4,
                            };
                        }

                        IndentSpan(
                            edit, 
                            indentation,
                            _textView.TextBuffer.CurrentSnapshot.GetLineFromPosition(start).LineNumber + 1, // 1st line is already indented
                            _textView.TextBuffer.CurrentSnapshot.GetLineFromPosition(end).LineNumber
                        );
                    }
                }

                // we now need to update any code which was not selected  that we just inserted.
                IndentSpan(edit, baseIndentation, ts[0].iStartLine + 1, ts[0].iEndLine);

                edit.Apply();
            }

            if (endSpan != null) {
                _session.SetEndSpan(endSpan.Value);
            }

            // add any missing imports...
            AddMissingImports(importList);

            return hr;
        }

        private void AddMissingImports(List<string> importList) {
            if (importList.Count > 0) {
                var projEntry = _textView.TextBuffer.GetPythonProjectEntry();
                if (projEntry != null) {
                    PythonAst ast;
                    IAnalysisCookie cookie;
                    projEntry.GetTreeAndCookie(out ast, out cookie);
                    if (ast != null) {
                        var walker = new ImportWalker();
                        ast.Walk(walker);

                        foreach (var import in importList) {
                            if (!walker.Imports.Contains(import)) {
                                MissingImportAnalysis.AddImport(
                                    _serviceProvider,
                                    _textView.TextBuffer,
                                    _textView,
                                    null,
                                    import
                                );
                            }
                        }
                    }
                }
            }
        }

        private static string GetTemplateSelectionIndentation(string templateText, int selectedIndex) {
            string indentation = "";
            for (int i = selectedIndex - 1; i >= 0; i--) {
                if (templateText[i] != '\t' && templateText[i] != ' ') {
                    indentation = templateText.Substring(i + 1, selectedIndex - i - 1);
                    break;
                }
            }
            return indentation;
        }

        private string GetBaseIndentation(TextSpan[] ts) {
            var indentationLine = _textView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(ts[0].iStartLine).GetText();
            string baseIndentation = indentationLine;
            for (int i = 0; i < indentationLine.Length; i++) {
                if (indentationLine[i] != ' ' && indentationLine[i] != '\t') {
                    baseIndentation = indentationLine.Substring(0, i);
                    break;
                }
            }
            return baseIndentation;
        }

        private void IndentSpan(ITextEdit edit, string indentation, int startLine, int endLine) {
            var snapshot = _textView.TextBuffer.CurrentSnapshot;
            for (int i = startLine; i <= endLine; i++) {
                var curline = snapshot.GetLineFromLineNumber(i);
                edit.Insert(curline.Start, indentation);
            }
        }

        public int GetExpansionFunction(MSXML.IXMLDOMNode xmlFunctionNode, string bstrFieldName, out IVsExpansionFunction pFunc) {
            pFunc = null;
            return VSConstants.S_OK;
        }

        public int IsValidKind(IVsTextLines pBuffer, TextSpan[] ts, string bstrKind, out int pfIsValidKind) {
            pfIsValidKind = 1;
            return VSConstants.S_OK;
        }

        public int IsValidType(IVsTextLines pBuffer, TextSpan[] ts, string[] rgTypes, int iCountTypes, out int pfIsValidType) {
            pfIsValidType = 1;
            return VSConstants.S_OK;
        }

        public int OnAfterInsertion(IVsExpansionSession pSession) {
            return VSConstants.S_OK;
        }

        public int OnBeforeInsertion(IVsExpansionSession pSession) {
            _session = pSession;
            return VSConstants.S_OK;
        }

        public int OnItemChosen(string pszTitle, string pszPath) {
            int caretLine, caretColumn;
            GetCaretPosition(out caretLine, out caretColumn);

            var textSpan = new TextSpan() { iStartLine = caretLine, iStartIndex = caretColumn, iEndLine = caretLine, iEndIndex = caretColumn };
            return InsertNamedExpansion(pszTitle, pszPath, textSpan);
        }

        public int InsertNamedExpansion(string pszTitle, string pszPath, TextSpan textSpan) {
            if (_session != null) {
                // if the user starts an expansion session while one is in progress
                // then abort the current expansion session
                _session.EndCurrentExpansion(1);
                _session = null;
            }

            var selection = _textView.Selection;
            var snapshot = selection.Start.Position.Snapshot;

            _selectionStart = snapshot.CreateTrackingPoint(selection.Start.Position, VisualStudio.Text.PointTrackingMode.Positive);
            _selectionEnd = snapshot.CreateTrackingPoint(selection.End.Position, VisualStudio.Text.PointTrackingMode.Negative);
            _selectEndSpan = _sessionEnded = false;

            int hr = _expansion.InsertNamedExpansion(
                pszTitle,
                pszPath,
                textSpan,
                this,
                GuidList.guidPythonLanguageServiceGuid,
                0,
                out _session
            );

            if (ErrorHandler.Succeeded(hr)) {
                if (_sessionEnded) {
                    _session = null;
                }
            }
            return hr;
        }

        public int NextField() {
            return _session.GoToNextExpansionField(0);
        }

        public int PreviousField() {
            return _session.GoToPreviousExpansionField();
        }

        public int EndCurrentExpansion(bool leaveCaret) {
            if (_selectEndSpan) {
                TextSpan[] endSpan = new TextSpan[1];
                if (ErrorHandler.Succeeded(_session.GetEndSpan(endSpan))) {
                    var snapshot = _textView.TextBuffer.CurrentSnapshot;
                    var startLine = snapshot.GetLineFromLineNumber(endSpan[0].iStartLine);
                    var span = new Span(startLine.Start + endSpan[0].iStartIndex, 4);
                    _textView.Caret.MoveTo(new SnapshotPoint(snapshot, span.Start));
                    _textView.Selection.Select(new SnapshotSpan(_textView.TextBuffer.CurrentSnapshot, span), false);
                    return _session.EndCurrentExpansion(1);
                }
            }
            return _session.EndCurrentExpansion(leaveCaret ? 1 : 0);
        }

        public int PositionCaretForEditing(IVsTextLines pBuffer, TextSpan[] ts) {
            return VSConstants.S_OK;
        }

        private void GetCaretPosition(out int caretLine, out int caretColumn) {
            ErrorHandler.ThrowOnFailure(_view.GetCaretPos(out caretLine, out caretColumn));

            // Handle virtual space
            int lineLength;
            ErrorHandler.ThrowOnFailure(_lines.GetLengthOfLine(caretLine, out lineLength));

            if (caretColumn > lineLength) {
                caretColumn = lineLength;
            }
        }
    }
}
