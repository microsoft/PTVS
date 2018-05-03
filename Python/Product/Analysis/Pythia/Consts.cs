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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Analysis.Pythia {
    static class Consts {
        public const string ExtensionVersion = "1.1.0";
        public const string ModelVersion = "1.1.0";

        public const string NullSequence = "N";
        public const string SequenceDelimiter = "~";
        public const string UnicodeStar = "\u2605 ";

        public const int MaxRecommendation = 5;
        public const int PrecedingSequenceLength = 2;
        public const int MaxSearchLength = 20;
    }
}
