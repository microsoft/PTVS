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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Utilities.UnifiedSettings;
using Newtonsoft.Json.Linq;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class PythonCondaUnifiedSettingsProviderTests {
        private const string PythonToolsPackageId = "6dbd7c1e-1f1b-496d-ac7c-c55dae66c783";
        private const string CondaOptionsPageId = "e73e8e2b-74dc-3834-b015-e56cb1fdadba";
        private const string CondaCategoryMoniker = "pythonTools.conda";

        private static readonly string[] SupportedCultures = {
            "en-US",
            "cs",
            "de",
            "es",
            "fr",
            "it",
            "ja",
            "ko",
            "pl",
            "pt-BR",
            "ru",
            "tr",
            "zh-Hans",
            "zh-Hant"
        };

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void UnifiedSettingsManifestResourcesResolveForAllSupportedCultures() {
            var packageAssembly = Assembly.LoadFrom(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Microsoft.PythonTools.dll")
            );
            var manifestPath = Path.Combine(
                Path.GetDirectoryName(packageAssembly.Location),
                "UnifiedSettings",
                "PythonTools.registration.json"
            );
            Assert.IsTrue(File.Exists(manifestPath), $"Unified Settings manifest not found at '{manifestPath}'.");

            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var resourceTokens = manifest
                .Descendants()
                .OfType<JValue>()
                .Where(value => value.Type == JTokenType.String)
                .Select(value => (string)value)
                .Where(value => value.StartsWith("@", StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(6, resourceTokens.Length, "Unexpected number of localizable resource tokens.");

            var tokenPattern = new Regex(
                @"^@(?<resourceId>\d+);\{(?<packageId>[0-9a-fA-F-]{36})\}$",
                RegexOptions.CultureInvariant
            );
            var resourceManager = new ResourceManager("Microsoft.VSPackage", packageAssembly);
            foreach (var cultureName in SupportedCultures) {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                foreach (var token in resourceTokens) {
                    var match = tokenPattern.Match(token);
                    Assert.IsTrue(match.Success, $"Invalid resource token '{token}'.");
                    Assert.AreEqual(
                        PythonToolsPackageId,
                        match.Groups["packageId"].Value,
                        true,
                        CultureInfo.InvariantCulture,
                        $"Resource token '{token}' does not target the Python Tools package."
                    );

                    var resolved = resourceManager.GetString(match.Groups["resourceId"].Value, culture);
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(resolved) || string.Equals(resolved, token, StringComparison.Ordinal),
                        $"Resource token '{token}' did not resolve for culture '{cultureName}'."
                    );
                }
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void UnifiedSettingsCondaCategoryReplacesLegacyOptionsPlaceholder() {
            var outputDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var manifestPath = Path.Combine(outputDirectory, "UnifiedSettings", "PythonTools.registration.json");
            var pkgdefPath = Path.Combine(outputDirectory, "Microsoft.PythonTools.pkgdef");
            Assert.IsTrue(File.Exists(manifestPath), $"Unified Settings manifest not found at '{manifestPath}'.");
            Assert.IsTrue(File.Exists(pkgdefPath), $"Python Tools pkgdef not found at '{pkgdefPath}'.");

            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var condaCategories = manifest["categories"]
                .Children<JProperty>()
                .Where(category => string.Equals(category.Name, CondaCategoryMoniker, StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(1, condaCategories.Length, "The Conda category must be registered exactly once.");
            Assert.AreEqual(
                CondaOptionsPageId,
                (string)condaCategories[0].Value["legacyOptionPageId"],
                true,
                CultureInfo.InvariantCulture
            );

            var pkgdef = File.ReadAllText(pkgdefPath);
            var condaPageSections = Regex.Matches(
                pkgdef,
                @"(?ms)^\[\$RootKey\$\\ToolsOptionsPages\\Python Tools\\Conda\]\s*(?<values>.*?)(?=^\[|\z)"
            );
            Assert.AreEqual(1, condaPageSections.Count, "The Conda Tools Options page must be registered exactly once.");

            var values = condaPageSections[0].Groups["values"].Value;
            Assert.IsTrue(
                Regex.IsMatch(values, "\"Page\"=\"\\{" + CondaOptionsPageId + "\\}\"", RegexOptions.IgnoreCase),
                "The Conda registration does not use the expected legacy page GUID."
            );
            var isInUnifiedSettingsMatch = Regex.Match(
                values,
                "^\"IsInUnifiedSettings\"=dword:(?<value>[0-9a-fA-F]{8})\r?$",
                RegexOptions.Multiline
            );
            Assert.IsTrue(
                isInUnifiedSettingsMatch.Success,
                "The Conda page must be marked as migrated to Unified Settings."
            );
            var isInUnifiedSettings = Convert.ToUInt32(isInUnifiedSettingsMatch.Groups["value"].Value, 16) != 0;
            Assert.IsTrue(isInUnifiedSettings, "The Conda page must be marked as migrated to Unified Settings.");

            var shouldShowPlaceholderMatch = Regex.Match(
                values,
                "^\"ShouldShowUnifiedSettingsPlaceholder\"=dword:(?<value>[0-9a-fA-F]{8})\r?$",
                RegexOptions.Multiline
            );
            Assert.IsTrue(
                shouldShowPlaceholderMatch.Success,
                "The Conda page must explicitly suppress the legacy placeholder."
            );
            var shouldShowUnifiedSettingsPlaceholder =
                Convert.ToUInt32(shouldShowPlaceholderMatch.Groups["value"].Value, 16) != 0;
            Assert.IsFalse(
                shouldShowUnifiedSettingsPlaceholder,
                "The Conda page must explicitly suppress the legacy placeholder."
            );
            Assert.IsTrue(
                Regex.IsMatch(
                    values,
                    "^\"UnifiedSettingsCategoryMoniker\"=\"" + Regex.Escape(CondaCategoryMoniker) + "\"\r?$",
                    RegexOptions.Multiline
                ),
                "The Conda page does not target the registered Unified Settings category."
            );

            // ToolsOptionsHierarchyMerger excludes an onboarded page before considering placeholder creation.
            var placeholderCount = !isInUnifiedSettings && shouldShowUnifiedSettingsPlaceholder ? 1 : 0;
            Assert.AreEqual(1, condaCategories.Length, "Expected one real Unified Settings Conda category.");
            Assert.AreEqual(0, placeholderCount, "The legacy Conda placeholder must be excluded.");
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public async Task UnifiedSettingsWriteUpdatesLegacyStoreAndCachedOptions() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var pythonService = serviceProvider.GetPythonToolsService();
            var optionsService = (IPythonToolsOptionsService)serviceProvider.GetService(typeof(IPythonToolsOptionsService));
            var cachedOptions = pythonService.CondaOptions;
            var provider = new PythonCondaUnifiedSettingsProvider(pythonService);
            var changed = 0;
            provider.SettingValuesChanged += (sender, args) => changed++;

            var result = await provider.SetValueAsync(
                PythonCondaUnifiedSettingsProvider.CustomCondaExecutablePathMoniker,
                @"C:\Miniconda\Scripts\conda.exe",
                CancellationToken.None
            );

            Assert.IsInstanceOfType(result, typeof(ExternalSettingOperationResult.Success));
            Assert.AreSame(cachedOptions, pythonService.CondaOptions);
            Assert.AreEqual(@"C:\Miniconda\Scripts\conda.exe", cachedOptions.CustomCondaExecutablePath);
            Assert.AreEqual(
                @"C:\Miniconda\Scripts\conda.exe",
                optionsService.LoadString("CustomCondaExecutablePath", "Conda")
            );
            Assert.AreEqual(1, changed);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public async Task UnifiedSettingsReadAndWriteDoNotCreateCachedOptions() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var pythonService = serviceProvider.GetPythonToolsService();
            var provider = new PythonCondaUnifiedSettingsProvider(pythonService);

            var initialResult = await provider.GetValueAsync<string>(
                PythonCondaUnifiedSettingsProvider.CustomCondaExecutablePathMoniker,
                CancellationToken.None
            );
            var writeResult = await provider.SetValueAsync(
                PythonCondaUnifiedSettingsProvider.CustomCondaExecutablePathMoniker,
                @"C:\Miniconda\Scripts\conda.exe",
                CancellationToken.None
            );

            Assert.AreEqual(string.Empty, ((ExternalSettingOperationResult<string>.Success)initialResult).Value);
            Assert.IsInstanceOfType(writeResult, typeof(ExternalSettingOperationResult.Success));
            Assert.IsFalse(pythonService.AreCondaOptionsCreated);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public async Task LegacyWriteRefreshesUnifiedSettingsAndCachedOptions() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var pythonService = serviceProvider.GetPythonToolsService();
            var cachedOptions = pythonService.CondaOptions;
            var provider = new PythonCondaUnifiedSettingsProvider(pythonService);
            var changed = 0;
            provider.SettingValuesChanged += (sender, args) => changed++;

            cachedOptions.CustomCondaExecutablePath = @"D:\Anaconda\Scripts\conda.exe";
            cachedOptions.Save();

            var result = await provider.GetValueAsync<string>(
                PythonCondaUnifiedSettingsProvider.CustomCondaExecutablePathMoniker,
                CancellationToken.None
            );

            Assert.AreEqual(
                @"D:\Anaconda\Scripts\conda.exe",
                ((ExternalSettingOperationResult<string>.Success)result).Value
            );
            Assert.AreEqual(1, changed);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public async Task UnifiedSettingsReadRefreshesStaleCachedOptions() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var pythonService = serviceProvider.GetPythonToolsService();
            var optionsService = (IPythonToolsOptionsService)serviceProvider.GetService(typeof(IPythonToolsOptionsService));
            var cachedOptions = pythonService.CondaOptions;
            var provider = new PythonCondaUnifiedSettingsProvider(pythonService);
            var changed = 0;
            provider.SettingValuesChanged += (sender, args) => changed++;

            optionsService.SaveString(
                "CustomCondaExecutablePath",
                "Conda",
                @"D:\Anaconda\Scripts\conda.exe"
            );

            var result = await provider.GetValueAsync<string>(
                PythonCondaUnifiedSettingsProvider.CustomCondaExecutablePathMoniker,
                CancellationToken.None
            );

            Assert.AreEqual(
                @"D:\Anaconda\Scripts\conda.exe",
                ((ExternalSettingOperationResult<string>.Success)result).Value
            );
            Assert.AreEqual(@"D:\Anaconda\Scripts\conda.exe", cachedOptions.CustomCondaExecutablePath);
            Assert.AreEqual(0, changed);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public async Task LegacyResetDoesNotNotifyBeforePersistence() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var pythonService = serviceProvider.GetPythonToolsService();
            var cachedOptions = pythonService.CondaOptions;
            var provider = new PythonCondaUnifiedSettingsProvider(pythonService);
            var changed = 0;
            provider.SettingValuesChanged += (sender, args) => changed++;

            await provider.SetValueAsync(
                PythonCondaUnifiedSettingsProvider.CustomCondaExecutablePathMoniker,
                @"C:\Miniconda\Scripts\conda.exe",
                CancellationToken.None
            );
            changed = 0;

            cachedOptions.Reset();

            Assert.AreEqual(string.Empty, cachedOptions.CustomCondaExecutablePath);
            Assert.AreEqual(0, changed);

            cachedOptions.Save();

            Assert.AreEqual(1, changed);
            var result = await provider.GetValueAsync<string>(
                PythonCondaUnifiedSettingsProvider.CustomCondaExecutablePathMoniker,
                CancellationToken.None
            );
            Assert.AreEqual(string.Empty, ((ExternalSettingOperationResult<string>.Success)result).Value);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LegacyLoadStillNotifiesWhenValueIsUnchanged() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var cachedOptions = serviceProvider.GetPythonToolsService().CondaOptions;
            var changed = 0;
            cachedOptions.Changed += (sender, args) => changed++;

            cachedOptions.Load();

            Assert.AreEqual(1, changed);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void UnchangedLegacySaveDoesNotNotifyUnifiedSettings() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var pythonService = serviceProvider.GetPythonToolsService();
            var cachedOptions = pythonService.CondaOptions;
            cachedOptions.Save();
            var provider = new PythonCondaUnifiedSettingsProvider(pythonService);
            var legacyChanged = 0;
            var unifiedSettingsChanged = 0;
            cachedOptions.Changed += (sender, args) => legacyChanged++;
            provider.SettingValuesChanged += (sender, args) => unifiedSettingsChanged++;

            cachedOptions.Save();

            Assert.AreEqual(1, legacyChanged);
            Assert.AreEqual(0, unifiedSettingsChanged);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void LegacyLoadAfterUnsavedResetDoesNotNotifyUnifiedSettings() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var pythonService = serviceProvider.GetPythonToolsService();
            var cachedOptions = pythonService.CondaOptions;
            cachedOptions.CustomCondaExecutablePath = @"C:\Miniconda\Scripts\conda.exe";
            cachedOptions.Save();
            var provider = new PythonCondaUnifiedSettingsProvider(pythonService);
            var legacyChanged = 0;
            var unifiedSettingsChanged = 0;
            cachedOptions.Changed += (sender, args) => legacyChanged++;
            provider.SettingValuesChanged += (sender, args) => unifiedSettingsChanged++;

            cachedOptions.Reset();
            cachedOptions.Load();

            Assert.AreEqual(@"C:\Miniconda\Scripts\conda.exe", cachedOptions.CustomCondaExecutablePath);
            Assert.AreEqual(2, legacyChanged);
            Assert.AreEqual(0, unifiedSettingsChanged);
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public async Task UnknownSettingAndTypeAreRejected() {
            var serviceProvider = PythonToolsTestUtilities.CreateMockServiceProvider();
            var pythonService = serviceProvider.GetPythonToolsService();
            var provider = new PythonCondaUnifiedSettingsProvider(pythonService);

            var unknownSetting = await provider.GetValueAsync<string>(
                "unknown",
                CancellationToken.None
            );
            var wrongType = await provider.SetValueAsync(
                PythonCondaUnifiedSettingsProvider.CustomCondaExecutablePathMoniker,
                42,
                CancellationToken.None
            );

            Assert.IsInstanceOfType(unknownSetting, typeof(ExternalSettingOperationResult<string>.Failure));
            Assert.IsInstanceOfType(wrongType, typeof(ExternalSettingOperationResult.Failure));
        }
    }
}

#endif
