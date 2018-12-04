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

using System.Globalization;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    using AP = AnalysisProtocol;

    // TODO: This class should transform the request and pass to MethodExtractor
    // MethodExtractor should move to Analysis and remove all direct depnedencies
    // on AnalysisProtocol. For now, we are keeping it here to save effort.

    class OutOfProcMethodExtractor {
        private readonly MethodExtractor _extractor;

        public OutOfProcMethodExtractor(PythonAst ast, string code) {
            _extractor = new MethodExtractor(ast, code);
        }

        public AP.ExtractMethodResponse ExtractMethod(AP.ExtractMethodRequest input, int version) {
            return _extractor.ExtractMethod(input, version);
        }
    }
}
