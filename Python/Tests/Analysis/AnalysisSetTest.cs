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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.AnalysisSetDetails;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    class TestAnalysisValue : AnalysisValue {
        public string _name;
        public override string Name {
            get { return _name; }
        }
        public string Value;
        public int MergeCount;

        public TestAnalysisValue() {
            MergeCount = 1;
        }

        public override bool Equals(object obj) {
            var tns = obj as TestAnalysisValue;
            if (tns == null) {
                return false;
            }
            return Name.Equals(tns.Name) && Value.Equals(tns.Value) && MergeCount.Equals(tns.MergeCount);
        }

        public override int GetHashCode() {
            return Name.GetHashCode() ^ Value.GetHashCode();
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            var tns = ns as TestAnalysisValue;
            if (tns == null) {
                return false;
            }
            return Name.Equals(tns.Name);
        }

        internal override int UnionHashCode(int strength) {
            return Name.GetHashCode();
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            var tns = ns as TestAnalysisValue;
            if (tns == null || object.ReferenceEquals(this, tns)) {
                return this;
            }
            return new TestAnalysisValue { _name = Name, Value = MergeCount > tns.MergeCount ? Value : tns.Value, MergeCount = MergeCount + tns.MergeCount };
        }

        public override string ToString() {
            return string.Format("{0}:{1}", Name, Value);
        }
    }

    class SubTestAnalysisValue : TestAnalysisValue { }

    [TestClass]
    public class AnalysisSetTest {
        private static readonly AnalysisValue nsA1 = new TestAnalysisValue { _name = "A", Value = "a" };
        private static readonly AnalysisValue nsA2 = new TestAnalysisValue { _name = "A", Value = "x" };
        private static readonly AnalysisValue nsAU1 = nsA1.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU2 = nsAU1.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU3 = nsAU2.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU4 = nsAU3.UnionMergeTypes(nsA2, 100);

        private static readonly AnalysisValue nsB1 = new TestAnalysisValue { _name = "B", Value = "b" };
        private static readonly AnalysisValue nsB2 = new TestAnalysisValue { _name = "B", Value = "y" };
        private static readonly AnalysisValue nsBU1 = nsB1.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU2 = nsBU1.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU3 = nsBU2.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU4 = nsBU3.UnionMergeTypes(nsB2, 100);

        private static readonly AnalysisValue nsC1 = new TestAnalysisValue { _name = "C", Value = "c" };
        private static readonly AnalysisValue nsC2 = new TestAnalysisValue { _name = "C", Value = "z" };
        private static readonly AnalysisValue nsCU1 = nsC1.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU2 = nsCU1.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU3 = nsCU2.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU4 = nsCU3.UnionMergeTypes(nsC2, 100);

        [TestMethod, Priority(0)]
        public void SetOfOne_Object() {
            var set = AnalysisSet.Create(nsA1);
            Assert.AreSame(nsA1, set);

            set = AnalysisSet.Create(new[] { nsA1 }.AsEnumerable());
            Assert.AreSame(nsA1, set);

            set = AnalysisSet.Create(new[] { nsA1, nsA1 }.AsEnumerable());
            Assert.AreSame(nsA1, set);

            set = AnalysisSet.Create(new[] { nsA1, nsA2 }.AsEnumerable());
            Assert.AreNotSame(nsA1, set);
        }

        [TestMethod, Priority(0)]
        public void SetOfOne_Union() {
            var set = AnalysisSet.CreateUnion(nsA1, UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetOneUnion));
            AssertUtil.ContainsExactly(set, nsA1);

            set = AnalysisSet.CreateUnion(new[] { nsA1 }.AsEnumerable(), UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetOneUnion));
            AssertUtil.ContainsExactly(set, nsA1);

            set = AnalysisSet.CreateUnion(new[] { nsA1, nsA1 }.AsEnumerable(), UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetOneUnion));
            AssertUtil.ContainsExactly(set, nsA1);

            set = AnalysisSet.CreateUnion(new[] { nsA1, nsA2 }.AsEnumerable(), UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetOneUnion));
            AssertUtil.ContainsExactly(set, nsAU1);
        }

        [TestMethod, Priority(0)]
        public void SetOfTwo_Object() {
            var set = AnalysisSet.Create(new[] { nsA1, nsA2 });
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoObject));
            AssertUtil.ContainsExactly(set, nsA1, nsA2);

            set = AnalysisSet.Create(new[] { nsA1, nsA1, nsA2, nsA2 });
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoObject));
            AssertUtil.ContainsExactly(set, nsA1, nsA2);
        }

        [TestMethod, Priority(0)]
        public void SetOfTwo_Union() {
            var set = AnalysisSet.CreateUnion(new[] { nsA1, nsA2, nsB1, nsB2 }, UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoUnion));
            AssertUtil.ContainsExactly(set, nsAU1, nsBU1);

            set = AnalysisSet.CreateUnion(new[] { nsA1, nsA1, nsA2, nsA2, nsB1, nsB1, nsB2, nsB2 }, UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoUnion));
            AssertUtil.ContainsExactly(set, nsAU2, nsBU2);
        }

        [TestMethod, Priority(0)]
        public void ManySet_Object() {
            var set = AnalysisSet.Create(new[] { nsA1, nsA2, nsB1, nsB2 });
            Assert.IsInstanceOfType(set, typeof(AnalysisHashSet));
            Assert.AreEqual(4, set.Count);
            AssertUtil.ContainsExactly(set, nsA1, nsA2, nsB1, nsB2);

            set = AnalysisSet.Create(new[] { nsA1, nsA1, nsA2, nsA2, nsB1, nsB1, nsB2, nsB2 });
            Assert.IsInstanceOfType(set, typeof(AnalysisHashSet));
            Assert.AreEqual(4, set.Count);
            AssertUtil.ContainsExactly(set, nsA1, nsA2, nsB1, nsB2);
        }

        [TestMethod, Priority(0)]
        public void ManySet_Union() {
            var set = AnalysisSet.CreateUnion(new[] { nsA1, nsA2, nsB1, nsB2, nsC1 }, UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisHashSet));
            Assert.AreEqual(3, set.Count);
            AssertUtil.ContainsExactly(set, nsAU1, nsBU1, nsC1);

            set = AnalysisSet.CreateUnion(new[] { nsA1, nsA1, nsA2, nsA2, nsB1, nsB1, nsB2, nsB2, nsC1 }, UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisHashSet));
            Assert.AreEqual(3, set.Count);
            AssertUtil.ContainsExactly(set, nsAU2, nsBU2, nsC1);
        }



        [TestMethod, Priority(0)]
        public void EmptySet_Add_Object() {
            var set = AnalysisSet.Empty;
            Assert.IsInstanceOfType(set, typeof(AnalysisSetEmptyObject));

            set = AnalysisSet.Create();
            Assert.IsInstanceOfType(set, typeof(AnalysisSetEmptyObject));
            Assert.AreSame(AnalysisSet.Empty, set);

            bool added;
            set = set.Add(nsA1, out added, false);
            Assert.IsTrue(added);
            Assert.AreSame(nsA1, set);

            set = AnalysisSet.Empty;
            set = set.Add(nsA1, out added, true);
            Assert.IsTrue(added);
            Assert.AreSame(nsA1, set);
        }

        [TestMethod, Priority(0)]
        public void EmptySet_Add_Union() {
            var set = AnalysisSet.EmptyUnion;
            Assert.IsInstanceOfType(set, typeof(AnalysisSetEmptyUnion));

            set = AnalysisSet.CreateUnion(UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetEmptyUnion));
            Assert.AreSame(AnalysisSet.EmptyUnion, set);

            bool added;
            set = set.Add(nsA1, out added, false);
            Assert.IsTrue(added);
            AssertUtil.ContainsExactly(set, nsA1);

            set = AnalysisSet.EmptyUnion;
            set = set.Add(nsA1, out added, true);
            Assert.IsTrue(added);
            AssertUtil.ContainsExactly(set, nsA1);
        }

        [TestMethod, Priority(0)]
        public void SetOfOne_Add_Object() {
            var set = AnalysisSet.Create(nsA1);

            bool added;
            set = set.Add(nsA1, out added, true);
            Assert.AreSame(nsA1, set);
            Assert.IsFalse(added);

            set = set.Add(nsA1, out added, false);
            Assert.AreSame(nsA1, set);
            Assert.IsFalse(added);

            set = set.Add(nsB1, out added, true);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoObject));
            Assert.IsTrue(added);

            set = AnalysisSet.Create(nsA1);
            var set2 = set.Add(nsA1, out added, true);
            Assert.AreSame(set, set2);
            Assert.IsFalse(added);
            AssertUtil.ContainsExactly(set2, nsA1);
        }

        [TestMethod, Priority(0)]
        public void SetOfOne_Add_Union() {
            var set = AnalysisSet.CreateUnion(nsA1, UnionComparer.Instances[0]);

            bool added;
            set = set.Add(nsA1, out added, true);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetOneUnion));
            Assert.IsFalse(added);

            set = set.Add(nsA1, out added, false);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetOneUnion));
            Assert.IsFalse(added);

            set = set.Add(nsB1, out added, true);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoUnion));
            AssertUtil.ContainsExactly(set, nsA1, nsB1);
            Assert.IsTrue(added);

            set = AnalysisSet.CreateUnion(nsA1, UnionComparer.Instances[0]);
            var set2 = set.Add(nsA2, out added, true);
            Assert.AreNotSame(set, set2);
            Assert.IsTrue(added);
            AssertUtil.ContainsExactly(set2, nsAU1);
        }

        [TestMethod, Priority(0)]
        public void SetOfTwo_Add_Object() {
            var set = AnalysisSet.Create(new[] { nsA1, nsB1 });
            IAnalysisSet set2;

            bool added;
            foreach (var o in new[] { nsA1, nsB1 }) {
                set2 = set.Add(o, out added, true);
                Assert.AreSame(set, set2);
                Assert.IsFalse(added);

                set2 = set.Add(o, out added, false);
                Assert.AreSame(set, set2);
                Assert.IsFalse(added);
            }

            foreach (var o in new[] { nsA2, nsB2, nsC1, nsC2 }) {
                set2 = set.Add(o, out added, true);
                Assert.AreNotSame(set, set2);
                AssertUtil.ContainsExactly(set2, nsA1, nsB1, o);
                Assert.IsTrue(added);

                set2 = set.Add(o, out added, false);
                Assert.AreNotSame(set, set2);
                AssertUtil.ContainsExactly(set2, nsA1, nsB1, o);
                Assert.IsTrue(added);
            }
        }

        [TestMethod, Priority(0)]
        public void SetOfTwo_Add_Union() {
            var set = AnalysisSet.CreateUnion(new[] { nsA1, nsB1 }, UnionComparer.Instances[0]);
            IAnalysisSet set2;

            bool added;
            foreach (var o in new[] { nsA1, nsB1 }) {
                set2 = set.Add(o, out added, true);
                Assert.AreSame(set, set2);
                Assert.IsFalse(added);

                set2 = set.Add(o, out added, false);
                Assert.AreSame(set, set2);
                Assert.IsFalse(added);
            }

            foreach (var o in new[] { nsC1, nsC2 }) {
                set2 = set.Add(o, out added, true);
                Assert.AreNotSame(set, set2);
                AssertUtil.ContainsExactly(set2, nsA1, nsB1, o);
                Assert.IsTrue(added);

                set2 = set.Add(o, out added, false);
                Assert.AreNotSame(set, set2);
                AssertUtil.ContainsExactly(set2, nsA1, nsB1, o);
                Assert.IsTrue(added);
            }

            set2 = set.Add(nsA2, out added, true);
            Assert.AreNotSame(set, set2);
            Assert.IsInstanceOfType(set2, typeof(AnalysisSetTwoUnion));
            AssertUtil.ContainsExactly(set2, nsAU1, nsB1);
            Assert.IsTrue(added);

            set2 = set.Add(nsB2, out added, false);
            Assert.AreNotSame(set, set2);
            Assert.IsInstanceOfType(set2, typeof(AnalysisSetTwoUnion));
            AssertUtil.ContainsExactly(set2, nsA1, nsBU1);
            Assert.IsTrue(added);
        }

        [TestMethod, Priority(0)]
        public void ManySet_Add_Object() {
            var set = AnalysisSet.Create(new[] { nsA1, nsB1, nsC1 });
            IAnalysisSet set2;

            bool added;
            foreach (var o in new[] { nsA1, nsB1, nsC1 }) {
                set2 = set.Add(o, out added, true);
                Assert.AreSame(set, set2);
                Assert.IsFalse(added);

                set2 = set.Add(o, out added, false);
                Assert.AreSame(set, set2);
                Assert.IsFalse(added);
            }

            foreach (var o in new[] { nsA2, nsB2, nsC2 }) {
                set = AnalysisSet.Create(new[] { nsA1, nsB1, nsC1 });
                set2 = set.Add(o, out added, false);
                Assert.AreNotSame(set, set2);
                AssertUtil.ContainsExactly(set2, nsA1, nsB1, nsC1, o);
                Assert.IsTrue(added);

                set2 = set.Add(o, out added, true);
                Assert.AreSame(set, set2);
                AssertUtil.ContainsExactly(set2, nsA1, nsB1, nsC1, o);
                Assert.IsTrue(added);
            }
        }

        [TestMethod, Priority(0)]
        public void ManySet_Add_Union() {
            var set = AnalysisSet.CreateUnion(new[] { nsA1, nsB1, nsC1 }, UnionComparer.Instances[0]);
            IAnalysisSet set2;

            bool added;
            foreach (var o in new[] { nsA1, nsB1, nsC1 }) {
                set2 = set.Add(o, out added, true);
                Assert.AreSame(set, set2);
                Assert.IsFalse(added);

                set2 = set.Add(o, out added, false);
                Assert.AreSame(set, set2);
                Assert.IsFalse(added);
            }

            set2 = set.Add(nsA2, out added, true);
            Assert.AreSame(set, set2);
            Assert.IsInstanceOfType(set2, typeof(AnalysisHashSet));
            AssertUtil.ContainsExactly(set2, nsAU1, nsB1, nsC1);
            Assert.IsTrue(added);

            set = AnalysisSet.CreateUnion(new[] { nsA1, nsB1, nsC1 }, UnionComparer.Instances[0]);
            set2 = set.Add(nsA2, out added, false);
            Assert.AreNotSame(set, set2);
            Assert.IsInstanceOfType(set2, typeof(AnalysisHashSet));
            AssertUtil.ContainsExactly(set2, nsAU1, nsB1, nsC1);
            Assert.IsTrue(added);

            set2 = set.Add(nsB2, out added, false);
            Assert.AreNotSame(set, set2);
            Assert.IsInstanceOfType(set2, typeof(AnalysisHashSet));
            AssertUtil.ContainsExactly(set2, nsA1, nsBU1, nsC1);
            Assert.IsTrue(added);

            set2 = set.Add(nsC2, out added, false);
            Assert.AreNotSame(set, set2);
            Assert.IsInstanceOfType(set2, typeof(AnalysisHashSet));
            AssertUtil.ContainsExactly(set2, nsA1, nsB1, nsCU1);
            Assert.IsTrue(added);
        }

        [TestMethod, Priority(0)]
        public void Set_PredicateSplit() {
            IAnalysisSet trueSet, falseSet;
            Assert.IsTrue(nsA1.Split(v => v.Name == "A", out trueSet, out falseSet));
            Assert.AreSame(nsA1, trueSet);
            Assert.AreEqual(0, falseSet.Count);

            Assert.IsFalse(nsA1.Split(v => v.Name != "A", out trueSet, out falseSet));
            Assert.AreEqual(0, trueSet.Count);
            Assert.AreSame(nsA1, falseSet);

            foreach (var cmp in new IEqualityComparer<AnalysisValue>[] { ObjectComparer.Instance, UnionComparer.Instances[0] }) {
                var set = AnalysisSet.Create(new[] { nsA1, nsB1, nsC1 }, cmp);

                Assert.IsTrue(set.Split(v => v.Name == "A", out trueSet, out falseSet));
                Assert.AreEqual(1, trueSet.Count);
                Assert.AreEqual(set.Count - 1, falseSet.Count);
                Assert.AreSame(set.Comparer, trueSet.Comparer);
                Assert.AreSame(set.Comparer, falseSet.Comparer);

                Assert.IsFalse(set.Split(v => v.Name == "X", out trueSet, out falseSet));
                Assert.AreEqual(0, trueSet.Count);
                Assert.AreEqual(set.Count, falseSet.Count);
                Assert.AreSame(set.Comparer, trueSet.Comparer);
                Assert.AreSame(set.Comparer, falseSet.Comparer);

                Assert.IsTrue(set.Split(v => v.Name != null, out trueSet, out falseSet));
                Assert.AreEqual(set.Count, trueSet.Count);
                Assert.AreEqual(0, falseSet.Count);
                Assert.AreSame(set.Comparer, trueSet.Comparer);
                Assert.AreSame(set.Comparer, falseSet.Comparer);
            }
        }

        [TestMethod, Priority(0)]
        public void Set_TypeSplit() {
            IReadOnlyList<SubTestAnalysisValue> ofType;
            IAnalysisSet rest;

            var testAv = new TestAnalysisValue { _name = "A", Value = "A" };
            var subTestAv = new SubTestAnalysisValue { _name = "B", Value = "B" };

            Assert.IsTrue(subTestAv.Split(out ofType, out rest));
            Assert.AreEqual(1, ofType.Count);
            Assert.AreSame(subTestAv, ofType[0]);
            Assert.AreEqual(0, rest.Count);

            Assert.IsFalse(testAv.Split(out ofType, out rest));
            Assert.AreEqual(0, ofType.Count);
            Assert.AreSame(testAv, rest);

            var set = AnalysisSet.Create(new[] { testAv, subTestAv });

            Assert.IsTrue(set.Split(out ofType, out rest));
            Assert.AreEqual(1, ofType.Count);
            Assert.AreSame(testAv, rest);

            set = AnalysisSet.Create(new[] { nsA1, nsB1, nsC1 });
            Assert.IsFalse(set.Split(out ofType, out rest));
            Assert.AreEqual(0, ofType.Count);
            Assert.AreSame(set, rest);
        }
    }
}
