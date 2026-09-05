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

#if DEV18
using System.ComponentModel;
using System.Reflection;
using Microsoft.CookiecutterTools;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CookiecutterTests {
    [TestClass]
    public class CookiecutterOptionPageTests {
        [TestMethod]
        public void DefaultsAndUnifiedSettingsMonikersMatchRegistration() {
            AssertSetting(
                nameof(CookiecutterOptionPage.ShowHelp),
                CookiecutterSettings.DefaultShowHelp,
                CookiecutterSettings.ShowHelpMoniker);
            AssertSetting(
                nameof(CookiecutterOptionPage.FeedUrl),
                CookiecutterSettings.DefaultFeedUrl,
                CookiecutterSettings.FeedUrlMoniker);
            AssertSetting(
                nameof(CookiecutterOptionPage.CheckForTemplateUpdate),
                CookiecutterSettings.DefaultCheckForTemplateUpdate,
                CookiecutterSettings.CheckForTemplateUpdateMoniker);
        }

        private static void AssertSetting(string propertyName, object expectedDefault, string expectedMoniker) {
            PropertyInfo property = typeof(CookiecutterOptionPage).GetProperty(propertyName);
            var defaultValue = property.GetCustomAttribute<DefaultValueAttribute>();
            var moniker = property.GetCustomAttribute<UnifiedSettingsMonikerAttribute>();

            Assert.IsNotNull(defaultValue, $"Missing default value for {propertyName}.");
            Assert.AreEqual(expectedDefault, defaultValue.Value);
            Assert.IsNotNull(moniker, $"Missing Unified Settings moniker for {propertyName}.");
            Assert.AreEqual(expectedMoniker, moniker.Moniker);
        }
    }
}
#endif
