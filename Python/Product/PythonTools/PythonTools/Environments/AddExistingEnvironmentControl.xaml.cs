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

namespace Microsoft.PythonTools.Environments {
    public partial class AddExistingEnvironmentControl : UserControl {
        public static readonly ICommand UnselectInterpreter = new RoutedCommand();

        public AddExistingEnvironmentControl() {
            InitializeComponent();
        }

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            Microsoft.VisualStudioTools.Wpf.Commands.CanExecute(null, sender, e);
        }

        private void Browse_Executed(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.VisualStudioTools.Wpf.Commands.Executed(null, sender, e);
        }
    }

    class AddExistingEnvironmentTemplateSelector : DataTemplateSelector {
        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            var element = container as FrameworkElement;
            if (item is InterpreterView iv) {
                var parent = GetAncestorOfType(container, typeof(ComboBox), typeof(ComboBoxItem));
                string templateName;
                if (parent is ComboBoxItem) {
                    templateName = "InterpreterNameAndPrefixPathItemTemplate";
                } else {
                    templateName = "InterpreterSelectedItemTemplate";
                }
                return element.FindResource(templateName) as DataTemplate;
            }
            return base.SelectTemplate(item, container);
        }

        private static DependencyObject GetAncestorOfType(DependencyObject obj, params Type[] desiredTypes) {
            while (obj != null) {
                obj = VisualTreeHelper.GetParent(obj);
                if (obj != null && desiredTypes.Any(t => t.IsAssignableFrom(obj.GetType()))) {
                    return obj;
                }
            }
            return null;
        }
    }
}
