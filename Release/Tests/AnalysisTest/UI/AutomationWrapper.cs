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
using System.Diagnostics;
using System.Windows.Automation;

namespace AnalysisTest.UI {
    class AutomationWrapper {
        private readonly AutomationElement _element;
        
        public AutomationWrapper(AutomationElement element) {
            Debug.Assert(element != null);
            _element = element;
        }

        /// <summary>
        /// Provides access to the underlying AutomationElement used for accessing the visual studio app.
        /// </summary>
        public AutomationElement Element {
            get {
                return _element;
            }
        }

        /// <summary>
        /// Clicks the child button with the specified automation ID.
        /// </summary>
        /// <param name="automationId"></param>
        public void ClickButtonByAutomationId(string automationId) {
            Invoke(FindByAutomationId(automationId));
        }

        
        /// <summary>
        /// Clicks the child button with the specified name.
        /// </summary>
        /// <param name="name"></param>
        public void ClickButtonByName(string name) {
            var button = FindByName(name);

            Invoke(button);
        }

        protected AutomationElement FindByName(string name) {
            return Element.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(
                    AutomationElement.NameProperty,
                    name
                )
            );
        }

        /// <summary>
        /// Finds the first descendent with the given automation ID.
        /// </summary>
        protected AutomationElement FindByAutomationId(string automationId) {
            return Element.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(
                    AutomationElement.AutomationIdProperty,
                    automationId
                )
            );
        }

        /// <summary>
        /// Finds the child button with the specified name.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        internal AutomationElement FindButton(string text) {
            return Element.FindFirst(
                TreeScope.Descendants,
                new AndCondition(
                    new PropertyCondition(
                        AutomationElement.NameProperty,
                        text
                    ),
                    new PropertyCondition(
                        AutomationElement.ClassNameProperty,
                        "Button"
                    )
                )
            );
        }
        
        /// <summary>
        /// Finds the first child element of a given control type.
        /// </summary>
        /// <param name="ctlType">The ControlType you wish to find</param>
        /// <returns></returns>
        internal AutomationElement FindFirstByControlType(ControlType ctlType) {
            return Element.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(
                    AutomationElement.ControlTypeProperty,
                    ctlType
                )
            );
        }

        /// <summary>
        /// Finds all the children with a given control type.
        /// </summary>
        /// <param name="ctlType">The ControlType you wish to find</param>
        /// <returns></returns>
        internal AutomationElementCollection FindAllByControlType(ControlType ctlType) {
            return Element.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(
                    AutomationElement.ControlTypeProperty,
                    ctlType
                )
            );
        }

        #region Pattern Helpers

        /// <summary>
        /// Invokes the specified invokable item.  The item must support the invoke pattern.
        /// </summary>
        internal static void Invoke(AutomationElement button) {
            var invokePattern = (InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern);

            invokePattern.Invoke();
        }

        /// <summary>
        /// Selects the selectable item.  The item must support the Selection item pattern.
        /// </summary>
        /// <param name="selectionItem"></param>
        internal static void Select(AutomationElement selectionItem) {
            var selectPattern = (SelectionItemPattern)selectionItem.GetCurrentPattern(SelectionItemPattern.Pattern);

            selectPattern.Select();
        }

        /// <summary>
        /// Expands the selected item.  The item must support the expand/collapse pattern.
        /// </summary>
        /// <param name="node"></param>
        internal static void EnsureExpanded(AutomationElement node) {
            ExpandCollapsePattern pat = (ExpandCollapsePattern)node.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            if (pat.Current.ExpandCollapseState == ExpandCollapseState.Collapsed) {
                pat.Expand();
            }
        }


        /// <summary>
        /// Gets the specified value from this element.  The element must support the value pattern.
        /// </summary>
        /// <returns></returns>
        public string GetValue() {
            return ((ValuePattern)Element.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
        }

        /// <summary>
        /// Sets the specified value from this element.  The element must support the value pattern.
        /// </summary>
        /// <returns></returns>
        public void SetValue(string value) {
            ((ValuePattern)Element.GetCurrentPattern(ValuePattern.Pattern)).SetValue(value);
        }

        #endregion

        public static void DumpElement(AutomationElement element) {
            Debug.WriteLine("Name    ClassName      ControlType");
            DumpElement(element, 0);
        }

        private static void DumpElement(AutomationElement element, int depth) {
            Debug.WriteLine(String.Format("{0} {1} {2} {3}", 
                new string(' ', depth * 4), 
                element.Current.Name, 
                element.Current.ControlType.ProgrammaticName, 
                element.Current.ClassName));

            var children = element.FindAll(TreeScope.Children, Condition.TrueCondition);
            foreach (AutomationElement child in children) {
                DumpElement(child, depth + 1);
            }
        }
    }
} 
