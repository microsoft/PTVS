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

using System;
using System.Threading;
using System.Windows.Automation;

namespace TestUtilities.UI
{
    public class NavigateToDialog : AutomationWrapper, IDisposable
    {
        public NavigateToDialog(IntPtr hwnd)
            : base(AutomationElement.FromHandle(hwnd))
        {
        }

        public NavigateToDialog(AutomationElement element)
            : base(element)
        {
        }

        public void Dispose()
        {
            Close();
        }

        public void GoToSelection()
        {
#if DEV12_OR_LATER
            ClickButtonByAutomationId("PART_SearchButton");
#else
            ClickButtonByAutomationId("okButton");
#endif
        }

        public void Close()
        {
#if DEV12_OR_LATER
            try {
                GetSearchBox().SetFocus();
            } catch (InvalidOperationException ex) {
                Trace.TraceError("Failed to set focus on search box");
                Trace.TraceError(ex.ToString());
            }
            Keyboard.PressAndRelease(System.Windows.Input.Key.Escape);
#else
            ClickButtonByAutomationId("cancelButton");
#endif
        }

        public string SearchTerm
        {
            get
            {
                var term = (ValuePattern)GetSearchBox().GetCurrentPattern(ValuePattern.Pattern);
                return term.Current.Value;
            }
            set
            {
                var term = (ValuePattern)GetSearchBox().GetCurrentPattern(ValuePattern.Pattern);
                term.SetValue(string.Empty);
                GetSearchBox().SetFocus();
                Keyboard.Type(value);
            }
        }

        internal AutomationElement GetSearchBox()
        {
#if DEV12_OR_LATER
            return Element.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.AutomationIdProperty, "PART_SearchBox"),
                new PropertyCondition(AutomationElement.ClassNameProperty, "TextBox")
            ));
#else
            return Element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "searchTerms")
            ).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ClassNameProperty, "Edit")
            );
#endif
        }

#if DEV12_OR_LATER
        private AutomationElement GetResultsList() {
            return Element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "PART_ResultList")
            );
        }

        internal int WaitForNumberOfResults(int results) {
            for (int retries = 10; retries > 0; --retries) {
                var list = GetResultsList();
                if (list != null) {
                    var count = list.FindAll(TreeScope.Children, Condition.TrueCondition).Count;
                    if (count >= results) {
                        return count;
                    }
                }
                Thread.Sleep(1000);
            }

            return 0;
        }
#else
        private GridPattern GetResultsList()
        {
            return (GridPattern)Element.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "results")
            ).GetCurrentPattern(GridPattern.Pattern);
        }

        internal int WaitForNumberOfResults(int results)
        {
            var list = GetResultsList();

            for (int count = 10; count > 0 && list.Current.RowCount < results; --count)
            {
                Thread.Sleep(1000);
            }
            return list.Current.RowCount;
        }
#endif
    }
}
