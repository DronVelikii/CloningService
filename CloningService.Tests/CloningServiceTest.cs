using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CloningService.Tests.types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CloningService.Tests
{
    [TestClass]
    public class CloningServiceTest
    {
        private ICloningService Cloner = new CloningService();

        private static void Assert(bool criteria)
        {
            if (!criteria)
                throw new InvalidOperationException("Assertion failed.");
        }

        public void Measure(string title, Action test)
        {
            test(); // Warmup
            var sw = new Stopwatch();
            GC.Collect();
            sw.Start();
            test();
            sw.Stop();
            Assert(sw.ElapsedMilliseconds < 1000);
            Console.WriteLine($"{title}: {sw.Elapsed.TotalMilliseconds:0.000}ms");
        }


        [TestMethod]
        public void SimpleTest()
        {
            var s = new Simple() {I = 1, S = "2", Ignored = "3", Shallow = new object()};
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(s.Computed == c.Computed);
            Assert(c.Ignored == null);
            Assert(ReferenceEquals(s.Shallow, c.Shallow));
        }

        [TestMethod]
        public void SimpleStructTest()
        {
            var s = new SimpleStruct(1, "2") {Ignored = "3"};
            var c = Cloner.Clone(s);
            Assert(s.Computed == c.Computed);
            Assert(c.Ignored == null);
        }

        [TestMethod]
        public void Simple2Test()
        {
            var s = new Simple2()
            {
                I = 1,
                S = "2",
                D = 3,
                SS = new SimpleStruct(3, "4"),
            };
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(s.Computed == c.Computed);
        }

        [TestMethod]
        public void NodeTest()
        {
            var s = new Node
            {
                Left = new Node
                {
                    Right = new Node()
                },
                Right = new Node()
            };
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(s.TotalNodeCount == c.TotalNodeCount);
        }

        [TestMethod]
        public void RecursionTest()
        {
            var s = new Node();
            s.Left = s;
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(null == c.Right);
            Assert(c == c.Left);
        }

        [TestMethod]
        public void ArrayTest()
        {
            var n = new Node
            {
                Left = new Node
                {
                    Right = new Node()
                },
                Right = new Node()
            };
            var s = new[] {n, n};
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(s.Sum(n1 => n1.TotalNodeCount) == c.Sum(n1 => n1.TotalNodeCount));
            Assert(c[0] == c[1]);
        }

        public void CollectionTest()
        {
            var n = new Node
            {
                Left = new Node
                {
                    Right = new Node()
                },
                Right = new Node()
            };
            var s = new List<Node>() {n, n};
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(s.Sum(n1 => n1.TotalNodeCount) == c.Sum(n1 => n1.TotalNodeCount));
            Assert(c[0] == c[1]);
        }

        [TestMethod]
        public void ArrayTest2()
        {
            var s = new[] {new[] {1, 2, 3}, new[] {4, 5}};
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(15 == c.SelectMany(a => a).Sum());
        }

        [TestMethod]
        public void CollectionTest2()
        {
            var s = new List<List<int>> {new List<int> {1, 2, 3}, new List<int> {4, 5}};
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(15 == c.SelectMany(a => a).Sum());
        }

        [TestMethod]
        public void MixedCollectionTest()
        {
            var s = new List<IEnumerable<int[]>>
            {
                new List<int[]> {new[] {1}},
                new List<int[]> {new[] {2, 3}},
            };
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(6 == c.SelectMany(a => a.SelectMany(b => b)).Sum());
        }

        [TestMethod]
        public void RecursionTest2()
        {
            var l = new List<Node>();
            var n = new Node {Value = l};
            n.Left = n;
            l.Add(n);
            var s = new object[] {null, l, n};
            s[0] = s;
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(c[0] == c);
            var cl = (List<Node>) c[1];
            Assert(l != cl);
            var cn = cl[0];
            Assert(n != cn);
            Assert(cl == cn.Value);
            Assert(cn.Left == cn);
        }

        [TestMethod]
        public void PerformanceTest()
        {
            Func<int, Node> makeTree = null;
            makeTree = depth =>
            {
                if (depth == 0)
                    return null;
                return new Node
                {
                    Value = depth,
                    Left = makeTree(depth - 1),
                    Right = makeTree(depth - 1),
                };
            };
            for (var i = 10; i <= 20; i++)
            {
                var root = makeTree(i);
                Measure($"Cloning {root.TotalNodeCount} nodes", () =>
                {
                    var copy = Cloner.Clone(root);
                    Assert(root != copy);
                });
            }
        }
    }
}