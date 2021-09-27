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

namespace TestUtilities.UI
{
    public class ExceptionHelperDialog : AutomationWrapper
    {
        public ExceptionHelperDialog(AutomationElement element)
            : base(element)
        {
        }

        public string Title
        {
            get
            {
                // this is just the 1st child pane, and it's name is the same as the text it has.
                var exceptionNamePane = Element.FindFirst(
                    TreeScope.Children,
                    new PropertyCondition(
                        AutomationElement.ControlTypeProperty,
                        ControlType.Pane
                    )
                );

                return exceptionNamePane.Current.Name;
            }
        }

        public string Description
        {
            get
            {
                var desc = FindByName("Exception Description:");
                return (((TextPattern)desc.GetCurrentPattern(TextPattern.Pattern)).DocumentRange.GetText(-1).ToString());
            }

        }

        public void Cancel()
        {
            ClickButtonByName("Cancel Button");
        }
    }
}
