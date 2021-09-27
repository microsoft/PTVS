// Visual Studio Shared Project
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

namespace TestUtilities.Mocks
{
    public class MockTextOptions : IEditorOptions
    {
        private readonly Dictionary<string, object> _options = new Dictionary<string, object> {
            { DefaultOptions.ConvertTabsToSpacesOptionName, true },
            { DefaultOptions.IndentSizeOptionName, 4 },
            { DefaultOptions.NewLineCharacterOptionName, "\r\n" },
            { DefaultOptions.TabSizeOptionName, 4 }
        };

        public bool ClearOptionValue<T>(EditorOptionKey<T> key)
        {
            return _options.Remove(key.Name);
        }

        public bool ClearOptionValue(string optionId)
        {
            return _options.Remove(optionId);
        }

        public object GetOptionValue(string optionId)
        {
            object value;
            if (_options.TryGetValue(optionId, out value))
            {
                return value;
            }

            throw new InvalidOperationException();
        }

        public T GetOptionValue<T>(EditorOptionKey<T> key)
        {
            return (T)GetOptionValue(key.Name);
        }

        public T GetOptionValue<T>(string optionId)
        {
            return (T)GetOptionValue(optionId);
        }

        public IEditorOptions GlobalOptions
        {
            get
            {
                IEditorOptions opt = this;
                while (opt.Parent != null)
                {
                    opt = opt.Parent;
                }
                return opt;
            }
        }

        public bool IsOptionDefined<T>(EditorOptionKey<T> key, bool localScopeOnly)
        {
            return IsOptionDefined(key.Name, localScopeOnly);
        }

        public bool IsOptionDefined(string optionId, bool localScopeOnly)
        {
            if (localScopeOnly || Parent == null)
            {
                return _options.ContainsKey(optionId);
            }

            return _options.ContainsKey(optionId) || Parent.IsOptionDefined(optionId, false);
        }

        public event EventHandler<EditorOptionChangedEventArgs> OptionChanged;

        public IEditorOptions Parent { get; set; }

        public void SetOptionValue<T>(EditorOptionKey<T> key, T value)
        {
            SetOptionValue(key.Name, value);
        }

        public void SetOptionValue(string optionId, object value)
        {
            _options[optionId] = value;
            var evt = OptionChanged;
            if (evt != null)
            {
                evt(this, new EditorOptionChangedEventArgs(optionId));
            }
        }

        public IEnumerable<EditorOptionDefinition> SupportedOptions
        {
            get { throw new NotImplementedException(); }
        }
    }
}
