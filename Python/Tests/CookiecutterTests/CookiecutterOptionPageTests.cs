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
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
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

        [TestMethod]
        public void UnifiedSettingsResourceTokensResolveFromPackageResources() {
            string manifest = ReadRegistrationManifest();
            MatchCollection tokens = Regex.Matches(
                manifest,
                @"@(?<id>\d+);\{(?<provider>[0-9A-Fa-f-]+)\}");
            var resources = new ResourceManager(
                "Microsoft.VSPackage",
                typeof(CookiecutterPackage).Assembly);

            Assert.AreEqual(8, tokens.Count, "Every localized manifest value must be covered.");
            foreach (Match token in tokens) {
                Assert.AreEqual(
                    PackageGuids.guidCookiecutterPkgString,
                    token.Groups["provider"].Value,
                    true,
                    CultureInfo.InvariantCulture);

                string value = resources.GetString(token.Groups["id"].Value, CultureInfo.InvariantCulture);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(value) || value.StartsWith("@", StringComparison.Ordinal),
                    $"Resource token {token.Value} did not resolve to text.");
            }
        }

        [TestMethod]
        public void UnifiedSettingsRegistrationSuppressesLegacyPlaceholder() {
            var expectedPageId = new Guid("BDB4E0B1-4869-4A6F-AD55-5230B768261D");
            ProvideOptionPageAttribute[] optionPages = typeof(CookiecutterPackage)
                .GetCustomAttributes<ProvideOptionPageAttribute>()
                .Where(attribute => attribute.PageType == typeof(CookiecutterOptionPage))
                .ToArray();

            Assert.AreEqual(1, optionPages.Length, "Expected one Cookiecutter > General option-page registration.");
            Assert.AreEqual(expectedPageId, optionPages[0].PageType.GUID);
            Assert.IsTrue(optionPages[0].IsInUnifiedSettings);

            string manifest = ReadRegistrationManifest();
            Assert.AreEqual(1, Regex.Matches(manifest, @"""cookiecutter\.general""\s*:").Count);
            Match legacyPageId = Regex.Match(
                manifest,
                @"""legacyOptionPageId""\s*:\s*""(?<id>[0-9A-Fa-f-]+)""");
            Assert.IsTrue(legacyPageId.Success);
            Assert.AreEqual(
                expectedPageId.ToString("D"),
                legacyPageId.Groups["id"].Value,
                true,
                CultureInfo.InvariantCulture);

            ProvideOptionPageAttribute[] placeholders = optionPages
                .Where(attribute => !attribute.IsInUnifiedSettings && attribute.ShouldShowUnifiedSettingsPlaceholder)
                .ToArray();
            Assert.AreEqual(0, placeholders.Length, "Onboarded option pages must be excluded before placeholder creation.");
        }

        private static string ReadRegistrationManifest() {
            string manifestPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "UnifiedSettings",
                "Cookiecutter.registration.json");
            return File.ReadAllText(manifestPath);
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
