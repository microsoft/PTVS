// Python Tools for Visual Studio
// Copyright(c) 2018 Intel Corporation.  All rights reserved.
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace Microsoft.PythonTools.VsCode
{
    public class CPUUtilRecord
    {
        // timeBin,Bin Start Time,Bin End Time,CPU Time:Self
        long _timeBin;
        double _binStartTime;
        double _binEndTime;
        double _cpuTimeSelf;
        public CPUUtilRecord(long bin, double start, double end, double cputime)
        {
            _timeBin = bin;
            _binStartTime = start;
            _binEndTime = end;
            _cpuTimeSelf = cputime;
        }
        public long Bin { get { return _timeBin; } }
        public double Start { get { return _binStartTime; } }
        public double End { get { return _binEndTime; } }
        public double CPUUtil { get { return _cpuTimeSelf; } }
    }

    public static class VTuneCPUUtilizationParser
    {
        public static IEnumerable<CPUUtilRecord> ParseCPURecords(this IEnumerable<string> s)
        {
            foreach (string _s in s)
            {
                var ss = _s.Split(',');
                if (ss.Length != 4) { break; }
                yield return new CPUUtilRecord(Int64.Parse(ss[0]), Single.Parse(ss[1]) * 1000, Single.Parse(ss[2]) * 1000, Single.Parse(ss[3]));
            }
        }
    }
}