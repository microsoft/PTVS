// Visual Studio Shared Project
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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace TestUtilities
{
    public static class TestExtensions
    {
        public static void SetStartupFile(this Project project, string name)
        {
            Assert.IsNotNull(project, "null project");
            Assert.IsNotNull(project.Properties, "null project properties " + project.Name + " " + project.GetType().FullName + " " + project.Kind);
            Assert.IsNotNull(project.Properties.Item("StartupFile"), "null startup file property" + project.Name + " " + project.GetType().FullName);
            project.Properties.Item("StartupFile").Value = name;
        }

        public static IEnumerable<int> FindIndexesOf(this string s, string substring)
        {
            int pos = 0;
            while (true)
            {
                pos = s.IndexOf(substring, pos);
                if (pos < 0)
                {
                    break;
                }
                yield return pos;
                pos++;
            }
        }

        public static int IndexOfEnd(this string s, string substring)
        {
            int i = s.IndexOf(substring);
            if (i < 0)
            {
                return i;
            }
            return i + substring.Length;
        }

        public static HashSet<T> ToSet<T>(this IEnumerable<T> enumeration)
        {
            return new HashSet<T>(enumeration);
        }

        public static HashSet<T> ToSet<T>(this IEnumerable<T> enumeration, IEqualityComparer<T> comparer)
        {
            return new HashSet<T>(enumeration, comparer);
        }

        public static bool ContainsExactly<T>(this HashSet<T> set, IEnumerable<T> values)
        {
            if (set.Count != values.Count())
            {
                return false;
            }
            foreach (var value in values)
            {
                if (!set.Contains(value))
                {
                    return false;
                }
            }
            foreach (var value in set)
            {
                if (!values.Contains(value, set.Comparer))
                {
                    return false;
                }
            }
            return true;
        }

        public static XName GetName(this XDocument doc, string localName)
        {
            return doc.Root.Name.Namespace.GetName(localName);
        }

        public static XElement Descendant(this XDocument doc, string localName)
        {
            return doc.Descendants(doc.Root.Name.Namespace.GetName(localName)).Single();
        }

        public static XElement Descendant(this XElement node, string localName)
        {
            return node.Descendants(node.Document.Root.Name.Namespace.GetName(localName)).Single();
        }

        public static void SetIfNotDisposed(this EventWaitHandle eventHandle)
        {
            try
            {
                eventHandle.Set();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public static void SetIfNotDisposed(this ManualResetEventSlim eventHandle)
        {
            try
            {
                eventHandle.Set();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
