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
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TemplateWizard;

namespace Microsoft.PythonTools.ImportWizard {
    public sealed class ImportSettings {
        public string SourceFilesPath { get; set; }
        public string StartupFile { get; set; }
        public string Filter { get; set; }
        public string[] SearchPaths { get; set; }
        public string InterpreterId { get; set; }
        public string InterpreterVersion { get; set; }
    }

    public sealed class Wizard : IWizard {
        public void BeforeOpeningFile(EnvDTE.ProjectItem projectItem) { }
        public void ProjectFinishedGenerating(EnvDTE.Project project) { }
        public void ProjectItemFinishedGenerating(EnvDTE.ProjectItem projectItem) { }
        public void RunFinished() { }

        public void SetReplacements(ImportSettings settings, Dictionary<string, string> replacementsDictionary) {
            var projectFilePath = replacementsDictionary["$destinationdirectory$"];

            var projectHome = CommonUtils.GetRelativeDirectoryPath(projectFilePath, settings.SourceFilesPath);
            var searchPaths = string.Join(";", settings.SearchPaths.Select(p => CommonUtils.GetRelativeDirectoryPath(settings.SourceFilesPath, p)));

            var directories = new HashSet<string>();
            var content = new StringBuilder();

            content.AppendLine("  <ItemGroup>");

            var files = Directory.EnumerateFiles(settings.SourceFilesPath, "*.py", SearchOption.AllDirectories);
            foreach (var pattern in settings.Filter.Split(';')) {
                try {
                    var theseFiles = Directory.EnumerateFiles(settings.SourceFilesPath, pattern.Trim(), SearchOption.AllDirectories);
                    files = files.Concat(theseFiles);
                } catch (ArgumentException) {
                    // Probably an invalid pattern.
                }
            }

            foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase)) {
                var relFile = CommonUtils.GetRelativeFilePath(settings.SourceFilesPath, file);
                var dir = Path.GetDirectoryName(relFile);
                if (!String.IsNullOrWhiteSpace(dir)) {
                    directories.Add(dir);
                }

                if (Path.GetExtension(file).Equals(".py", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(file).Equals(".pyw", StringComparison.OrdinalIgnoreCase)) {
                    content.AppendLine(string.Format("    <Compile Include=\"{0}\" />", relFile));
                } else {
                    content.AppendLine(string.Format("    <Content Include=\"{0}\" />", relFile));
                }
            }
            content.AppendLine("  </ItemGroup>");

            if (directories.Any()) {
                content.AppendLine("  <ItemGroup>");
                foreach (var dir in directories.OrderBy(key => key)) {
                    content.AppendLine(string.Format("    <Folder Include=\"{0}\" />", dir));
                }
                content.AppendLine("  </ItemGroup>");
            }

            replacementsDictionary["$projecthome$"] = projectHome;
            replacementsDictionary["$searchpaths$"] = searchPaths;
            replacementsDictionary["$content$"] = content.ToString();

            if (!string.IsNullOrEmpty(settings.InterpreterId)) {
                replacementsDictionary["$interpreter$"] = string.Format("    <InterpreterId>{0}</InterpreterId>{2}    <InterpreterVersion>{1}</InterpreterVersion>{2}",
                    settings.InterpreterId, settings.InterpreterVersion, Environment.NewLine);
            } else {
                replacementsDictionary["$interpreter$"] = Environment.NewLine;
            }

            if (!string.IsNullOrEmpty(settings.StartupFile)) {
                replacementsDictionary["$startupfile$"] = CommonUtils.GetRelativeFilePath(settings.SourceFilesPath, settings.StartupFile);
            } else {
                replacementsDictionary["$startupfile$"] = "";
            }
        }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
            try {
                var provider = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)automationObject);
                var settings = ImportWizardDialog.ShowImportDialog(provider);

                if (settings == null) {
                    throw new WizardBackoutException();
                }

                SetReplacements(settings, replacementsDictionary);
            } catch (WizardBackoutException) {
                try {
                    Directory.Delete(replacementsDictionary["$destinationdirectory$"]);
                    Directory.Delete(replacementsDictionary["$solutiondirectory$"]);
                } catch {
                    // If it fails (doesn't exist/contains files/read-only), let the directory stay.
                }
                throw;
            } catch (Exception ex) {
                MessageBox.Show(string.Format("Error occurred running wizard:\n\n{0}", ex));
                throw new WizardCancelledException("Internal error", ex);
            }
        }

        public bool ShouldAddProjectItem(string filePath) {
            return true;
        }
    }
}
