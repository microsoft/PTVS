using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools
{
    class ProvideImportTemplatesAttribute : RegistrationAttribute {
        private readonly string _guid, _projectType, _templateDir;
        private readonly string _winexeProject, _consoleProject, _classProject;

        public ProvideImportTemplatesAttribute(
            string projectType, string guid, string templateDir,
            string winexeProject, string consoleProject, string classProject)
        {
            _projectType = projectType;
            _guid = Guid.Parse(guid).ToString("B");
            _templateDir = templateDir;
            _winexeProject = winexeProject;
            _consoleProject = consoleProject;
            _classProject = classProject;
        }


        public override void Register(RegistrationContext context) {
            var importKey = context.CreateKey("Projects\\" + _guid + "\\ImportTemplates");
            importKey.SetValue("ProjectType", _projectType);
            importKey.SetValue("WizardPageObjectAssembly", "Microsoft.VisualStudio.ImportProjectFolderWizard, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            importKey.SetValue("WizardPageObjectClass", "Microsoft.VsWizards.ImportProjectFolderWizard.Managed.PageManager");
            importKey.SetValue("ImportProjectsDir", _templateDir);
            importKey.SetValue("WindowsAppProjectFile", _winexeProject);
            importKey.SetValue("ConsoleAppProjectFile", _consoleProject);
            importKey.SetValue("ClassLibProjectFile", _classProject);
        }

        public override void Unregister(RegistrationContext context) {
        }
    }
}
