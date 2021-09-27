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

using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Debugger
{
    class PythonStackFrame
    {
        private int _lineNo;    // mutates on set next line
        private readonly string _frameName, _filename;
        private readonly int _argCount, _frameId;
        private readonly int _startLine, _endLine;
        private PythonEvaluationResult[] _variables;
        private readonly PythonThread _thread;
        private readonly FrameKind _kind;

        public PythonStackFrame(PythonThread thread, string frameName, string filename, int startLine, int endLine, int lineNo, int argCount, int frameId, FrameKind kind)
        {
            _thread = thread;
            _frameName = frameName;
            _filename = filename;
            _argCount = argCount;
            _lineNo = lineNo;
            _frameId = frameId;
            _startLine = startLine;
            _endLine = endLine;
            _kind = kind;
        }

        /// <summary>
        /// The line nubmer where the current function/class/module starts
        /// </summary>
        public int StartLine
        {
            get
            {
                return _startLine;
            }
        }

        /// <summary>
        /// The line number where the current function/class/module ends.
        /// </summary>
        public int EndLine
        {
            get
            {
                return _endLine;
            }
        }

        public PythonThread Thread
        {
            get
            {
                return _thread;
            }
        }

        public int LineNo
        {
            get
            {
                return _lineNo;
            }
            set
            {
                _lineNo = value;
            }
        }

        public string FunctionName
        {
            get
            {
                return _frameName;
            }
        }

        public string FileName
        {
            get
            {
                return _thread.Process.MapFile(_filename, toDebuggee: false);
            }
        }

        public FrameKind Kind
        {
            get
            {
                return _kind;
            }
        }

        /// <summary>
        /// Gets the ID of the frame.  Frame 0 is the currently executing frame, 1 is the caller of the currently executing frame, etc...
        /// </summary>
        public int FrameId
        {
            get
            {
                return _frameId;
            }
        }

        internal void SetVariables(PythonEvaluationResult[] variables)
        {
            _variables = variables;
        }

        public IList<PythonEvaluationResult> Locals
        {
            get
            {
                PythonEvaluationResult[] res = new PythonEvaluationResult[_variables.Length - _argCount];
                for (int i = _argCount; i < _variables.Length; i++)
                {
                    res[i - _argCount] = _variables[i];
                }
                return res;
            }
        }

        public IList<PythonEvaluationResult> Parameters
        {
            get
            {
                PythonEvaluationResult[] res = new PythonEvaluationResult[_argCount];
                for (int i = 0; i < _argCount; i++)
                {
                    res[i] = _variables[i];
                }
                return res;
            }
        }

        /// <summary>
        /// Attempts to parse the given text.  Returns true if the text is a valid expression.  Returns false if the text is not
        /// a valid expression and assigns the error messages produced to errorMsg.
        /// </summary>
        public virtual bool TryParseText(string text, out string errorMsg)
        {
            CollectingErrorSink errorSink = new CollectingErrorSink();
            Parser parser = Parser.CreateParser(new StringReader(text), _thread.Process.LanguageVersion, new ParserOptions() { ErrorSink = errorSink });
            var ast = parser.ParseSingleStatement();
            if (errorSink.Errors.Count > 0)
            {
                StringBuilder msg = new StringBuilder();
                foreach (var error in errorSink.Errors)
                {
                    msg.Append(error.Message);
                    msg.Append(Environment.NewLine);
                }

                errorMsg = msg.ToString();
                return false;
            }

            errorMsg = null;
            return true;
        }

        /// <summary>
        /// Executes the given text against this stack frame.
        /// </summary>
        public Task ExecuteTextAsync(string text, Action<PythonEvaluationResult> completion, CancellationToken ct)
        {
            return ExecuteTextAsync(text, PythonEvaluationResultReprKind.Normal, completion, ct);
        }

        public Task ExecuteTextAsync(string text, PythonEvaluationResultReprKind reprKind, Action<PythonEvaluationResult> completion, CancellationToken ct)
        {
            return _thread.Process.ExecuteTextAsync(text, reprKind, this, false, completion, ct);
        }

        public async Task<PythonEvaluationResult> ExecuteTextAsync(string text, PythonEvaluationResultReprKind reprKind = PythonEvaluationResultReprKind.Normal, CancellationToken ct = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<PythonEvaluationResult>();
            var cancellationRegistration = ct.Register(() => tcs.TrySetCanceled());

            EventHandler<ProcessExitedEventArgs> processExited = delegate
            {
                tcs.TrySetCanceled();
            };

            _thread.Process.ProcessExited += processExited;
            try
            {
                await ExecuteTextAsync(text, reprKind, result => tcs.TrySetResult(result), ct);
                return await tcs.Task;
            }
            finally
            {
                _thread.Process.ProcessExited -= processExited;
                cancellationRegistration.Dispose();
            }
        }

        /// <summary>
        /// Sets the line number that this current frame is executing.  Returns true
        /// if the line was successfully set or false if the line number cannot be changed
        /// to this line.
        /// </summary>
        public Task<bool> SetLineNumber(int lineNo, CancellationToken ct)
        {
            return _thread.Process.SetLineNumberAsync(this, lineNo, ct);
        }

        public string GetQualifiedFunctionName()
        {
            return GetQualifiedFunctionName(_thread.Process, FileName, LineNo, FunctionName);
        }

        public static string GetQualifiedFunctionName(PythonProcess process, string filename, int lineNo, string functionName)
        {
            var ast = process.GetAst(filename);
            if (ast == null)
            {
                return functionName;
            }

            return QualifiedFunctionNameWalker.GetDisplayName(
                lineNo,
                functionName,
                ast,
                (a, n) => string.IsNullOrEmpty(a) ? n : Strings.DebugStackFrameNameInName.FormatUI(n, a)
            );
        }
    }

    class DjangoStackFrame : PythonStackFrame
    {
        private readonly string _sourceFile;
        private readonly int _sourceLine;

        public DjangoStackFrame(PythonThread thread, string frameName, string filename, int startLine, int endLine, int lineNo, int argCount, int frameId, string sourceFile, int sourceLine)
            : base(thread, frameName, filename, startLine, endLine, lineNo, argCount, frameId, FrameKind.Django)
        {
            _sourceFile = sourceFile;
            _sourceLine = sourceLine;
        }

        /// <summary>
        /// The source .py file which implements the template logic.  The normal filename is the
        /// name of the template it's self.
        /// </summary>
        public string SourceFile
        {
            get
            {
                return _sourceFile;
            }
        }

        /// <summary>
        /// The line in the source .py file which implements the template logic.
        /// </summary>
        public int SourceLine
        {
            get
            {
                return _sourceLine;
            }
        }
    }

}
