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
            new Guid("{725071E1-96AE-4405-9303-1BA64EFF6EBD}")  // Worker Role Project marker
        };

        // These targets files existed in PTVS 2.1 Beta but were removed. We
        // want to replace them with some properties and Web.targets.
        // Some intermediate builds of PTVS have different paths that will not
        // be upgraded automatically.
        private const string Ptvs21BetaBottleTargets = @"$(VSToolsPath)\Python Tools\Microsoft.PythonTools.Bottle.targets";
        private const string Ptvs21BetaFlaskTargets = @"$(VSToolsPath)\Python Tools\Microsoft.PythonTools.Flask.targets";

        private const string WebTargets = @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\Python Tools\Microsoft.PythonTools.Web.targets";

        public PythonProjectFactory(PythonProjectPackage/*!*/ package)
            : base(package) {
        }

        internal override ProjectNode/*!*/ CreateProject() {
            PythonProjectNode project = new PythonProjectNode((PythonProjectPackage)Package);
            project.SetSite((IOleServiceProvider)((IServiceProvider)Package).GetService(typeof(IOleServiceProvider)));
            return project;
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

            // Importing a targets file from 2.1 Beta
            if (projectXml.Imports.Any(p =>
                p.Project.Equals(Ptvs21BetaBottleTargets, StringComparison.OrdinalIgnoreCase) ||
                p.Project.Equals(Ptvs21BetaFlaskTargets, StringComparison.OrdinalIgnoreCase))) {
                return ProjectUpgradeState.SafeRepair;
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
                AddOrSetProperty(globals, "PythonWebFrameworkPackage", "bottle");
                AddOrSetProperty(globals, "PythonWebFrameworkPackageDisplayName", "Bottle");
                AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app()");
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedBottleImports));
            }
            if (flaskImports.Any()) {
                var globals = projectXml.PropertyGroups.FirstOrDefault() ?? projectXml.AddPropertyGroup();
                AddOrSetProperty(globals, "PythonWebFrameworkPackage", "flask");
                AddOrSetProperty(globals, "PythonWebFrameworkPackageDisplayName", "Flask");
                AddOrSetProperty(globals, "PythonWsgiHandler", "{StartupModule}.wsgi_app");
                log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedFlaskImports));
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
