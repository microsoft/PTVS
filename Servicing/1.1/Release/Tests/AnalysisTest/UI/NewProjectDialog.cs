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
using System.Windows.Automation;

namespace AnalysisTest.UI {
    /// <summary>
    /// Wrapps VS's File->New Project dialog.
    /// </summary>
    class NewProjectDialog  : AutomationWrapper {
        private TreeView _installedTemplates;
        private Table _projectTypesTable;

        public NewProjectDialog(AutomationElement element)
            : base(element) {
        }

        /// <summary>
        /// Clicks the OK button on the dialog.
        /// </summary>
        public void ClickOK() {
            ClickButtonByAutomationId("btn_OK");
        }

        /// <summary>
        /// Gets the installed templates tree view which enables access to all of the project types.
        /// </summary>
        public TreeView InstalledTemplates {
            get {
                if (_installedTemplates == null) {
                    var templates = Element.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(
                            AutomationElement.AutomationIdProperty,
                            "Installed Templates"
                        )
                    );

                    // all the templates have the same name (Installed, Recent, etc...)
                    // so we need to find the one that actually has our templates.
                    foreach (AutomationElement template in templates) {
                        var temp = new TreeView(template);
                        var item = temp.FindItem("Visual C#");
                        if (item != null) {
                            _installedTemplates = temp;
                            break;
                        }
                    }
                }
                return _installedTemplates;
            }
        }

        /// <summary>
        /// Gets the project types table which enables selecting an individual project type.
        /// </summary>
        public Table ProjectTypes {
            get {
                if (_projectTypesTable == null) {
                    var extensions = Element.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(
                            AutomationElement.AutomationIdProperty,
                            "lvw_Extensions"
                        )
                    );

                    if (extensions.Count != 1) {
                        throw new Exception("multiple controls match");
                    }
                    _projectTypesTable = new Table(extensions[0]);

                }
                return _projectTypesTable;
            }
        }

        public string ProjectName {
            get {
                var patterns = GetProjectNameBox().GetSupportedPatterns();
                var filename = (ValuePattern)GetProjectNameBox().GetCurrentPattern(ValuePattern.Pattern);
                return filename.Current.Value;
            }
            set {
                var patterns = GetProjectNameBox().GetSupportedPatterns();
                var filename = (ValuePattern)GetProjectNameBox().GetCurrentPattern(ValuePattern.Pattern);
                filename.SetValue(value);
            }
        }

        private AutomationElement GetProjectNameBox() {
            return Element.FindFirst(TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "txt_Name"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
                )
            );
        }
    }
}
