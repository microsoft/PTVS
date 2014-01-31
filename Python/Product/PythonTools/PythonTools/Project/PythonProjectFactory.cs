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
using System.Xml;
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
            new Guid("{E614C764-6D9E-4607-9337-B7073809A0BD}")  // Bottle Web Project marker
        };

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

        protected override __VSPPROJECTUPGRADEVIAFACTORYREPAIRFLAGS UpgradeProjectCheck(
            ProjectRootElement projectXml,
            ProjectRootElement userProjectXml,
            Action<__VSUL_ERRORLEVEL, string> log,
            ref Guid projectFactory,
            ref __VSPPROJECTUPGRADEVIAFACTORYFLAGS backupSupport
        ) {
            Version version;

            if (!Version.TryParse(projectXml.ToolsVersion, out version) ||
                version < new Version(4, 0)) {
                return __VSPPROJECTUPGRADEVIAFACTORYREPAIRFLAGS.VSPUVF_PROJECT_SAFEREPAIR;
            }

            return __VSPPROJECTUPGRADEVIAFACTORYREPAIRFLAGS.VSPUVF_PROJECT_NOREPAIR;
        }

        protected override void UpgradeProject(
            ref ProjectRootElement projectXml,
            ref ProjectRootElement userProjectXml,
            Action<__VSUL_ERRORLEVEL, string> log
        ) {
            projectXml.ToolsVersion = "4.0";
            log(__VSUL_ERRORLEVEL.VSUL_INFORMATIONAL, SR.GetString(SR.UpgradedToolsVersion));
        }
    }
}
