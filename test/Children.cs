namespace DecTest
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class Children : Base
    {
        public class CCRoot : Dec.Dec
        {
            public CCChild child;
        }

        public class CCChild
        {
            public int value;
            public int initialized = 10;
        }

        [Test]
        public void ChildClass([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(CCRoot) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <CCRoot decName=""TestDec"">
                        <child>
                            <value>5</value>
                        </child>
                    </CCRoot>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Dec.Database<CCRoot>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.AreEqual(5, result.child.value);
            Assert.AreEqual(10, result.child.initialized);
        }

        public class CCDRoot : Dec.Dec
        {
            public CCDChild child = new CCDChild() { initialized = 8 };
        }

        public class CCDChild
        {
            public int value;
            public int initialized = 10;
        }

        [Test]
        public void ChildClassDefaults([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(CCDRoot) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <CCDRoot decName=""TestDec"">
                        <child>
                            <value>5</value>
                        </child>
                    </CCDRoot>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Dec.Database<CCDRoot>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.AreEqual(5, result.child.value);
            Assert.AreEqual(8, result.child.initialized);
        }

        public class CSRoot : Dec.Dec
        {
            public CSChild child;
        }

        public struct CSChild
        {
            public int value;
            public int valueZero;

            public CSGrandChild child;
        }

        public struct CSGrandChild
        {
            public int value;
        }

        [Test]
        public void ChildStruct([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(CSRoot) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <CSRoot decName=""TestDec"">
                        <child>
                            <value>5</value>
                            <child>
                                <value>8</value>
                            </child>
                        </child>
                    </CSRoot>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Dec.Database<CSRoot>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.AreEqual(5, result.child.value);
            Assert.AreEqual(8, result.child.child.value);
            Assert.AreEqual(0, result.child.valueZero);
        }

        public class ExplicitTypeDec : Dec.Dec
        {
            public ETBase child;
        }

        public class ExplicitTypeDerivedDec : Dec.Dec
        {
            public ETDerived child;
        }

        public class ETBase
        {

        }

        public class ETDerived : ETBase
        {
            public int value;
        }

        public class ETNotDerived
        {

        }

        [Test]
        public void ExplicitType([Values] BehaviorMode mode)
        {
            Dec.Config.UsingNamespaces = new string[] { "DecTest.Children" };
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ExplicitTypeDec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ExplicitTypeDec decName=""TestDec"">
                        <child class=""ETDerived"">
                            <value>5</value>
                        </child>
                    </ExplicitTypeDec>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Dec.Database<ExplicitTypeDec>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.IsNotNull(result.child);
            Assert.AreEqual(typeof(ETDerived), result.child.GetType());
            Assert.AreEqual(5, ( result.child as ETDerived ).value);
        }

        [Test]
        public void ExplicitTypeOverspecify([Values] BehaviorMode mode)
        {
            Dec.Config.UsingNamespaces = new string[] { "DecTest.Children" };
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ExplicitTypeDec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ExplicitTypeDec decName=""TestDec"">
                        <child class=""ETBase"">
                            
                        </child>
                    </ExplicitTypeDec>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Dec.Database<ExplicitTypeDec>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.IsNotNull(result.child);
            Assert.AreEqual(typeof(ETBase), result.child.GetType());
        }

        [Test]
        public void ExplicitTypeBackwards([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ExplicitTypeDerivedDec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ExplicitTypeDerivedDec decName=""TestDec"">
                        <child class=""ETBase"">
                            
                        </child>
                    </ExplicitTypeDerivedDec>
                </Decs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Dec.Database<ExplicitTypeDerivedDec>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.IsNotNull(result.child);
            Assert.AreEqual(typeof(ETDerived), result.child.GetType());
        }

        public class ChildPrivate
        {
            private ChildPrivate() { }
        }

        public class ChildParameter
        {
            public ChildParameter(int x) { }
        }

        public class ChildException
        {
            public ChildException() { throw new Exception(); }
        }

        public class ContainerDec : Dec.Dec
        {
            public ChildPrivate childPrivate;
            public ChildParameter childParameter;
            public ChildException childException;
        }

        [Test]
        public void Private([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ContainerDec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ContainerDec decName=""TestDec"">
                        <childPrivate />
                    </ContainerDec>
                </Decs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Dec.Database<ContainerDec>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.IsNull(result.childPrivate);
        }

        [Test]
        public void Parameter([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ContainerDec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ContainerDec decName=""TestDec"">
                        <childParameter />
                    </ContainerDec>
                </Decs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Dec.Database<ContainerDec>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.IsNull(result.childParameter);
        }

        [Test]
        public void Exception([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ContainerDec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ContainerDec decName=""TestDec"">
                        <childException />
                    </ContainerDec>
                </Decs>");
            ExpectErrors(() => parser.Finish());

            DoBehavior(mode);

            var result = Dec.Database<ContainerDec>.Get("TestDec");
            Assert.IsNotNull(result);

            Assert.IsNull(result.childException);
        }

        public class ObjectHolderDec : Dec.Dec
        {
            public object child;
        }

        [Test]
        public void Object([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ObjectHolderDec), typeof(ETBase) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ObjectHolderDec decName=""ETDec"">
                        <child class=""ETBase"" />
                    </ObjectHolderDec>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            Assert.IsInstanceOf(typeof(ETBase), Dec.Database<ObjectHolderDec>.Get("ETDec").child);
        }

        [Test]
        public void ObjectUnspecified([Values] BehaviorMode mode)
        {
            // so it turns out you can just be all "new object" and C# is like "okiedokie you're the boss"
            // wasn't really expecting that

            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ObjectHolderDec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ObjectHolderDec decName=""TestDec"">
                        <child />
                    </ObjectHolderDec>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            var result = Dec.Database<ObjectHolderDec>.Get("TestDec");
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.child);
        }

        [Test]
        public void ObjectInt([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(ObjectHolderDec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <ObjectHolderDec decName=""TestDec"">
                        <child class=""int"">42</child>
                    </ObjectHolderDec>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            Assert.AreEqual(42, Dec.Database<ObjectHolderDec>.Get("TestDec").child);
        }

        public class Int32Dec : Dec.Dec
        {
            public System.Int32 i32; // why would you do this
        }

        [Test]
        public void Int32([Values] BehaviorMode mode)
        {
            Dec.Config.TestParameters = new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(Int32Dec) } };

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <Int32Dec decName=""TestDec"">
                        <i32>42</i32>
                    </Int32Dec>
                </Decs>");
            parser.Finish();

            DoBehavior(mode);

            Assert.AreEqual(42, Dec.Database<Int32Dec>.Get("TestDec").i32);
        }
    }
}
