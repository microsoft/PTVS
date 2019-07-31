
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System;

namespace Microsoft.PythonTools.TestAdapter.Services {
    internal class DiscovererFactory {

        public static IPythonTestDiscoverer GetDiscoverer(PythonProjectSettings settings) {
            switch (settings.TestFramwork) {
                case TestFrameworkType.Pytest:
                    return new TestDiscovererPytest(settings);
                case TestFrameworkType.UnitTest:
                    return new TestDiscovererUnitTest(settings);
                case TestFrameworkType.None:
                default:
                    throw new NotImplementedException($"CreateDiscoveryService TestFrameworkType:{settings.TestFramwork.ToString()} not supported");
            }
        }
    }
}
