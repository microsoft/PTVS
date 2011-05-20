using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Interaction logic for ExtractMethodDialog.xaml
    /// </summary>
    internal partial class ExtractMethodDialog {
        private readonly ExtractedMethodCreator _previewer;
        private readonly List<ScopeStatement> _targetScopes = new List<ScopeStatement>();

        public ExtractMethodDialog(ExtractedMethodCreator previewer) {
            _previewer = previewer;

            InitializeComponent();
            ScopeStatement lastClass = null;
            for (int i = _previewer.Scopes.Length - 1; i >= 0; i--) {
                if (_previewer.Scopes[i] is ClassDefinition) {
                    lastClass = _previewer.Scopes[i];
                    break;
                }
            }

            foreach (var scope in _previewer.Scopes) {
                if (!(scope is ClassDefinition) || scope == lastClass) {
                    _targetScope.Items.Add(scope.Name);
                    _targetScopes.Add(scope);
                }
            }

            _targetScope.SelectedIndex = 0;
            UpdatePreview();

            _newName.Focus();
            _newName.SelectAll();
        }

        private void _targetScope_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var newScope = _targetScopes[_targetScope.SelectedIndex]; // the scope we're extracting the method to

            _closureVariables.Children.Clear();
            List<string> parameterOrClosure = new List<string>();
            HashSet<string> addedVars = new HashSet<string>();
            foreach (var variable in _previewer.Variables) {
                var variableScope = variable.Scope;
                var parentScope = newScope;

                // are these variables a child of the target scope so we can close over them?
                while (parentScope != null && parentScope != variableScope) {
                    parentScope = parentScope.Parent;
                }

                if (parentScope != null) {
                    // we can either close over or pass these in as parameters, add them to the list
                    parameterOrClosure.Add(variable.Name);
                    addedVars.Add(variable.Name);
                }
            }

            parameterOrClosure.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var param in parameterOrClosure) {
                var checkBox = new CheckBox() { Content = param };
                checkBox.Checked += CheckBoxChecked;
                checkBox.Unchecked += CheckBoxChecked;
                _closureVariables.Children.Add(checkBox);
            }

            UpdatePreview();
        }

        void CheckBoxChecked(object sender, RoutedEventArgs e) {
            UpdatePreview();
        }


        private void NewNameTextChanged(object sender, TextChangedEventArgs e) {
            UpdatePreview();
        }

        private void UpdatePreview() {
            if (_previewText != null) {
                var text = _previewer.GetExtractionResult(GetExtractInfo()).Method;
                var curScope = _targetScopes[_targetScope.SelectedIndex].Parent;
                while (curScope != null) {
                    text = _previewer.Indentation + text;
                    curScope = curScope.Parent;
                }
                _previewText.Text = text;
            }
        }

        public ExtractMethodRequest GetExtractInfo() {
            return new ExtractMethodRequest(_targetScopes[_targetScope.SelectedIndex], _newName.Text, Parameters.ToArray());
        }

        private IEnumerable<string> Parameters {
            get {
                if (_closureVariables != null) {
                    foreach (CheckBox param in _closureVariables.Children) {
                        if (param.IsChecked != null && !param.IsChecked.Value) {
                            yield return (string)param.Content;
                        }
                    }
                }
            }
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            Close();
        }

    }
}
