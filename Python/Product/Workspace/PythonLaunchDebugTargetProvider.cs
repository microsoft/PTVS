using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Debug;
using Microsoft.VisualStudio.Workspace.Extensions.Build;

namespace Microsoft.PythonTools.Workspace {
    [ExportLaunchConfigurationProvider(
        ProviderType,
        new[] { PythonConstants.FileExtension, PythonConstants.WindowsFileExtension },
        LaunchTypeName,
        JsonSchema
    )]
    class PythonLaunchDebugTargetProvider : ILaunchConfigurationProvider {
        public const string ProviderType = "CCA8088B-06BC-4AE7-8521-FC66628ABE13";
        public const string LaunchTypeName = "Python";
        public const string JsonSchema = @"";

        public bool IsDebugLaunchActionSupported(DebugLaunchActionContext debugLaunchActionContext) {
            return true;
        }

        public void CustomizeLaunchConfiguration(DebugLaunchActionContext debugLaunchActionContext, IPropertySettings launchSettings) {
            
        }
    }
}
