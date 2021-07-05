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

using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Creates Python Projects
    /// </summary>
    [Guid(PythonConstants.ProjectFactoryGuid)]
    class PythonProjectFactory : ProjectFactory {
        // We don't want to create projects with these GUIDs because they are
        // either incompatible or don't really exist (e.g. telemetry markers).
        private static readonly HashSet<Guid> IgnoredProjectTypeGuids = new HashSet<Guid> {
            new Guid("{789894C7-04A9-4A11-A6B5-3F4435165112}"), // Flask Web Project marker
            new Guid("{E614C764-6D9E-4607-9337-B7073809A0BD}"), // Bottle Web Project marker
            new Guid("{725071E1-96AE-4405-9303-1BA64EFF6EBD}"), // Worker Role Project marker
            new Guid("{A41C8EA1-112A-4A2D-9F91-29557995525F}"), // ML Classifier template marker
            new Guid("{8267E218-6B96-4D5D-A9DF-50CEDF58D05F}"), // ML Clustering template marker
            new Guid("{6C0EFAFA-1A04-41B6-A6D7-511B90951B5B}"), // ML Regression template marker
            // Reserved for future use
            new Guid("{C6BB79BC-0657-4BB5-8732-4FFE9EB5352D}"),
            new Guid("{C966CC89-2BC8-4036-85D1-478A085253AD}"),
            new Guid("{D848A2D7-0C4D-4A6A-9048-2B62DC103475}"),
            new Guid("{74DCBC5F-E288-431D-A7A0-B7CD4BE4B611}"),
            new Guid("{2BAC7739-571D-41CB-953C-7101995EBD9E}"),
            new Guid("{B452423D-5304-416F-975E-351476E8705C}"),
            new Guid("{587EF8DD-BE2D-4792-AE5F-8AE0A49AC1A5}")
        };

        internal const string UwpProjectGuid = @"{2b557614-1a2b-4903-b9df-ed20d7b63f3a}";

        // These targets files existed in PTVS 2.1 Beta but were removed. We
        // want to replace them with some properties and Web.targets.
        // Some intermediate builds of PTVS have different paths that will not
        // be upgraded automatically.
        private const string Ptvs21BetaBottleTargets = @"$(VSToolsPath)\Python Tools\Microsoft.PythonTools.Bottle.targets";
        private const string Ptvs21BetaFlaskTargets = @"$(VSToolsPath)\Python Tools\Microsoft.PythonTools.Flask.targets";

        // These targets files existed in early PTVS versions but are no longer
        // suitable and need to be replaced with our own targets file.
        internal const string CommonTargets = @"$(MSBuildToolsPath)\Microsoft.Common.targets";
        internal const string CommonProps = @"$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props";

        internal const string PtvsTargets = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.targets";
        internal const string WebTargets = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets";
        internal const string UwpTargets = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Uwp.targets";

#if DEV15
        internal const string ToolsVersion = "15.1";
#else
        internal const string ToolsVersion = "4.0";
#endif

        // These GUIDs were used for well-known interpreter IDs
        private static readonly Dictionary<Guid, string> InterpreterIdMap = new Dictionary<Guid, string> {
            { new Guid("{2AF0F10D-7135-4994-9156-5D01C9C11B7E}"), "Global|PythonCore|{0}-32" },
            { new Guid("{9A7A9026-48C1-4688-9D5D-E5699D47D074}"), "Global|PythonCore|{0}" },
            { new Guid("{80659AB7-4D53-4E0C-8588-A766116CBD46}"), "IronPython|{0}-32" },
            { new Guid("{FCC291AA-427C-498C-A4D7-4502D6449B8C}"), "IronPython|{0}-64" },
            { new Guid("{86767848-40B4-4007-8BCC-A3835EDF0E69}"), "PythonUwpIoT|{0}|$(MSBuildProjectFullPath)" },
        };

        public PythonProjectFactory(IServiceProvider/*!*/ package)
            : base(package) {
        }

        internal override ProjectNode/*!*/ CreateProject() {
            // Ensure our package is properly loaded
            var pyService = Site.GetPythonToolsService();

            return new PythonProjectNode(Site);
        }

        protected override string ProjectTypeGuids(string file) {
            var guids = base.ProjectTypeGuids(file);

            // Exclude GUIDs from IgnoredProjectTypeGuids so we don't try and
            // create projects from them.
            return string.Join(";", guids
                .Split(';')
                .Where(s => {
                    Guid g;
                    return Guid.TryParse(s, out g) && !IgnoredProjectTypeGuids.Contains(g);
                })
            );
        }

        private static bool IsGuidValue(ProjectPropertyElement e) {
            Guid g;
            return Guid.TryParse(e.Value, out g);
        }

        private static bool IsGuidValue(ProjectMetadataElement e) {
            Guid g;
            return Guid.TryParse(e.Value, out g);
        }

        private static bool IsGuidValue(ProjectItemElement e) {
            Guid g;
            foreach (var i in (e.Include?.Split('/', '\\')).MaybeEnumerate()) {
                if (Guid.TryParse(i?.Trim() ?? "", out g)) {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAssemblyReference(ProjectItemElement e) {
            try {
                new AssemblyName(e.Include);
            } catch (Exception) {
                return false;
            }
            return true;
        }

        private static bool IsMscorlibReference(ProjectItemElement e) {
            try {
                return (new AssemblyName(e.Include)).Name == "mscorlib";
            } catch (Exception) {
                return false;
            }
        }

        protected override ProjectUpgradeState UpgradeProjectCheck(
            ProjectRootElement projectXml,
            ProjectRootElement userProjectXml,
            Action<__VSUL_ERRORLEVEL, string> log,
            ref Guid projectFactory,
            ref __VSPPROJECTUPGRADEVIAFACTORYFLAGS backupSupport
        ) {
            Version version;

            // Referencing an interpreter by GUID
            if (projectXml.Properties.Where(p => p.Name == "InterpreterId").Any(IsGuidValue) ||
                projectXml.ItemGroups.SelectMany(g => g.Items)
                    .Where(i => i.ItemType == "InterpreterReference")
                    .Any(IsGuidValue) ||
                projectXml.ItemGroups.SelectMany(g => g.Items)
                    .Where(i => i.ItemType == "Interpreter")
                    .SelectMany(i => i.Metadata.Where(m => m.Name == "BaseInterpreter"))
                    .Any(IsGuidValue)
            ) {
                return ProjectUpgradeState.OneWayUpgrade;
            }

            var imports = new HashSet<string>(projectXml.Imports.Select(p => p.Project), StringComparer.OrdinalIgnoreCase);
            // Only importing the Common targets and/or props.
            if (imports.Contains(CommonProps) || imports.Contains(CommonTargets) && imports.Count == 1) {
                return ProjectUpgradeState.OneWayUpgrade;
            }

            // Includes imports from PTVS 2.2
            if (projectXml.Properties.Any(IsPtvsTargetsFileProperty)) {
                return ProjectUpgradeState.SafeRepair;
            }

            // Uses web or Django launcher and has no WebBrowserUrl property
            if (projectXml.Properties.Where(p => p.Name == "LaunchProvider")
                    .Any(p => p.Value == "Web launcher" || p.Value == "Django launcher") &&
                !projectXml.Properties.Any(p => p.Name == "WebBrowserUrl")) {
                return ProjectUpgradeState.SafeRepair;
            }

            // Importing a targets file from 2.1 Beta
            if (imports.Contains(Ptvs21BetaBottleTargets) || imports.Contains(Ptvs21BetaFlaskTargets)) {
                return ProjectUpgradeState.SafeRepair;
            }

            // ToolsVersion less than 4.0 (or unspecified) is not supported, so
            // set it to 4.0.
            if (!Version.TryParse(projectXml.ToolsVersion, out version) ||
                version < new Version(4, 0)) {
                return ProjectUpgradeState.SafeRepair;
            }

            // Referencing .NET assemblies but not mscorlib
            var references = projectXml.ItemGroups.SelectMany(g => g.Items)
                .Where(i => i.ItemType == ProjectFileConstants.Reference).ToArray();
            if (references.Any(IsAssemblyReference) && !references.Any(IsMscorlibReference)) {
                return ProjectUpgradeState.SafeRepair;
            }

            return ProjectUpgradeState.NotNeeded;
        }

        protected override void UpgradeProject(
            ref ProjectRootElement projectXml,
            ref ProjectRootElement userProjectXml,
            Action<__VSUL_ERRORLEVEL, string> log
        ) {
            Version version;

            // ToolsVersion less than 4.0 (or unspecified) is not supported, so
            // set it to the latest.
            if (!Version.TryParse(projectXml.ToolsVersion, out version) ||
                version < new Version(4, 0)) {
                projectXml.ToolsVersion = ToolsVersion;
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedToolsVersion);
            }

            // Referencing an interpreter by GUID
            ProcessInterpreterIdsFrom22(projectXml, log);

            // Importing a targets file from 2.2
            ProcessImportsFrom22(projectXml, log);

            // Importing a targets file from 2.1 Beta
            ProcessImportsFrom21b(projectXml, log);

            // Add missing WebBrowserUrl property
            ProcessMissingWebBrowserUrl(projectXml, log);

            // Referencing .NET assemblies but not mscorlib
            ProcessMissingMscorlibReference(projectXml, log);
        }

        private static bool IsPtvsTargetsFileProperty(ProjectPropertyElement p) {
            return p.Name == "PtvsTargetsFile";
        }

        private static void ProcessMissingWebBrowserUrl(ProjectRootElement projectXml, Action<__VSUL_ERRORLEVEL, string> log) {
            foreach (var g in projectXml.PropertyGroupsReversed) {
                var launcher = g.PropertiesReversed.FirstOrDefault(p => p.Name == "LaunchProvider");
                if (launcher == null) {
                    continue;
                }
                if (launcher.Value != "Web launcher" && launcher.Value != "Django launcher") {
                    return;
                }

                // <WebBrowserUrl>http://localhost</WebBrowserUrl>
                g.AddProperty("WebBrowserUrl", "http://localhost");
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedWebBrowserUrlProperty);
                return;
            }
        }

        private static void ProcessImportsFrom22(ProjectRootElement projectXml, Action<__VSUL_ERRORLEVEL, string> log) {
            bool anyUpdated = false;
            var propValue = PtvsTargets;

            foreach (var p in projectXml.Properties.Where(IsPtvsTargetsFileProperty).ToArray()) {
                propValue = p.Value;
                p.Parent.RemoveChild(p);
                anyUpdated = true;
            }

            // Replace:
            // <Import Condition="Exists($(PtvsTargetsFile))" Project="$(PtvsTargetsFile)" />
            // <Import Condition="!Exists($(PtvsTargetsFile))" Project="$(MSBuildToolsPath)\Microsoft.Common.targets" />
            //
            // With:
            // <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.targets" />
            foreach (var p in projectXml.Imports.Where(i => i.Condition.Contains("$(PtvsTargetsFile)") || i.Project.Equals("$(PtvsTargetsFile)")).ToArray()) {
                p.Parent.RemoveChild(p);
                anyUpdated = true;
            }

            string targets = PtvsTargets;
            if (ContainsProjectTypeGuid(projectXml, UwpProjectGuid)) {
                targets = UwpTargets;
            }

            if (!projectXml.Imports.Any(p => targets.Equals(p.Project, StringComparison.OrdinalIgnoreCase))) {
                projectXml.AddImport(targets);
                anyUpdated = true;
            }

            if (anyUpdated) {
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedImportsFor30);
            }
        }

        private static void ProcessImportsFrom21b(ProjectRootElement projectXml, Action<__VSUL_ERRORLEVEL, string> log) {
            var bottleImports = projectXml.Imports.Where(p => p.Project.Equals(Ptvs21BetaBottleTargets, StringComparison.OrdinalIgnoreCase)).ToList();
            var flaskImports = projectXml.Imports.Where(p => p.Project.Equals(Ptvs21BetaFlaskTargets, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var import in bottleImports.Concat(flaskImports)) {
                import.Project = WebTargets;
            }

            if (bottleImports.Any()) {
                var globals = projectXml.PropertyGroups.FirstOrDefault() ?? projectXml.AddPropertyGroup();
                AddOrSetProperty(globals, "PythonDebugWebServerCommandArguments", "--debug $(CommandLineArguments)");
                AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app()");
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedBottleImports);
            }
            if (flaskImports.Any()) {
                var globals = projectXml.PropertyGroups.FirstOrDefault() ?? projectXml.AddPropertyGroup();
                AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app");
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedFlaskImports);
            }

            var commonPropsImports = projectXml.Imports.Where(p => p.Project.Equals(CommonProps, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var p in commonPropsImports) {
                projectXml.RemoveChild(p);
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedRemoveCommonProps);
            }

            if (projectXml.Imports.Count == 1 && projectXml.Imports.First().Project.Equals(CommonTargets, StringComparison.OrdinalIgnoreCase)) {
                projectXml.RemoveChild(projectXml.Imports.First());
                projectXml.AddImport(PtvsTargets);
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedRemoveCommonTargets);
            }
        }

        private static void ProcessInterpreterIdsFrom22(ProjectRootElement projectXml, Action<__VSUL_ERRORLEVEL, string> log) {
            bool interpreterChanged = false, interpreterRemoved = false;
            var msbuildInterpreters = new Dictionary<Guid, string>();

            foreach (var i in projectXml.ItemGroups.SelectMany(g => g.Items).Where(i => i.ItemType == "Interpreter")) {
                var id = i.Metadata.LastOrDefault(m => m.Name == "Id");
                if (id != null) {
                    Guid guid;
                    if (Guid.TryParse(id.Value, out guid)) {
                        msbuildInterpreters[guid] = i.Include?.Trim('/', '\\');
                    }
                }

                var mdBase = i.Metadata.LastOrDefault(m => m.Name == "BaseInterpreter");
                if (mdBase != null) {
                    // BaseInterpreter value is now unused, so just remove it
                    mdBase.Parent.RemoveChild(mdBase);
                }
                var mdVer = i.Metadata.LastOrDefault(m => m.Name == "Version");
                if (mdVer == null) {
                    log(__VSUL_ERRORLEVEL.VSUL_ERROR, Strings.UpgradedInterpreterReferenceFailed);
                    continue;
                }
            }

            var interpreterId = projectXml.Properties.LastOrDefault(p => p.Name == "InterpreterId");
            var interpreterVersion = projectXml.Properties.LastOrDefault(p => p.Name == "InterpreterVersion");
            if (interpreterId != null && interpreterVersion != null) {
                var newId = MapInterpreterId(interpreterId.Value, interpreterVersion.Value, msbuildInterpreters);
                if (newId != null) {
                    interpreterId.Value = newId;
                    if (!ContainsProjectTypeGuid(projectXml, UwpProjectGuid)) {
                        interpreterVersion.Parent.RemoveChild(interpreterVersion);
                    }
                    interpreterChanged = true;
                } else {
                    interpreterId.Parent.RemoveChild(interpreterId);
                    interpreterVersion.Parent.RemoveChild(interpreterVersion);
                    interpreterRemoved = true;
                }
            }

            foreach (var i in projectXml.ItemGroups.SelectMany(g => g.Items).Where(i => i.ItemType == "InterpreterReference").ToList()) {
                var newId = MapInterpreterId(i.Include, null, null);
                if (newId != null) {
                    i.Include = newId;
                    interpreterChanged = true;
                } else {
                    i.Parent.RemoveChild(i);
                    interpreterRemoved = true;
                }
            }

            if (interpreterRemoved) {
                log(__VSUL_ERRORLEVEL.VSUL_WARNING, Strings.UpgradedInterpreterReferenceRemoved);
            } else if (interpreterChanged) {
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedInterpreterReference);
            }
        }

        private static void ProcessMissingMscorlibReference(ProjectRootElement projectXml, Action<__VSUL_ERRORLEVEL, string> log) {
            var references = projectXml.ItemGroups.SelectMany(g => g.Items)
                .Where(i => i.ItemType == ProjectFileConstants.Reference).ToArray();
            if (!references.Any(IsAssemblyReference) || references.Any(IsMscorlibReference)) {
                return;
            }

            var group = projectXml.ItemGroups.OrderByDescending(g => g.Items.Count(i => i.ItemType == ProjectFileConstants.Reference)).FirstOrDefault() ??
                projectXml.AddItemGroup();

            group.AddItem(ProjectFileConstants.Reference, "mscorlib", new Dictionary<string, string> {
                ["Name"] = "mscorlib",
                ["Private"] = "False"
            });

            log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, Strings.UpgradedMscorlibReference);
        }

        private static void AddOrSetProperty(ProjectPropertyGroupElement group, string name, string value) {
            bool anySet = false;
            foreach (var prop in group.Properties.Where(p => p.Name == name)) {
                prop.Value = value;
                anySet = true;
            }

            if (!anySet) {
                group.AddProperty(name, value);
            }
        }

        private static string MapInterpreterId(string idStr, string versionStr, IDictionary<Guid, string> msBuildInterpreters) {
            int splitter = idStr.IndexOfAny(new[] { '/', '\\' });
            if (splitter > 0) {
                versionStr = idStr.Substring(splitter + 1);
                idStr = idStr.Remove(splitter);
            }

            Guid id;
            Version version;
            if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out id)) {
                return null;
            }

            string fmt;
            if (InterpreterIdMap.TryGetValue(id, out fmt)) {
                if (string.IsNullOrEmpty(versionStr) || !Version.TryParse(versionStr, out version)) {
                    return null;
                }

                return fmt.FormatInvariant(version.ToString());
            }

            string msbuildId = null;
            if ((msBuildInterpreters?.TryGetValue(id, out msbuildId) ?? false) && !string.IsNullOrEmpty(msbuildId)) {
                return "MSBuild|{0}|$(MSBuildProjectFullPath)".FormatInvariant(msbuildId);
            }

            return null;
        }

        private static bool ContainsProjectTypeGuid(ProjectRootElement projectXml, string guid) {
            return projectXml.Properties.Where(p => p.Name == ProjectFileConstants.ProjectTypeGuids).Any(p => p.Value.Contains(guid));
        }
    }
}
