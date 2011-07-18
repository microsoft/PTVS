
namespace Microsoft.TC.RemoteTest
{
    public class RemoteTest
    {
        public const string IRemoteTestGuid = "45C00F61-5394-4288-8AA5-66DBCF95FF20";
        public const string IRemoteTestProviderGuid = "F579C434-4AB2-4180-A503-481D169AD3E7";
        public const string RemoteTestComponentGuid = "613726D3-47AC-41C8-9818-5963113E577A";
        public const string DefaultRemoteTestProviderGuid = "557C3B4F-1E9C-40B8-8E4B-1A3A1FEFEBB1";

        private static object s_inProcessInstance;

        public static object InProcessInstance
        {
            get
            {                
                return s_inProcessInstance;
            }
        }

        public static void InitializeInProcess(object inProcessInstance)
        {
            s_inProcessInstance = inProcessInstance;
        }

    }
}
