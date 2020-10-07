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

using System;

namespace Microsoft.PythonTools.Options {
    public sealed class PythonAdvancedEditorOptions {
        private const string Category = "Advanced";
        private const string CompleteFunctionParensSetting = "CompleteFunctionParens";
        private const string AutoImportCompletionsSetting = "AutoImportCompletions";

        private readonly PythonToolsService _service;

        internal PythonAdvancedEditorOptions(PythonToolsService service) {
            _service = service;
        }

        public void Load() {
            var changed = false;
            
            var completeFunctionParens = _service.LoadBool(CompleteFunctionParensSetting, Category) ?? false;
            if(CompleteFunctionParens != completeFunctionParens) {
                CompleteFunctionParens = completeFunctionParens;
                changed = true;
            }

            var autoImportCompletions = _service.LoadBool(AutoImportCompletionsSetting, Category) ?? true;
            if (AutoImportCompletions != autoImportCompletions) {
                AutoImportCompletions = autoImportCompletions;
                changed = true;
            }

            if (changed) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Save() {
            var changed = _service.SaveBool(CompleteFunctionParensSetting, Category, CompleteFunctionParens);
            changed |= _service.SaveBool(AutoImportCompletionsSetting, Category, AutoImportCompletions);
            if (changed) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Reset() {
            CompleteFunctionParens = false;
            AutoImportCompletions = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;

        public bool CompleteFunctionParens { get; set; }
        public bool AutoImportCompletions { get; set; }
    }
}