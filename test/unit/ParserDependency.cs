using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DecTest
{
    [TestFixture]
    public class ParserDependency : Base
    {
        private static List<string> postLoadOrder;

        [SetUp]
        public void SetUp()
        {
            postLoadOrder = new List<string>();
        }

        [Dec.Abstract]
        public abstract class TestDec : Dec.Dec
        {
            public override void PostLoad(Action<string> reporter)
            {
                postLoadOrder.Add(this.GetType().Name);
            }
        }

        public class ADec : TestDec { }
        public class BDec : TestDec { }
        public class CDec : TestDec { }
        public class DDec : TestDec { }

        [Test]
        public void TestNoDependencies()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ADec), typeof(BDec), typeof(CDec), typeof(DDec) } });

            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ADec decName=""A"" />
                    <BDec decName=""B"" />
                    <CDec decName=""C"" />
                </Decs>");
            parser.Finish();

            // there's actually no guarantee about order here, but this is currently consistent
            CollectionAssert.AreEquivalent(new[] { "ADec", "BDec", "CDec" }, postLoadOrder);
        }

        [Test]
        public void TestSimpleDependency([Values] bool alphabetic)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ADec), typeof(BDec), typeof(CDec), typeof(DDec) } });

            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ADec decName=""A"" />
                    <BDec decName=""B"" />
                </Decs>");

            var dependencies = new List<Dec.Dag<Type>.Dependency>();

            if (alphabetic)
            {
                dependencies.Add(new Dec.Dag<Type>.Dependency { before = typeof(ADec), after = typeof(BDec) });
            }
            else
            {
                dependencies.Add(new Dec.Dag<Type>.Dependency { before = typeof(BDec), after = typeof(ADec) });
            }

            parser.Finish(dependencies);

            if (alphabetic)
            {
                CollectionAssert.AreEqual(new[] { "ADec", "BDec" }, postLoadOrder);
            }
            else
            {
                CollectionAssert.AreEqual(new[] { "BDec", "ADec" }, postLoadOrder);
            }
        }

        [Test]
        public void TestComplexDependencies()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ADec), typeof(BDec), typeof(CDec), typeof(DDec) } });

            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ADec decName=""A"" />
                    <BDec decName=""B"" />
                    <CDec decName=""C"" />
                    <DDec decName=""D"" />
                </Decs>");

            var dependencies = new List<Dec.Dag<Type>.Dependency>
            {
                new Dec.Dag<Type>.Dependency { before = typeof(BDec), after = typeof(ADec) },
                new Dec.Dag<Type>.Dependency { before = typeof(BDec), after = typeof(CDec) },
                new Dec.Dag<Type>.Dependency { before = typeof(CDec), after = typeof(ADec) },
                new Dec.Dag<Type>.Dependency { before = typeof(DDec), after = typeof(CDec) }
            };
            parser.Finish(dependencies);

            CollectionAssert.AreEqual(new[] { "BDec", "DDec", "CDec", "ADec" }, postLoadOrder);
        }

        [Test]
        public void TestCyclicDependencies()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ADec), typeof(BDec), typeof(CDec), typeof(DDec) } });

            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ADec decName=""A"" />
                    <BDec decName=""B"" />
                    <CDec decName=""C"" />
                </Decs>");

            var dependencies = new List<Dec.Dag<Type>.Dependency>
            {
                new Dec.Dag<Type>.Dependency { before = typeof(ADec), after = typeof(BDec) },
                new Dec.Dag<Type>.Dependency { before = typeof(BDec), after = typeof(CDec) },
                new Dec.Dag<Type>.Dependency { before = typeof(CDec), after = typeof(ADec) }
            };
            ExpectErrors(() => parser.Finish(dependencies));

            // again, not really guaranteed, but we should look into it if this changes
            CollectionAssert.AreEqual(new[] { "BDec", "CDec", "ADec" }, postLoadOrder);
        }

        [Test]
        public void TestPartialDependencies()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ADec), typeof(BDec), typeof(CDec), typeof(DDec) } });

            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ADec decName=""A"" />
                    <BDec decName=""B"" />
                    <CDec decName=""C"" />
                    <DDec decName=""D"" />
                </Decs>");

            var dependencies = new List<Dec.Dag<Type>.Dependency>
            {
                new Dec.Dag<Type>.Dependency { before = typeof(BDec), after = typeof(ADec) },
                new Dec.Dag<Type>.Dependency { before = typeof(DDec), after = typeof(CDec) }
            };

            parser.Finish(dependencies);

            // again, not really guaranteed, but we should look into it if this changes
            CollectionAssert.AreEqual(new List<string> { "BDec", "ADec", "DDec", "CDec" }, postLoadOrder);
        }

        [Test]
        public void TestDependenciesWithMissingTypes()
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ADec), typeof(BDec), typeof(CDec), typeof(DDec) } });

            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <ADec decName=""A"" />
                    <BDec decName=""B"" />
                </Decs>");

            var dependencies = new List<Dec.Dag<Type>.Dependency>
            {
                new Dec.Dag<Type>.Dependency { before = typeof(BDec), after = typeof(ADec) },
                new Dec.Dag<Type>.Dependency { before = typeof(DDec), after = typeof(CDec) }
            };

            // I don't like that this produces errors, but right now it does
            // later we'll need a test for it anyway
            ExpectErrors(() => parser.Finish(dependencies));

            CollectionAssert.AreEqual(new[] { "BDec", "ADec" }, postLoadOrder);
        }
    }
}