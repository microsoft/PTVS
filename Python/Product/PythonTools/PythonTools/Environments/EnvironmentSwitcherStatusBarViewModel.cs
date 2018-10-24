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

using System;
using Microsoft.PythonTools.Common.Infrastructure;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Environments {
    sealed class EnvironmentSwitcherStatusBarViewModel : BindableBase, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly EnvironmentSwitcherManager _envSwitchMgr;
        private string _statusText;
        private string _toolTipText;
        private bool _isVisible;

        public EnvironmentSwitcherStatusBarViewModel(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _envSwitchMgr = serviceProvider.GetPythonToolsService().EnvironmentSwitcherManager;
            _envSwitchMgr.EnvironmentsChanged += OnEnvironmentsChanged;
            UpdateEnvironments();
        }

        public string StatusText {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ToolTipText {
            get => _toolTipText;
            set => SetProperty(ref _toolTipText, value);
        }

        public bool IsVisible {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        private void OnEnvironmentsChanged(object sender, EventArgs e) {
            UpdateEnvironments();
        }

        private void UpdateEnvironments() {
            IsVisible = _envSwitchMgr.IsInPythonMode;
            StatusText = BuildStatusText();
            ToolTipText = BuildToolTipText();
        }

        private string BuildStatusText() {
            if (_envSwitchMgr.CurrentFactory != null) {
                return _envSwitchMgr.CurrentFactory.Configuration.Description;
            } else {
                return Strings.EnvironmentSwitcherNoCurrentEnvironment;
            }
        }

        private string BuildToolTipText() {
            var keyBinding = _envSwitchMgr.GetSwitcherCommandKeyBinding();
            return string.IsNullOrEmpty(keyBinding)
                ? Strings.EnvironmentSwitcherTooltipWithoutKeyBinding
                : Strings.EnvironmentSwitcherTooltipWithKeyBinding.FormatUI(keyBinding);
        }

        public void Dispose() {
            _envSwitchMgr.EnvironmentsChanged -= OnEnvironmentsChanged;
        }
    }
}
