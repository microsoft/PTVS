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
    public class CheckBox : AutomationWrapper
    {
        public string Name { get; set; }

        public CheckBox(AutomationElement element)
            : base(element)
        {
            Name = (string)Element.GetCurrentPropertyValue(AutomationElement.NameProperty);
        }

        public void SetSelected()
        {
            Assert.IsTrue((bool)Element.GetCurrentPropertyValue(AutomationElement.IsTogglePatternAvailableProperty), "Element is not a check box");
            TogglePattern pattern = (TogglePattern)Element.GetCurrentPattern(TogglePattern.Pattern);

            if (pattern.Current.ToggleState != ToggleState.On) pattern.Toggle();
            if (pattern.Current.ToggleState != ToggleState.On) pattern.Toggle();

            Assert.AreEqual(pattern.Current.ToggleState, ToggleState.On, "Could not toggle " + Name + " to On.");
        }

        public void SetUnselected()
        {
            Assert.IsTrue((bool)Element.GetCurrentPropertyValue(AutomationElement.IsTogglePatternAvailableProperty), "Element is not a check box");
            TogglePattern pattern = (TogglePattern)Element.GetCurrentPattern(TogglePattern.Pattern);

            if (pattern.Current.ToggleState != ToggleState.Off) pattern.Toggle();
            if (pattern.Current.ToggleState != ToggleState.Off) pattern.Toggle();
            Assert.AreEqual(pattern.Current.ToggleState, ToggleState.Off, "Could not toggle " + Name + " to Off.");
        }

        public ToggleState ToggleState
        {
            get
            {
                TogglePattern pattern = (TogglePattern)Element.GetCurrentPattern(TogglePattern.Pattern);
                return pattern.Current.ToggleState;
            }

            set
            {
                if (value == ToggleState.On)
                {
                    SetSelected();
                }
                else if (value == ToggleState.Off)
                {
                    SetUnselected();
                }
            }
        }
    }
}
