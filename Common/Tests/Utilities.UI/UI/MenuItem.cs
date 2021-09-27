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
    class MenuItem : AutomationWrapper
    {
        public MenuItem(AutomationElement element)
            : base(element)
        {
        }

        public string Value
        {
            get
            {
                return this.Element.Current.Name.ToString();
            }
        }

        public bool ToggleStatus
        {
            get
            {
                var pat = (TogglePattern)Element.GetCurrentPattern(TogglePattern.Pattern);
                if (pat.Current.ToggleState == ToggleState.On)
                    return true;
                return false;
            }
        }

        public void Check()
        {
            var pat = (TogglePattern)Element.GetCurrentPattern(TogglePattern.Pattern);
            if (pat.Current.ToggleState == ToggleState.Off)
            {
                try
                {
                    pat.Toggle();
                }
                catch (InvalidOperationException)
                {
                    return;
                }
            }
        }

        public void Uncheck()
        {
            var pat = (TogglePattern)Element.GetCurrentPattern(TogglePattern.Pattern);
            if (pat.Current.ToggleState == ToggleState.On)
            {
                try
                {
                    pat.Toggle();
                }
                catch (InvalidOperationException)
                {
                    return;
                }
            }
        }
    }
}
