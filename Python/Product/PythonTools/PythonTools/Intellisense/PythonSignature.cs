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

using AP = Microsoft.PythonTools.Intellisense.AnalysisProtocol;

namespace Microsoft.PythonTools.Intellisense {
    internal class PythonSignature : ISignature {
        private readonly ITrackingSpan _span;
        private readonly string _documentation;
        private readonly AP.Signature _overload;
        private readonly VsProjectAnalyzer _analyzer;

        private string _content, _ppContent;
        private ReadOnlyCollection<IParameter> _parameters;
        private int _listParamIndex, _dictParamIndex;

        private int _initialParameterIndex;
        private string _initialParameterName;
        private IParameter _currentParameter;

        public PythonSignature(VsProjectAnalyzer analyzer, ITrackingSpan span, AP.Signature overload, int paramIndex, string lastKeywordArg = null) {
            _span = span;
            _overload = overload;
            _analyzer = analyzer;

            _listParamIndex = _dictParamIndex = int.MaxValue;

            if (string.IsNullOrEmpty(lastKeywordArg)) {
                _initialParameterIndex = paramIndex;
            } else {
                _initialParameterIndex = int.MaxValue;
                _initialParameterName = lastKeywordArg;
            }

            _documentation = overload.doc;
        }

        private void Initialize() {
            if (_content != null) {
                Debug.Assert(_ppContent != null && _parameters != null);
                return;
            }

            Debug.Assert(_content == null && _ppContent == null && _parameters == null);

            var content = new StringBuilder(_overload.name);
            var ppContent = new StringBuilder(_overload.name);
            var parameters = new IParameter[_overload.parameters.Length];
            content.Append('(');
            ppContent.AppendLine("(");
            int start = content.Length, ppStart = ppContent.Length;
            for (int i = 0; i < _overload.parameters.Length; i++) {
                ppContent.Append("    ");
                ppStart = ppContent.Length;

                var param = _overload.parameters[i];
                if (param.optional) {
                    content.Append('[');
                    ppContent.Append('[');
                }
                if (i > 0) {
                    content.Append(", ");
                    start = content.Length;
                }

                var name = param.name ?? "";
                var isDict = name.StartsWithOrdinal("**");
                var isList = !isDict && name.StartsWithOrdinal("*");

                content.Append(name);
                ppContent.Append(name);

                if (!string.IsNullOrEmpty(param.type) && param.type != "object") {
                    content.Append(": ");
                    content.Append(param.type);
                    ppContent.Append(": ");
                    ppContent.Append(param.type);
                }

                if (!String.IsNullOrWhiteSpace(param.defaultValue)) {
                    content.Append(" = ");
                    content.Append(param.defaultValue);
                    ppContent.Append(" = ");
                    ppContent.Append(param.defaultValue);
                }

                var paramSpan = new Span(start, content.Length - start);
                var ppParamSpan = new Span(ppStart, ppContent.Length - ppStart);

                if (param.optional) {
                    content.Append(']');
                    ppContent.Append(']');
                }

                ppContent.AppendLine(",");

                parameters[i] = new PythonParameter(
                    this,
                    param.name,
                    param.doc,
                    paramSpan,
                    ppParamSpan
                );

                if (isDict && _dictParamIndex == int.MaxValue) {
                    _dictParamIndex = i;
                }

                if (isList && _listParamIndex == int.MaxValue) {
                    _listParamIndex = i;
                }
            }
            content.Append(')');
            ppContent.Append(')');

            _content = content.ToString();
            _ppContent = ppContent.ToString();

            _parameters = new ReadOnlyCollection<IParameter>(parameters);
            SelectBestParameter(_initialParameterIndex, _initialParameterName);
        }

        internal int SelectBestParameter(int index, string name) {
            Initialize();

            if (!string.IsNullOrEmpty(name)) {
                index = _parameters.IndexOf(p => p.Name == name);
                if (index < 0 || index > _dictParamIndex) {
                    index = _dictParamIndex;
                }
            } else if (index > _listParamIndex) {
                index = _listParamIndex;
            }

            if (index < 0 || index >= _parameters.Count) {
                SetCurrentParameter(null);
                return -1;
            }
            SetCurrentParameter(_parameters[index]);
            return index;
        }

        internal void ClearParameter() {
            SetCurrentParameter(null);
        }

        private void SetCurrentParameter(IParameter newValue) {
            if (newValue != _currentParameter) {
                var old = _currentParameter;
                _currentParameter = newValue;
                CurrentParameterChanged?.Invoke(this, new CurrentParameterChangedEventArgs(old, newValue));
            }
        }

        public ITrackingSpan ApplicableToSpan {
            get { return _span; }
        }

        public string Content {
            get {
                Initialize();
                return _content;
            }
        }

        public IParameter CurrentParameter {
            get {
                Initialize();
                return _currentParameter;
            }
        }

        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;

        public string Documentation => _documentation.LimitLines(15, stopAtFirstBlankLine: true);

        public ReadOnlyCollection<IParameter> Parameters {
            get {
                Initialize();
                return _parameters;
            }
        }

        public string PrettyPrintedContent {
            get {
                Initialize();
                return _ppContent;
            }
        }
    }
}
