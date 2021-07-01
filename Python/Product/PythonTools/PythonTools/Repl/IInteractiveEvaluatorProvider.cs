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

namespace Microsoft.PythonTools.Repl
{
    interface IInteractiveEvaluatorProvider
    {
        IInteractiveEvaluator GetEvaluator(string evaluatorId);

        /// <summary>
        /// Returns a list of display name - evaluatorId pairs.
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> GetEvaluators();

        /// <summary>
        /// The result of <see cref="GetEvaluators"/> has changed.
        /// </summary>
        event EventHandler EvaluatorsChanged;
    }
}
