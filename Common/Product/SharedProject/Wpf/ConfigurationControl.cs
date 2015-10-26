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

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Microsoft.VisualStudioTools.Wpf {
    sealed class ConfigurationTextBoxWithHelp : Control {
        public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register("Watermark", typeof(string), typeof(ConfigurationTextBoxWithHelp), new PropertyMetadata());
        public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register("HelpText", typeof(string), typeof(ConfigurationTextBoxWithHelp), new PropertyMetadata());
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(ConfigurationTextBoxWithHelp), new PropertyMetadata());
        public static readonly DependencyProperty BrowseButtonStyleProperty = DependencyProperty.Register("BrowseButtonStyle", typeof(Style), typeof(ConfigurationTextBoxWithHelp), new PropertyMetadata());
        public static readonly DependencyProperty BrowseCommandParameterProperty = DependencyProperty.Register("BrowseCommandParameter", typeof(object), typeof(ConfigurationTextBoxWithHelp), new PropertyMetadata());

        public string Watermark {
            get { return (string)GetValue(WatermarkProperty); }
            set { SetValue(WatermarkProperty, value); }
        }

        public string HelpText {
            get { return (string)GetValue(HelpTextProperty); }
            set { SetValue(HelpTextProperty, value); }
        }

        public string Text {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public Style BrowseButtonStyle {
            get { return (Style)GetValue(BrowseButtonStyleProperty); }
            set { SetValue(BrowseButtonStyleProperty, value); }
        }

        public object BrowseCommandParameter {
            get { return (object)GetValue(BrowseCommandParameterProperty); }
            set { SetValue(BrowseCommandParameterProperty, value); }
        }
    }

    sealed class ConfigurationComboBoxWithHelp : Control {
        public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register("Watermark", typeof(string), typeof(ConfigurationComboBoxWithHelp), new PropertyMetadata());
        public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register("HelpText", typeof(string), typeof(ConfigurationComboBoxWithHelp), new PropertyMetadata());
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(string), typeof(ConfigurationComboBoxWithHelp), new PropertyMetadata());
        public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register("Values", typeof(IList<string>), typeof(ConfigurationComboBoxWithHelp), new PropertyMetadata());

        public string Watermark {
            get { return (string)GetValue(WatermarkProperty); }
            set { SetValue(WatermarkProperty, value); }
        }

        public string HelpText {
            get { return (string)GetValue(HelpTextProperty); }
            set { SetValue(HelpTextProperty, value); }
        }

        public string Value {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        
        public IList<string> Values {
            get { return (IList<string>)GetValue(ValuesProperty); }
            set { SetValue(ValuesProperty, value); }
        }
    }
}