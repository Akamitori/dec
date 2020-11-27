namespace DefTest
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class Primitives : Base
    {
        public class IntDef : Def.Def
        {
            public int value = 4;
        }

        public class BoolDef : Def.Def
        {
            public bool value = true;
        }

        public class StringDef : Def.Def
        {
            public string value = "one";
        }

	    [Test]
	    public void EmptyIntParse([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(IntDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <IntDef defName=""TestDef"">
                        <value />
                    </IntDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<IntDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(4, result.value);
	    }

        [Test]
	    public void FailingIntParse([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(IntDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <IntDef defName=""TestDef"">
                        <value>NotAnInt</value>
                    </IntDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<IntDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(4, result.value);
	    }

        [Test]
	    public void FailingIntParse2([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(IntDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <IntDef defName=""TestDef"">
                        <value>10NotAnInt</value>
                    </IntDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<IntDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(4, result.value);
	    }

	    [Test]
	    public void EmptyBoolParse([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(BoolDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <BoolDef defName=""TestDef"">
                        <value />
                    </BoolDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<BoolDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(true, result.value);
	    }

	    [Test]
	    public void FailingBoolParse([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(BoolDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <BoolDef defName=""TestDef"">
                        <value>NotABool</value>
                    </BoolDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<BoolDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(true, result.value);
	    }

        [Test]
	    public void EmptyStringParse([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(StringDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <StringDef defName=""TestDef"">
                        <value />
                    </StringDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<StringDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual("", result.value);
	    }

        public class BulkParseDef : Def.Def
        {
            public int testIntA = 1;
            public int testIntB = 2;
            public int testIntC = 3;
            public float testFloatA = 1;
            public float testFloatB = 2;
            public float testFloatC = 3;
            public string testStringA = "one";
            public string testStringB = "two";
            public string testStringC = "three";
            public string testStringD = "four";
            public bool testBoolA = false;
            public bool testBoolB = false;
            public bool testBoolC = false;
        }

	    [Test]
	    public void BulkParse([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(BulkParseDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <BulkParseDef defName=""TestDef"">
                        <testIntA>35</testIntA>
                        <testIntB>-20</testIntB>
                        <testFloatA>0.1234</testFloatA>
                        <testFloatB>-8000000000000000</testFloatB>
                        <testStringA>Hello</testStringA>
                        <testStringB>Data, data, data</testStringB>
                        <testStringC>Forsooth</testStringC>
                        <testBoolA>true</testBoolA>
                        <testBoolB>false</testBoolB>
                    </BulkParseDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<BulkParseDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(35, result.testIntA);
            Assert.AreEqual(-20, result.testIntB);
            Assert.AreEqual(3, result.testIntC);
            Assert.AreEqual(0.1234f, result.testFloatA);
            Assert.AreEqual(-8000000000000000f, result.testFloatB);
            Assert.AreEqual(3, result.testFloatC);
            Assert.AreEqual("Hello", result.testStringA);
            Assert.AreEqual("Data, data, data", result.testStringB);
            Assert.AreEqual("Forsooth", result.testStringC);
            Assert.AreEqual("four", result.testStringD);
            Assert.AreEqual(true, result.testBoolA);
            Assert.AreEqual(false, result.testBoolB);
            Assert.AreEqual(false, result.testBoolC);
	    }

        public class MissingMemberDef : Def.Def
        {
            public int value1;
            public int value3;
        }

        [Test]
	    public void MissingMember([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(MissingMemberDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <MissingMemberDef defName=""TestDef"">
                        <value1>9</value1>
                        <value2>99</value2>
                        <value3>999</value3>
                    </MissingMemberDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<MissingMemberDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(result.value1, 9);
            Assert.AreEqual(result.value3, 999);
	    }

        public enum ExampleEnum
        {
            One,
            Two,
            Three,
        }

        public class EnumDef : Def.Def
        {
            public ExampleEnum value;
        }

        [Test]
	    public void Enum([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(EnumDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <EnumDef defName=""TestDef"">
                        <value>Two</value>
                    </EnumDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<EnumDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(result.value, ExampleEnum.Two);
	    }

        [Test]
	    public void InvalidAttribute([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(IntDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <IntDef defName=""TestDef"">
                        <value invalid=""yes"">5</value>
                    </IntDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<IntDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(result.value, 5);
	    }

        public class TypeDef : Def.Def
        {
            public Type type;
        }

        public class Example { }
        public class ContainerA { public class Overridden { } }
        public class ContainerB { public class Overridden { } public class NotOverridden { } }
        public static class Static { }
        public abstract class Abstract { }
        public class Generic<T> { }

        [Test]
	    public void TypeBasic([Values] BehaviorMode mode)
	    {
            Def.Config.UsingNamespaces = new string[] { "DefTest.Primitives" };
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>Example</type>
                    </TypeDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(result.type, typeof(Example));
	    }

        [Test]
	    public void TypeNested([Values] BehaviorMode mode)
	    {
            Def.Config.UsingNamespaces = new string[] { "DefTest.Primitives", "DefTest.Primitives.ContainerA", "DefTest.Primitives.ContainerB" };
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>NotOverridden</type>
                    </TypeDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(result.type, typeof(ContainerB.NotOverridden));
	    }

        [Test]
	    public void TypeStatic([Values] BehaviorMode mode)
	    {
            Def.Config.UsingNamespaces = new string[] { "DefTest.Primitives" };
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>Static</type>
                    </TypeDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(result.type, typeof(Static));
	    }

        [Test]
	    public void TypeAbstract([Values] BehaviorMode mode)
	    {
            Def.Config.UsingNamespaces = new string[] { "DefTest.Primitives" };
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>Abstract</type>
                    </TypeDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(result.type, typeof(Abstract));
	    }

        [Test]
	    public void TypeDefRef([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>TypeDef</type>
                    </TypeDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(result.type, typeof(TypeDef));
	    }

        [Test]
	    public void TypeGenericA([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>Generic</type>
                    </TypeDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.IsNull(result.type);
	    }

        [Test]
	    public void TypeGenericB([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>Generic&lt;&gt;</type>
                    </TypeDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.IsNull(result.type);
	    }

        [Test]
	    public void TypeGenericC([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>Generic&lt;int&gt;</type>
                    </TypeDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.IsNull(result.type);
	    }

        [Test]
	    public void TypeOverridden([Values] BehaviorMode mode)
	    {
            Def.Config.UsingNamespaces = new string[] { "DefTest.Primitives", "DefTest.Primitives.ContainerA", "DefTest.Primitives.ContainerB" };
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>Overridden</type>
                    </TypeDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode, rewrite_expectParseErrors: true);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.IsNotNull(result.type);
	    }

        [Test]
	    public void TypeComplete([Values] BehaviorMode mode)
	    {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(TypeDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <TypeDef defName=""TestDef"">
                        <type>DefTest.Primitives+Example</type> <!-- conveniently tests both namespaces and classes at the same time -->
                    </TypeDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<TypeDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(typeof(Example), result.type);
	    }

        public class Ieee754SpecialDef : Def.Def
        {
            public float floatNan;
            public float floatInf;
            public float floatNinf;
            public float floatEpsilon;
            public double doubleNan;
            public double doubleInf;
            public double doubleNinf;
            public double doubleEpsilon;
        }

        [Test]
        public void Ieee754Special([Values] BehaviorMode mode)
        {
            Def.Config.TestParameters = new Def.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(Ieee754SpecialDef) } };

            var parser = new Def.Parser();
            parser.AddString(@"
                <Defs>
                    <Ieee754SpecialDef defName=""TestDef"">
                        <floatNan>NaN</floatNan>
                        <floatInf>Infinity</floatInf>
                        <floatNinf>-Infinity</floatNinf>
                        <floatEpsilon>1.401298E-45</floatEpsilon>
                        <doubleNan>NaN</doubleNan>
                        <doubleInf>Infinity</doubleInf>
                        <doubleNinf>-Infinity</doubleNinf>
                        <doubleEpsilon>4.94065645841247E-324</doubleEpsilon>
                    </Ieee754SpecialDef>
                </Defs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Def.Database<Ieee754SpecialDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(float.NaN, result.floatNan);
            Assert.AreEqual(float.PositiveInfinity, result.floatInf);
            Assert.AreEqual(float.NegativeInfinity, result.floatNinf);
            Assert.AreEqual(float.Epsilon, result.floatEpsilon);
            Assert.AreEqual(double.NaN, result.doubleNan);
            Assert.AreEqual(double.PositiveInfinity, result.doubleInf);
            Assert.AreEqual(double.NegativeInfinity, result.doubleNinf);
            Assert.AreEqual(double.Epsilon, result.doubleEpsilon);

            // We currently don't support NaN-boxed values or signaling NaN.
            // Maybe someday we will.
            // (Does C# even accept those?)
        }
    }
}
