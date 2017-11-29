using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestExternalProfilerDriver
{
#if false
    [TestClass]
    class TestReadVTune
    {
        static readonly string VTuneOutputSample = @"
timeBin  Bin Start Time Bin End Time  CPU Time:Self
-------  --------------  ------------  -------------
1                 0.563         1.125        42.127s
2                 1.125         1.688        99.994s
3                 1.688         2.251        99.986s
4                 2.251         2.813       100.000s
5                 2.813         3.376       100.000s
6                 3.376         3.939       100.000s
7                 3.939         4.501       100.000s
8                 4.501         5.064       100.000s
9                 5.064         5.626       100.000s
10                5.626         6.189       100.000s
11                6.189         6.752       100.000s
12                6.752         7.314        99.964s
13                7.314         7.877        99.984s
14                7.877         8.440       260.834s

Process: OMP Master Thread #0 (TID: 1304)
timeBin  Bin Start Time Bin End Time  CPU Time:Self
-------  --------------  ------------  -------------
1                 0.563         1.125        42.127s
2                 1.125         1.688        99.994s
3                 1.688         2.251        99.986s
4                 2.251         2.813       100.000s
5                 2.813         3.376       100.000s
6                 3.376         3.939       100.000s
7                 3.939         4.501       100.000s
8                 4.501         5.064       100.000s
9                 5.064         5.626       100.000s
10                5.626         6.189       100.000s
11                6.189         6.752       100.000s
12                6.752         7.314        99.964s
13                7.314         7.877        99.984s
14                7.877         8.440        93.344s

Process: OMP Worker Thread #1 (TID: 1468)
timeBin  Bin Start Time Bin End Time  CPU Time:Self
-------  --------------  ------------  -------------
14                7.877         8.440        57.424s

Process: OMP Worker Thread #2 (TID: 5364)
timeBin  Bin Start Time Bin End Time  CPU Time:Self
-------  --------------  ------------  -------------
14                7.877         8.440        55.146s

Process: OMP Worker Thread #3 (TID: 8728)
timeBin  Bin Start Time Bin End Time  CPU Time:Self
-------  --------------  ------------  -------------
14                7.877         8.440        54.920s
";
        [TestMethod]
        public void TestReadVTuneFromString()
        {
        }
    }
#endif
}
