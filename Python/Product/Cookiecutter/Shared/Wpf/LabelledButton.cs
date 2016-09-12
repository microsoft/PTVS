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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Windows;
using System.Windows.Controls;

namespace Microsoft.VisualStudioTools.Wpf {
    sealed class LabelledButton : Button {
        public string Title {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(LabelledButton), new PropertyMetadata());

        public string HelpText {
            get { return (string)GetValue(HelpTextProperty); }
            set { SetValue(HelpTextProperty, value); }
        }

        public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register("HelpText", typeof(string), typeof(LabelledButton), new PropertyMetadata(HelpText_PropertyChanged));

        private static void HelpText_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            d.SetValue(HasHelpTextPropertyKey, !string.IsNullOrWhiteSpace(e.NewValue as string));
        }


        public bool HasHelpText {
            get { return (bool)GetValue(HasHelpTextProperty); }
            private set { SetValue(HasHelpTextPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey HasHelpTextPropertyKey = DependencyProperty.RegisterReadOnly("HasHelpText", typeof(bool), typeof(LabelledButton), new PropertyMetadata(false));
        public static readonly DependencyProperty HasHelpTextProperty = HasHelpTextPropertyKey.DependencyProperty;

    }
}
