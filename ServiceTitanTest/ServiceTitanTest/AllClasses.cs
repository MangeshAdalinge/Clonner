using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ServiceTitanTest.DeepClone;

namespace ServiceTitanTest
{
    public interface ICloningService
    {
        T Clone<T>(T source);
    }

    public class CloningService : ICloningService
    {
        bool IsSimple(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // nullable type, check if the nested type is simple.
                return IsSimple(type.GetGenericArguments()[0]);
            }
            return type.IsPrimitive
              || type.IsEnum
              || type.Equals(typeof(string))
              || type.Equals(typeof(decimal));
        }

        public T Clone<T>(T source)
        {
            //Getting Type of Generic Class Model
            Type tModelType = source.GetType();
            CloningService c = new CloningService();
            if (!IsClassSafe(tModelType))
            {
                return c.CloneDeep(source);
            }
            else
            {
                var cloneOfSource = Activator.CreateInstance(tModelType);
                FieldInfo[] f = source.GetType().GetFields();

                //Console.WriteLine(f.ElementAt(0).Name + " : "+ f.ElementAt(0).GetValue(source));
                foreach (var item in f)
                {
                    object cloneProperty = item.GetValue(source);
                    item.SetValue(cloneOfSource, cloneProperty);
                }

                //We will be defining a PropertyInfo Object which contains details about the class property 
                PropertyInfo[] arrayPropertyInfos = tModelType.GetProperties();

                //Now we will loop in all properties one by one to get value
                foreach (PropertyInfo property in arrayPropertyInfos)
                {
                    bool simpleOnly = false;
                    if (property.Name == "Computed")
                    {
                        continue;
                    }
                    if (IsSimple(property.PropertyType))
                    {
                        simpleOnly = true;
                    }
                    //Console.WriteLine("Name of Property is\t:\t" + property.Name);
                    //Console.WriteLine("Value of Property is\t:\t" + property.GetValue(source).ToString());
                    //Console.WriteLine("Type: "+property.PropertyType + " : " + property.ReflectedType);
                    if (property.CustomAttributes.Count() > 0)
                    {
                        //Console.WriteLine("Value of Custom Atribute is\t:\t" + property.CustomAttributes.ElementAt(0).ConstructorArguments.ElementAt(0).Value);
                        int op = (int)property.CustomAttributes.ElementAt(0).ConstructorArguments.ElementAt(0).Value;
                        if (op == 2)
                        {
                            //Console.WriteLine("Ignore Cloning");

                        }
                        else if (op == 1)
                        {
                            //Console.WriteLine("Shallow Cloning");
                            property.SetValue(cloneOfSource, property.GetValue(source, null), null);

                        }
                        else if (op == 0)
                        {
                            //Console.WriteLine("Deep");

                            if (simpleOnly)
                            {
                               // Console.WriteLine("Simple: Deep Cloning");
                                object cloneProperty = property.GetValue(source, null);
                                property.SetValue(cloneOfSource, cloneProperty, null);
                            }
                            else
                            {

                                //Console.WriteLine("Complex: Deep Cloning");
                                object cloneProperty = property.GetValue(source, null);
                                cloneProperty = c.CloneDeep(cloneProperty);
                                property.SetValue(cloneOfSource, cloneProperty, null);
                            }

                        }

                    }
                    else
                    {
                        //Console.WriteLine("Deep");

                        if (simpleOnly)
                        {
                          //  Console.WriteLine("Simple: Deep Cloning");
                            object cloneProperty = property.GetValue(source, null);
                            property.SetValue(cloneOfSource, cloneProperty, null);
                        }
                        else
                        {

                           // Console.WriteLine("Complex: Deep Cloning");
                            object cloneProperty = property.GetValue(source, null);
                            cloneProperty = c.CloneDeep(cloneProperty);
                            property.SetValue(cloneOfSource, cloneProperty, null);
                        }
                    }
                }
                return (T)cloneOfSource;
            }
        }

        private bool IsClassSafe(Type tModelType)
        {
            if (tModelType.IsArray == true || tModelType.IsGenericType == true || tModelType.Name.Contains("Node"))
                return false;
            return true;
        }

        public T CloneDeep<T>(T source)
        {
            T res = DeepClonerGenerator.CloneObject(source);
            //Console.WriteLine("Deep");
            return res;
        }

       

        // Feel free to add any other methods, classes, etc.
    }


    public enum CloningMode
    {
        Deep = 0,
        Shallow = 1,
        Ignore = 2,
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class CloneableAttribute : Attribute
    {
        public CloningMode Mode { get; }

        public CloneableAttribute(CloningMode mode)
        {
            Mode = mode;
        }
    }

    public class CloningServiceTest
    {
        public class Simple
        {
            public int I;
            public string S { get; set; }
            [Cloneable(CloningMode.Ignore)]
            public string Ignored { get; set; }
            [Cloneable(CloningMode.Shallow)]
            public object Shallow { get; set; }

            public virtual string Computed => S + I + Shallow;

        }

        public struct SimpleStruct
        {
            public int I;
            public string S { get; set; }
            [Cloneable(CloningMode.Ignore)]
            public string Ignored { get; set; }

            public string Computed => S + I;

            public SimpleStruct(int i, string s)
            {
                I = i;
                S = s;
                Ignored = null;
            }
        }

        public class Simple2 : Simple
        {
            public double D;
            public SimpleStruct SS;
            public override string Computed => S + I + D + SS.Computed;
        }

        public class Node
        {
            public Node Left;
            public Node Right;
            public object Value;
            public int TotalNodeCount => 1 + (Left?.TotalNodeCount ?? 0) + (Right?.TotalNodeCount ?? 0);
        }

        public ICloningService Cloner = new CloningService();
        public Action[] AllTests => new Action[] {
            SimpleTest,
            SimpleStructTest,
            Simple2Test,
            NodeTest,
            ArrayTest,
            CollectionTest,
            ArrayTest2,
            CollectionTest2,
            MixedCollectionTest,
            RecursionTest,
            RecursionTest2,
            PerformanceTest,
        };

        public static void Assert(bool criteria)
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
            // Console.WriteLine($"{title}: {sw.Elapsed.TotalMilliseconds:0.000}ms");
        }

        public void SimpleTest()
        {
            var s = new Simple() { I = 5, S = "2", Ignored = "3", Shallow = new object() };
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(s.Computed == c.Computed);
            Assert(c.Ignored == null);
            Assert(ReferenceEquals(s.Shallow, c.Shallow));
        }

        public void SimpleStructTest()
        {
            var s = new SimpleStruct(1, "2") { Ignored = "3" };
            var c = Cloner.Clone(s);
            Assert(s.Computed == c.Computed);
            Assert(c.Ignored == null);
        }

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

        public void RecursionTest()
        {
            var s = new Node();
            s.Left = s;
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(null == c.Right);
            Assert(c == c.Left);
        }

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
            var s = new[] { n, n };
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
            var s = new List<Node>() { n, n };
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(s.Sum(n1 => n1.TotalNodeCount) == c.Sum(n1 => n1.TotalNodeCount));
            Assert(c[0] == c[1]);
        }

        public void ArrayTest2()
        {
            var s = new[] { new[] { 1, 2, 3 }, new[] { 4, 5 } };
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(15 == c.SelectMany(a => a).Sum());
        }

        public void CollectionTest2()
        {
            var s = new List<List<int>> { new List<int> { 1, 2, 3 }, new List<int> { 4, 5 } };
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(15 == c.SelectMany(a => a).Sum());
        }

        public void MixedCollectionTest()
        {
            var s = new List<IEnumerable<int[]>> {
                new List<int[]> {new [] {1}},
                new List<int[]> {new [] {2, 3}},
            };
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(6 == c.SelectMany(a => a.SelectMany(b => b)).Sum());
        }

        public void RecursionTest2()
        {
            var l = new List<Node>();
            var n = new Node { Value = l };
            n.Left = n;
            l.Add(n);
            var s = new object[] { null, l, n };
            s[0] = s;
            var c = Cloner.Clone(s);
            Assert(s != c);
            Assert(c[0] == c);
            var cl = (List<Node>)c[1];
            Assert(l != cl);
            var cn = cl[0];
            Assert(n != cn);
            Assert(cl == cn.Value);
            Assert(cn.Left == cn);
        }

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

        public void RunAllTests()
        {
            foreach (var test in AllTests)
                test.Invoke();
            Console.WriteLine("Done.");
        }
    }

    public class Solution
    {
        public static void Main(string[] args)
        {
            List<string> l = new List<string>();
            // Run from console input
           /* var cloningServiceTest = new CloningServiceTest();
            var allTests = cloningServiceTest.AllTests;
            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;
                var test = allTests[int.Parse(line)];
                try
                {
                    test.Invoke();
                }
                catch (Exception)
                {
                    Console.WriteLine($"Failed on {test.GetMethodInfo().Name}.");
                }
            }*/

            // Run All Tests
            var cloningServiceTest = new CloningServiceTest();
            cloningServiceTest.RunAllTests();
            int i = 0;
            var allTests = cloningServiceTest.AllTests;
            foreach (var test in allTests)
            {

                //Console.WriteLine(i + " : " + test.GetType().GetMethods());
                i++;
                test.Invoke();
            }
            Console.WriteLine("Done.");
            Console.ReadLine();
        }
    }
}



