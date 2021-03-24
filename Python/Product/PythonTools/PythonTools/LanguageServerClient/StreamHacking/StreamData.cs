using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.LanguageServerClient.StreamHacking {
    class StreamData {
        public byte[] bytes;
        public int offset;
        public int count;
    }
}
