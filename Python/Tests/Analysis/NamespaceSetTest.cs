/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.AnalysisSetDetails;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    class TestNamespace : AnalysisValue {
        public string _name;
        public override string Name {
            get { return _name; }
        }
        public string Value;
        public int MergeCount;

        public TestNamespace() {
            MergeCount = 1;
        }

        public override bool Equals(object obj) {
            var tns = obj as TestNamespace;
            if (tns == null) {
                return false;
            }
            return Name.Equals(tns.Name) && Value.Equals(tns.Value) && MergeCount.Equals(tns.MergeCount);
        }

        public override int GetHashCode() {
            return Name.GetHashCode() ^ Value.GetHashCode();
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            var tns = ns as TestNamespace;
            if (tns == null) {
                return false;
            }
            return Name.Equals(tns.Name);
        }

        internal override int UnionHashCode(int strength) {
            return Name.GetHashCode();
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            var tns = ns as TestNamespace;
            if (tns == null || object.ReferenceEquals(this, tns)) {
                return this;
            }
            return new TestNamespace { _name = Name, Value = MergeCount > tns.MergeCount ? Value : tns.Value, MergeCount = MergeCount + tns.MergeCount };
        }

        public override string ToString() {
            return string.Format("{0}:{1}", Name, Value);
        }
    }

    [TestClass]
    public class NamespaceSetTest {
        private static readonly AnalysisValue nsA1 = new TestNamespace { _name = "A", Value = "a" };
        private static readonly AnalysisValue nsA2 = new TestNamespace { _name = "A", Value = "x" };
        private static readonly AnalysisValue nsAU1 = nsA1.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU2 = nsAU1.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU3 = nsAU2.UnionMergeTypes(nsA2, 100);
        private static readonly AnalysisValue nsAU4 = nsAU3.UnionMergeTypes(nsA2, 100);

        private static readonly AnalysisValue nsB1 = new TestNamespace { _name = "B", Value = "b" };
        private static readonly AnalysisValue nsB2 = new TestNamespace { _name = "B", Value = "y" };
        private static readonly AnalysisValue nsBU1 = nsB1.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU2 = nsBU1.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU3 = nsBU2.UnionMergeTypes(nsB2, 100);
        private static readonly AnalysisValue nsBU4 = nsBU3.UnionMergeTypes(nsB2, 100);

        private static readonly AnalysisValue nsC1 = new TestNamespace { _name = "C", Value = "c" };
        private static readonly AnalysisValue nsC2 = new TestNamespace { _name = "C", Value = "z" };
        private static readonly AnalysisValue nsCU1 = nsC1.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU2 = nsCU1.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU3 = nsCU2.UnionMergeTypes(nsC2, 100);
        private static readonly AnalysisValue nsCU4 = nsCU3.UnionMergeTypes(nsC2, 100);

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
        public void SetOfTwo_Object() {
            var set = AnalysisSet.Create(new[] { nsA1, nsA2 });
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoObject));
            AssertUtil.ContainsExactly(set, nsA1, nsA2);

            set = AnalysisSet.Create(new[] { nsA1, nsA1, nsA2, nsA2 });
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoObject));
            AssertUtil.ContainsExactly(set, nsA1, nsA2);
        }

        [TestMethod, TestCategory("AnalysisSet")]
        public void SetOfTwo_Union() {
            var set = AnalysisSet.CreateUnion(new[] { nsA1, nsA2, nsB1, nsB2 }, UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoUnion));
            AssertUtil.ContainsExactly(set, nsAU1, nsBU1);

            set = AnalysisSet.CreateUnion(new[] { nsA1, nsA1, nsA2, nsA2, nsB1, nsB1, nsB2, nsB2 }, UnionComparer.Instances[0]);
            Assert.IsInstanceOfType(set, typeof(AnalysisSetTwoUnion));
            AssertUtil.ContainsExactly(set, nsAU2, nsBU2);
        }

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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



        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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

        [TestMethod, TestCategory("AnalysisSet")]
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
    }
}
