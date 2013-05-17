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
using System.IO;
using System.Diagnostics;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    class TemplateTokenizer {
        private readonly TextReader _reader;
        private TemplateToken? _readToken;
        private int _position;

        public TemplateTokenizer(TextReader reader) {
            _reader = reader;
        }

        public IEnumerable<TemplateToken> GetTokens() {
            for (; ; ) {
                var next = GetNextToken();
                if (next != null) {
                    yield return next.Value;
                } else {
                    break;
                }
            }
        }

        public TemplateToken? GetNextToken() {
            int start = _position;
            TemplateToken? res;
            int curChar;
            if (_readToken != null) {
                res = _readToken;
                _readToken = null;
                return res;
            } else if ((curChar = ReadChar()) == '{') {
                res = TryReadTemplateTag(_position - 1);
                if (res != null) {
                    return res;
                }
            } else if (curChar == -1) {
                return null;
            }

            // not a tag, read until we get to a tag, or we hit EOF.
            for (; ; ) {
                switch (ReadChar()) {
                    case '{':
                        // we have to parse the whole tag to make sure it has a valid end, otherwise it's text.
                        Debug.Assert(_position >= 2, "We've read 2 chars, once to check for the initial {, and now a 2nd to check for the 2nd {");
                        int end = _position - 2;
                        res = TryReadTemplateTag(_position - 1);
                        if (res != null) {
                            _readToken = res;
                            return new TemplateToken(TemplateTokenKind.Text, start, end);
                        }                        
                        break;
                    case -1:
                        // EOF
                        return new TemplateToken(TemplateTokenKind.Text, start, _position - 1);
                }
            }
            
        }

        private TemplateToken? TryReadTemplateTag(int start) {
            switch (ReadChar()) {
                case '%':   // block tag start
                    return ReadToClose(start, TemplateTokenKind.Block, '%');
                case '#':   // comment tag start
                    return ReadToClose(start, TemplateTokenKind.Comment, '#');
                case '{':   // variable tag start
                    return ReadToClose(start, TemplateTokenKind.Variable, '}');
                default:
                    return null;
            }
        }

        public TemplateToken? ReadToClose(int start, TemplateTokenKind kind, char closeType) {
            var prevChar = ReadChar();            
            for (; ; ) {               
                if (prevChar == -1) {
                    return new TemplateToken(kind, start, _position - 1);
                } else if (prevChar == closeType) {
                    if ((prevChar = ReadChar()) == '}') {
                        // we're done
                        return new TemplateToken(kind, start, _position - 1);
                    }
                } else {
                    prevChar = ReadChar();
                }
            }            
        }

        private int ReadChar() {
            int res = _reader.Read();
            if (res != -1) {
                _position++;
            }
            return res;
        }

        private int PeekChar() {
            return _reader.Peek();
        }
    }
}
