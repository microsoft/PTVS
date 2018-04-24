using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace Microsoft.PythonTools.Analysis.Pythia
{
    internal static class PythiaUtil
    {
        public static string AssemblyDirectory
        {
            get
            {
                return Path.GetDirectoryName(typeof(PythiaUtil).Assembly.Location);
            }
        }

        public static Stream ReadModel(string filePath)
        {
            filePath = Path.Combine(AssemblyDirectory, filePath);
            return new GZipStream(new FileStream(filePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress);
        }
    }
}
