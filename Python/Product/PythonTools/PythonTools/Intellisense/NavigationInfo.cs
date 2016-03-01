using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    class NavigationInfo {
        public readonly string Name;
        public readonly Span Span;
        public readonly NavigationInfo[] Children;
        public readonly NavigationKind Kind;

        public NavigationInfo(string name, NavigationKind kind, Span span, NavigationInfo[] children) {
            Name = name;
            Kind = kind;
            Span = span;
            Children = children;
        }
    }

    public enum NavigationKind {
        None,
        Class,
        Function,
        StaticMethod,
        ClassMethod,
        Property
    }
}
