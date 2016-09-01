using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Django.Analysis {
    class DjangoUrl : IComparable<DjangoUrl> {
        public readonly string FullUrl;

        public DjangoUrl() { }

        public DjangoUrl(string urlName) {
            FullUrl = urlName;
        }

        public int CompareTo(DjangoUrl other) {
            return FullUrl.CompareTo(other.FullUrl);
        }
    }
}
