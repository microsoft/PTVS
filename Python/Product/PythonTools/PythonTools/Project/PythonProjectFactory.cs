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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

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

        public PythonProjectFactory(IServiceProvider/*!*/ package)
            : base(package) {
        }

        internal override ProjectNode/*!*/ CreateProject() {
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

        protected override ProjectUpgradeState UpgradeProjectCheck(
            ProjectRootElement projectXml,
            ProjectRootElement userProjectXml,
            Action<__VSUL_ERRORLEVEL, string> log,
            ref Guid projectFactory,
            ref __VSPPROJECTUPGRADEVIAFACTORYFLAGS backupSupport
        ) {
            Version version;

#if DEV12_OR_LATER
            // Web projects are incompatible with WDExpress/Shell
            ProjectPropertyElement projectType;
            if (!IsWebProjectSupported &&
                (projectType = projectXml.Properties.FirstOrDefault(p => p.Name == "ProjectTypeGuids")) != null) {
                var webProjectGuid = new Guid(PythonConstants.WebProjectFactoryGuid);
                if (projectType.Value
                    .Split(';')
                    .Select(s => {
                        Guid g;
                        return Guid.TryParse(s, out g) ? g : Guid.Empty;
                    })
                    .Contains(webProjectGuid)
                ) {
                    log(__VSUL_ERRORLEVEL.VSUL_ERROR, SR.GetString(SR.ProjectRequiresVWDExpress));
                    return ProjectUpgradeState.Incompatible;
                }
            }
#endif

            var imports = new HashSet<string>(projectXml.Imports.Select(p => p.Project), StringComparer.OrdinalIgnoreCase);
            // Importing a targets file from 2.1 Beta
            if (imports.Contains(Ptvs21BetaBottleTargets) || imports.Contains(Ptvs21BetaFlaskTargets)) {
                return ProjectUpgradeState.SafeRepair;
            }

            // Only importing the Common targets and/or props.
            if (imports.Contains(CommonProps) || imports.Contains(CommonTargets) && imports.Count == 1) {
                return ProjectUpgradeState.OneWayUpgrade;
            }

            // ToolsVersion less than 4.0 (or unspecified) is not supported, so
            // set it to 4.0.
            if (!Version.TryParse(projectXml.ToolsVersion, out version) ||
                version < new Version(4, 0)) {
                return ProjectUpgradeState.SafeRepair;
            }

#if !DEV12_OR_LATER
            // ToolsVersion later than 4.0 cannot be loaded in VS 2010 or 2012.
            if (userProjectXml != null) {
                if (!Version.TryParse(userProjectXml.ToolsVersion, out version) ||
                    version > new Version(4, 0)) {
                    return ProjectUpgradeState.SafeRepair;
                }
            }
#endif

            return ProjectUpgradeState.NotNeeded;
        }

        protected override void UpgradeProject(
            ref ProjectRootElement projectXml,
            ref ProjectRootElement userProjectXml,
            Action<__VSUL_ERRORLEVEL, string> log
        ) {
            Version version;

            // ToolsVersion less than 4.0 (or unspecified) is not supported, so
            // set it to 4.0.
            if (!Version.TryParse(projectXml.ToolsVersion, out version) ||
                version < new Version(4, 0)) {
                projectXml.ToolsVersion = "4.0";
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedToolsVersion));
            }

            // Importing a targets file from 2.1 Beta
            var bottleImports = projectXml.Imports.Where(p => p.Project.Equals(Ptvs21BetaBottleTargets, StringComparison.OrdinalIgnoreCase)).ToList();
            var flaskImports = projectXml.Imports.Where(p => p.Project.Equals(Ptvs21BetaFlaskTargets, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var import in bottleImports.Concat(flaskImports)) {
                import.Project = WebTargets;
            }

            if (bottleImports.Any()) {
                var globals = projectXml.PropertyGroups.FirstOrDefault() ?? projectXml.AddPropertyGroup();
                AddOrSetProperty(globals, "PythonDebugWebServerCommandArguments", "--debug $(CommandLineArguments)");
                AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app()");
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedBottleImports));
            }
            if (flaskImports.Any()) {
                var globals = projectXml.PropertyGroups.FirstOrDefault() ?? projectXml.AddPropertyGroup();
                AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app");
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedFlaskImports));
            }

            var commonPropsImports = projectXml.Imports.Where(p => p.Project.Equals(CommonProps, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var p in commonPropsImports) {
                projectXml.RemoveChild(p);
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedRemoveCommonProps));
            }
            
            if (projectXml.Imports.Count == 1 && projectXml.Imports.First().Project.Equals(CommonTargets, StringComparison.OrdinalIgnoreCase)) {
                projectXml.RemoveChild(projectXml.Imports.First());
                var group = projectXml.AddPropertyGroup();
                if (!projectXml.Properties.Any(p => p.Name == "VisualStudioVersion")) {
                    group.AddProperty("VisualStudioVersion", "10.0").Condition = "'$(VisualStudioVersion)' == ''";
                }
                group.AddProperty("PtvsTargetsFile", PtvsTargets);
                projectXml.AddImport("$(PtvsTargetsFile)").Condition = "Exists($(PtvsTargetsFile))";
                projectXml.AddImport(CommonTargets).Condition = "!Exists($(PtvsTargetsFile))";
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedRemoveCommonTargets));
            }

#if !DEV12_OR_LATER
            if (userProjectXml != null) {
                if (!Version.TryParse(userProjectXml.ToolsVersion, out version) ||
                    version > new Version(4, 0)) {
                    userProjectXml.ToolsVersion = "4.0";
                    log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedUserToolsVersion));
                }
            }
#endif
        }

        private const int ExpressSkuValue = 500;
        private const int ShellSkuValue = 1000;
        private const int ProSkuValue = 2000;
        private const int PremiumUltimateSkuValue = 3000;

        private const int VWDExpressSkuValue = 0x0040;
        private const int WDExpressSkuValue = 0x8000;
        private const int PremiumSubSkuValue = 0x0080;
        private const int UltimateSubSkuValue = 0x0188;

        private bool IsWebProjectSupported {
            get {
                var shell = (IVsShell)Site.GetService(typeof(SVsShell));
                if (shell == null) {
                    // Outside of VS, so we're probably only here for tests.
                    return true;
                }
                object obj;
                ErrorHandler.ThrowOnFailure(shell.GetProperty((int)__VSSPROPID2.VSSPROPID_SKUEdition, out obj));
                var sku = (obj as int?) ?? 0;
                if (sku == ShellSkuValue) {
                    return false;
                } else if (sku == ExpressSkuValue) {
                    ErrorHandler.ThrowOnFailure(shell.GetProperty((int)__VSSPROPID2.VSSPROPID_SubSKUEdition, out obj));
                    if ((obj as int?) == WDExpressSkuValue) {
                        return false;
                    }
                }

                return true;
            }
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
    }
}
