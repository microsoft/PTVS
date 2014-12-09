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
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonFormattingOptionsPage : PythonDialogPage {
        private PythonFormattingOptionsControl _window;
        private readonly OptionCategory[] _categories;
        private const string _formattingCat = "Formatting";

        internal PythonFormattingOptionsPage()
            : this(new OptionCategory(
                    "Class Declarations",
                    OptionCategory.GetOptions(CodeFormattingCategory.Classes)
                )) {
        }

        internal PythonFormattingOptionsPage(params OptionCategory[] categories)
            : base(_formattingCat) {
            _categories = categories;
        }

        protected override void OnActivate(System.ComponentModel.CancelEventArgs e) {
            base.OnActivate(e);
            _window.OnActivated();
        }

        protected override void OnDeactivate(System.ComponentModel.CancelEventArgs e) {
            _window.OnDeactivated();
        }

        // replace the default UI of the dialog page w/ our own UI.
        protected override System.Windows.Forms.IWin32Window Window {
            get {
                EnsureWindow();
                return _window;
            }
        }

        private void EnsureWindow() {
            if (_window == null) {
                _window = new PythonFormattingOptionsControl(Site, _categories);
            }
        }

        public override void ResetSettings() {
            EnsureWindow();

            foreach (var value in _categories) {
                foreach (var option in value.Options) {
                    _window.SetSetting(option.Key, option.DefaultValue);
                }
            }
            base.ResetSettings();
        }

        public override void LoadSettingsFromStorage() {
            EnsureWindow();

            foreach (var value in _categories) {
                foreach (var option in value.Options) {
                    _window.SetSetting(option.Key, PyService.GetFormattingOption(option.Key));
                }
            }
            base.LoadSettingsFromStorage();
        }

        public override void SaveSettingsToStorage() {
            EnsureWindow();

            foreach (var value in _categories) {
                foreach (var option in value.Options) {
                    PyService.SetFormattingOption(option.Key, _window.GetSetting(option.Key));
                }
            }
            base.SaveSettingsToStorage();
        }
    }
}