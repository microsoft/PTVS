using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace AnalysisTest.DotNetAnalysis {
    public class GenericType<T> where T : IEnumerable {
        public T ReturnsGenericParam() {
            return default(T);
        }
    }
}
