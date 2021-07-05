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

namespace Microsoft.PythonTools.Options {
    public static class SuppressDialog {
        public const string Category = "SuppressDialog";

        public const string SwitchEvaluatorSetting = "SwitchEvaluator";
        public const string PublishToAzure30Setting = "PublishToAzure30";
    }

    sealed class SuppressDialogOptions {
        private readonly PythonToolsService _service;

        internal SuppressDialogOptions(PythonToolsService service) {
            _service = service;
            Load();
        }

        public void Load() {
            SwitchEvaluator = _service.LoadString(SuppressDialog.SwitchEvaluatorSetting, SuppressDialog.Category);
            PublishToAzure30 = _service.LoadString(SuppressDialog.PublishToAzure30Setting, SuppressDialog.Category);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Save() {
            _service.SaveString(SuppressDialog.SwitchEvaluatorSetting, SuppressDialog.Category, SwitchEvaluator);
            _service.SaveString(SuppressDialog.PublishToAzure30Setting, SuppressDialog.Category, PublishToAzure30);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Reset() {
            SwitchEvaluator = null;
            PublishToAzure30 = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;

        public string SwitchEvaluator { get; set; }
        public string PublishToAzure30 { get; set; }
    }
}
