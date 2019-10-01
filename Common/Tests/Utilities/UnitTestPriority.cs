namespace TestUtilities
{
    public static class UnitTestPriority
    {
        public const int CORE_UNIT_TEST = 0;            // Run on every PR request going into master
        public const int SUPPLEMENTARY_UNIT_TEST = 1;   // Run on every commit into master

        public const int P2_UNIT_TEST = 20;
        public const int P3_UNIT_TEST = 30;

        public const int P0_FAILING_UNIT_TEST = 11;
        public const int P2_FAILING_UNIT_TEST = 21;
        public const int P3_FAILING_UNIT_TEST = 31;


    }
}