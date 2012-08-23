
namespace Microsoft.TC.RemoteTest
{
    public class RemoteTest
    {
#if DEV11
        public const string IRemoteTestGuid = "33EE7772-E6BF-4C03-9ADC-6651698E06B5";
        public const string IRemoteTestProviderGuid = "746E04A7-E1FF-4559-AC1C-C98EDD5F0B94";
        public const string RemoteTestComponentGuid = "C39EE233-077E-45CD-897E-1A1871EDB330";
        public const string DefaultRemoteTestProviderGuid = "6E55C52D-011D-4FAA-A085-4ADFAE9588A3";
#else
        public const string IRemoteTestGuid = "45C00F61-5394-4288-8AA5-66DBCF95FF20";
        public const string IRemoteTestProviderGuid = "F579C434-4AB2-4180-A503-481D169AD3E7";
        public const string RemoteTestComponentGuid = "613726D3-47AC-41C8-9818-5963113E577A";
        public const string DefaultRemoteTestProviderGuid = "557C3B4F-1E9C-40B8-8E4B-1A3A1FEFEBB1";
#endif
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
