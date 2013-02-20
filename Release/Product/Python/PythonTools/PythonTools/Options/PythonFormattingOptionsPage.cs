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
        private Func<CodeFormattingOptions> _optionsFactory;
        private const string _formattingCat = "Formatting";
        private static readonly Dictionary<string, OptionInfo> _allOptions = new Dictionary<string, OptionInfo>();

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
                _window = new PythonFormattingOptionsControl(_categories);
            }
        }

        /// <summary>
        /// Sets the value for a formatting setting.  The name is one of the properties
        /// in CodeFormattingOptions.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        internal static void SetOption(string name, object value) {
            EnsureAllOptions();
            OptionInfo option;
            if (!_allOptions.TryGetValue(name, out option)) {
                throw new InvalidOperationException("Unknown option " + name);
            }

            SaveString(name, option.SerializeOptionValue(value), _formattingCat);
        }

        /// <summary>
        /// Gets the value for a formatting setting.  The name is one of the properties in
        /// CodeFormattingOptions.
        /// </summary>
        internal static object GetOption(string name) {
            EnsureAllOptions();
            OptionInfo option;
            if (!_allOptions.TryGetValue(name, out option)) {
                throw new InvalidOperationException("Unknown option " + name);
            }
            return option.DeserializeOptionValue(LoadString(name, _formattingCat));
        }

        private static void EnsureAllOptions() {
            if (_allOptions.Count == 0) {
                foreach (CodeFormattingCategory curCat in Enum.GetValues(typeof(CodeFormattingCategory))) {
                    if (curCat == CodeFormattingCategory.None) {
                        continue;
                    }

                    var cat = OptionCategory.GetOptions(curCat);
                    foreach (var optionInfo in cat) {
                        _allOptions[optionInfo.Key] = optionInfo;
                    }
                }
            }
        }

        /// <summary>
        /// Gets a new CodeFormattinOptions object configured to the users current settings.
        /// </summary>
        internal CodeFormattingOptions GetCodeFormattingOptions() {
            if (_optionsFactory == null) {
                // create a factory which can create CodeFormattingOptions without tons of reflection
                var initializers = new Dictionary<OptionInfo, Action<CodeFormattingOptions, object>>();
                foreach (CodeFormattingCategory curCat in Enum.GetValues(typeof(CodeFormattingCategory))) {
                    if (curCat == CodeFormattingCategory.None) {
                        continue;
                    }

                    var cat = OptionCategory.GetOptions(curCat);
                    foreach (var option in cat) {
                        var propInfo = typeof(CodeFormattingOptions).GetProperty(option.Key);

                        if (propInfo.PropertyType == typeof(bool)) {
                            initializers[option] = MakeFastSetter<bool>(propInfo);
                        } else if (propInfo.PropertyType == typeof(bool?)) {
                            initializers[option] = MakeFastSetter<bool?>(propInfo);
                        } else if (propInfo.PropertyType == typeof(int)) {
                            initializers[option] = MakeFastSetter<int>(propInfo);
                        } else {
                            throw new InvalidOperationException(String.Format("Unsupported formatting option type: {0}", propInfo.PropertyType.FullName));
                        }
                    }
                }

                _optionsFactory = CreateOptionsFactory(initializers);
            }           

            return _optionsFactory();
        }

        private Func<CodeFormattingOptions> CreateOptionsFactory(Dictionary<OptionInfo, Action<CodeFormattingOptions, object>> initializers) {
            return () => {
                var res = new CodeFormattingOptions();
                foreach (var keyValue in initializers) {
                    var option = keyValue.Key;
                    var fastSet = keyValue.Value;

                    fastSet(res, option.DeserializeOptionValue(LoadString(option.Key)));
                }
                return res;
            };
        }

        private static Action<CodeFormattingOptions, object> MakeFastSetter<T>(PropertyInfo propInfo) {
            var fastSet = (Action<CodeFormattingOptions, T>)propInfo.GetSetMethod().CreateDelegate(typeof(Action<CodeFormattingOptions, T>));
            return (options, value) => fastSet(options, (T)value);
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
                    _window.SetSetting(option.Key, option.DeserializeOptionValue(LoadString(option.Key)));
                }
            }
            base.LoadSettingsFromStorage();
        }

        public override void SaveSettingsToStorage() {
            EnsureWindow();

            foreach (var value in _categories) {
                foreach (var option in value.Options) {
                    SaveString(option.Key, option.SerializeOptionValue(_window.GetSetting(option.Key)));
                }
            }
            base.SaveSettingsToStorage();
        }
    }
}