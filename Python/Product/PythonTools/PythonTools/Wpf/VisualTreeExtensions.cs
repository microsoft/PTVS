// Python Tools for Visual Studio
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

namespace Microsoft.Common.Wpf.Extensions {
    public static class VisualTreeExtensions {
        public static T FindFirstVisualChildBreadthFirst<T>(this DependencyObject obj) where T : DependencyObject => obj.TraverseBreadthFirst(EnumerateVisualChildren).OfType<T>().FirstOrDefault();

        public static T FindFirstVisualChildOfType<T>(DependencyObject o) where T : DependencyObject {
            if (o is T) {
                return o as T;
            }
            int childrenCount = VisualTreeHelper.GetChildrenCount(o);
            for (int i = 0; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(o, i);
                var inner = FindFirstVisualChildOfType<T>(child);
                if (inner != null) {
                    return inner;
                }
            }
            return null;
        }

        public static T GetParentOfType<T>(this DependencyObject o) where T : DependencyObject {
            while (o != null) {
                var parent = VisualTreeHelper.GetParent(o);
                var typedParent = parent as T;
                if (typedParent != null) {
                    return typedParent;
                }
                o = parent;
            }

            return null;
        }

        public static IEnumerable<T> GetChildrenOfType<T>(this DependencyObject o) where T : DependencyObject {
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(o);
            while (queue.Count > 0) {
                foreach (var child in EnumerateVisualChildren(queue.Dequeue())) {
                    var typedChild = child as T;
                    if (typedChild != null) {
                        yield return typedChild;
                    } else {
                        queue.Enqueue(child);
                    }
                }
            }
        }

        public static T FindNextVisualSiblingOfType<T>(DependencyObject o) where T : DependencyObject {
            var parent = VisualTreeHelper.GetParent(o);
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            int i = 0;
            for (; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child == o) {
                    break;
                }
            }
            i++;
            for (; i < childrenCount; i++) {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T) {
                    return child as T;
                }
            }
            return null;
        }

        private static IEnumerable<DependencyObject> EnumerateVisualChildren(DependencyObject obj) {
            int childrenCount = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childrenCount; i++) {
                yield return VisualTreeHelper.GetChild(obj, i);
            }
        }
    }
}
