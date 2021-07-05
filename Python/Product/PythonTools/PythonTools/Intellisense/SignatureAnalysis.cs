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

namespace Microsoft.PythonTools.Intellisense {
    class SignatureAnalysis {
        private readonly string _text;
        private readonly int _paramIndex;
        private readonly ISignature[] _signatures;
        private readonly string _lastKeywordArgument;

        internal SignatureAnalysis(string text, int paramIndex, IList<ISignature> signatures, string lastKeywordArgument = null) {
            _text = text;
            _paramIndex = paramIndex;
            _signatures = new ISignature[signatures.Count];
            signatures.CopyTo(_signatures, 0);
            _lastKeywordArgument = lastKeywordArgument;
            Array.Sort(_signatures, (x, y) => x.Parameters.Count - y.Parameters.Count);
        }

        public string Text {
            get {
                return _text;
            }
        }

        public int ParameterIndex {
            get {
                return _paramIndex;
            }
        }

        public string LastKeywordArgument {
            get {
                return _lastKeywordArgument;
            }
        }

        public IList<ISignature> Signatures {
            get {
                return _signatures;
            }
        }
    }
}
