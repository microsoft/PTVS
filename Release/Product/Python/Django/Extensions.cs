using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Microsoft.PythonTools.Project.Automation;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.PythonTools.Django.TemplateParsing;

namespace Microsoft.PythonTools.Django {
    static class Extensions {
        internal static PythonProjectNode GetPythonProject(this EnvDTE.Project project) {
            return project.GetCommonProject() as PythonProjectNode;
        }

        internal static EnvDTE.Project GetProject(this IVsHierarchy hierarchy) {
            object project;

            ErrorHandler.ThrowOnFailure(
                hierarchy.GetProperty(
                    VSConstants.VSITEMID_ROOT,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out project
                )
            );

            return (project as EnvDTE.Project);
        }

        internal static CommonProjectNode GetCommonProject(this EnvDTE.Project project) {
            OAProject oaProj = project as OAProject;
            if (oaProj != null) {
                var common = oaProj.Project as CommonProjectNode;
                if (common != null) {
                    return common;
                }
            }
            return null;
        }

        internal static string GetFilePath(this ITextBuffer textBuffer) {
            ITextDocument textDocument;
            HtmlProjectionBuffer projBuffer;
            if (textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out textDocument)) {
                return textDocument.FilePath;
            } else if(textBuffer.Properties.TryGetProperty<HtmlProjectionBuffer>(typeof(HtmlProjectionBuffer), out projBuffer)) {
                return projBuffer.DiskBuffer.GetFilePath();
            } else {
                return null;
            }
        }

        internal static EnvDTE.Project GetProject(this ITextBuffer buffer) {
            var path = buffer.GetFilePath();
            if (path != null && DjangoPackage.Instance != null) {
                var item = DjangoPackage.Instance.DTE.Solution.FindProjectItem(path);
                if (item != null) {
                    return item.ContainingProject;
                }
            }
            return null;
        }

        internal static ITrackingSpan CreateTrackingSpan(this IIntellisenseSession session, ITextBuffer buffer) {
            var triggerPoint = session.GetTriggerPoint(buffer);

            var position = triggerPoint.GetPosition(buffer.CurrentSnapshot);

            return buffer.CurrentSnapshot.CreateTrackingSpan(position, 0, SpanTrackingMode.EdgeInclusive);
        }
    }
}
