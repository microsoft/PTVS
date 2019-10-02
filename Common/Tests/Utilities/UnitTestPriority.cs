namespace TestUtilities {
    public static class UnitTestPriority {
        public const int P0 = 0;    // Run on every PR request going into master
        public const int P1 = 1;    // Run on every commit into master

        public const int P2_UNIT_TEST = 20;
        public const int P3_UNIT_TEST = 30;

        public const int P1_FAILING = 11;   
        public const int P2_FAILING = 21;
        public const int P3_FAILING = 31;

    }
}