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
using Microsoft.VisualStudio.Text.Editor;

namespace TestUtilities.Mocks {
    public class MockTextOptions : IEditorOptions {
        private readonly Dictionary<string, object> _options = new Dictionary<string, object> {
            { DefaultOptions.ConvertTabsToSpacesOptionName, true },
            { DefaultOptions.IndentSizeOptionName, 4 },
            { DefaultOptions.NewLineCharacterOptionName, "\r\n" },
            { DefaultOptions.TabSizeOptionName, 4 }
        };

        public bool ClearOptionValue<T>(EditorOptionKey<T> key) {
            return _options.Remove(key.Name);
        }

        public bool ClearOptionValue(string optionId) {
            return _options.Remove(optionId);
        }

        public object GetOptionValue(string optionId) {
            object value;
            if (_options.TryGetValue(optionId, out value)) {
                return value;
            }

            throw new InvalidOperationException();
        }

        public T GetOptionValue<T>(EditorOptionKey<T> key) {
            return (T)GetOptionValue(key.Name);
        }

        public T GetOptionValue<T>(string optionId) {
            return (T)GetOptionValue(optionId);
        }

        public IEditorOptions GlobalOptions {
            get {
                IEditorOptions opt = this;
                while (opt.Parent != null) {
                    opt = opt.Parent;
                }
                return opt;
            }
        }

        public bool IsOptionDefined<T>(EditorOptionKey<T> key, bool localScopeOnly) {
            return IsOptionDefined(key.Name, localScopeOnly);
        }

        public bool IsOptionDefined(string optionId, bool localScopeOnly) {
            if (localScopeOnly || Parent == null) {
                return _options.ContainsKey(optionId);
            }

            return _options.ContainsKey(optionId) || Parent.IsOptionDefined(optionId, false);
        }

        public event EventHandler<EditorOptionChangedEventArgs> OptionChanged;

        public IEditorOptions Parent { get; set; }

        public void SetOptionValue<T>(EditorOptionKey<T> key, T value) {
            SetOptionValue(key.Name, value);
        }

        public void SetOptionValue(string optionId, object value) {
            _options[optionId] = value;
            var evt = OptionChanged;
            if (evt != null) {
                evt(this, new EditorOptionChangedEventArgs(optionId));
            }
        }

        public IEnumerable<EditorOptionDefinition> SupportedOptions {
            get { throw new NotImplementedException(); }
        }
    }
}
