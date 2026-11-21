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

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace PythonToolsTests {
    [TestClass]
    public class ObservableCollectionExtensionsTests {
        private sealed class Item : IDisposable {
            public Item(string key) {
                Key = key;
            }

            public string Key { get; }
            public bool Disposed { get; private set; }

            public void Dispose() {
                Disposed = true;
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void Merge_DisposesEvictedItemsViaOnRemoved() {
            var a = new Item("a");
            var b = new Item("b");
            var c = new Item("c");
            var left = new ObservableCollection<Item> { a, b, c };
            int projected = 0;

            left.Merge(
                new[] { "b", "c", "d" },
                it => it.Key,
                s => s,
                s => { projected++; return new Item(s); },
                StringComparer.Ordinal,
                StringComparer.Ordinal,
                onRemoved: it => it.Dispose());

            Assert.IsTrue(a.Disposed, "Evicted item should be disposed via onRemoved.");
            Assert.IsFalse(b.Disposed, "Retained item should not be disposed.");
            Assert.IsFalse(c.Disposed, "Retained item should not be disposed.");
            Assert.AreEqual(1, projected, "Only newly added items should be projected.");
            CollectionAssert.AreEqual(new[] { "b", "c", "d" }, left.Select(i => i.Key).ToArray());
            Assert.AreSame(b, left.Single(i => i.Key == "b"), "Existing instance should be retained, not recreated.");
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public void Merge_WithoutOnRemoved_DoesNotThrow() {
            var left = new ObservableCollection<Item> { new Item("a") };

            left.Merge(
                new[] { "b" },
                it => it.Key,
                s => s,
                s => new Item(s),
                StringComparer.Ordinal,
                StringComparer.Ordinal);

            CollectionAssert.AreEqual(new[] { "b" }, left.Select(i => i.Key).ToArray());
        }
    }
}
