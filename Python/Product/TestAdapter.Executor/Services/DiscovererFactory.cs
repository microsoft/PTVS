
using Microsoft.PythonTools.TestAdapter.Config;
using Microsoft.PythonTools.TestAdapter.Pytest;
using Microsoft.PythonTools.TestAdapter.UnitTest;
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
