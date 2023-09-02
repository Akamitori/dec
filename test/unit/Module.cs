namespace DecTest
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class Module : Base
    {
        public class TwoIntsDec : Dec.Dec
        {
            public int a = -1;
            public int b = -2;
        }

        [Test]
        public void DeAbstract([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(TwoIntsDec) } });

            var parser = new Dec.ParserModular();
            parser.CreateModule("Base").AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <TwoIntsDec decName=""AbsA"" abstract=""true"">
                        <a>42</a>
                        <b>100</b>
                    </TwoIntsDec>
                    <TwoIntsDec decName=""AbsB"" abstract=""true"">
                        <a>-42</a>
                        <b>-100</b>
                    </TwoIntsDec>
                </Decs>");
            parser.CreateModule("Mod").AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <TwoIntsDec decName=""AbsA"" mode=""patch"" abstract=""false"" />
                    <TwoIntsDec decName=""ConcA"" parent=""AbsA"" />
                    <TwoIntsDec decName=""AbsB"" mode=""patch"" />
                    <TwoIntsDec decName=""AAB"" parent=""AbsB"" abstract=""true"" />
                    <TwoIntsDec decName=""ConcAAB"" parent=""AAB"" />
                </Decs>");
            parser.Finish();

            DoParserTests(mode);

            Assert.IsNotNull(Dec.Database<TwoIntsDec>.Get("AbsA"));
            Assert.IsNotNull(Dec.Database<TwoIntsDec>.Get("ConcA"));
            Assert.IsNull(Dec.Database<TwoIntsDec>.Get("AbsB"));
            Assert.IsNull(Dec.Database<TwoIntsDec>.Get("AAB"));
            Assert.IsNotNull(Dec.Database<TwoIntsDec>.Get("ConcAAB"));

            Assert.AreEqual(42, Dec.Database<TwoIntsDec>.Get("AbsA").a);
            Assert.AreEqual(42, Dec.Database<TwoIntsDec>.Get("ConcA").a);
            Assert.AreEqual(100, Dec.Database<TwoIntsDec>.Get("AbsA").b);
            Assert.AreEqual(100, Dec.Database<TwoIntsDec>.Get("ConcA").b);

            Assert.AreEqual(-42, Dec.Database<TwoIntsDec>.Get("ConcAAB").a);
            Assert.AreEqual(-100, Dec.Database<TwoIntsDec>.Get("ConcAAB").b);
        }

        [Test]
        public void InheritanceInsertion([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(TwoIntsDec) } });

            var parser = new Dec.ParserModular();
            parser.CreateModule("Base").AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <TwoIntsDec decName=""Abstract"" abstract=""true"">
                        <a>42</a>
                        <b>100</b>
                    </TwoIntsDec>
                    <TwoIntsDec decName=""Concrete"" parent=""Abstract"" />
                </Decs>");
            parser.CreateModule("Mod").AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <TwoIntsDec decName=""Abstract"" mode=""patch"">
                        <a>-42</a>
                    </TwoIntsDec>
                </Decs>");
            parser.Finish();

            DoParserTests(mode);

            Assert.IsNull(Dec.Database<TwoIntsDec>.Get("Abstract"));
            Assert.IsNotNull(Dec.Database<TwoIntsDec>.Get("Concrete"));

            Assert.AreEqual(-42, Dec.Database<TwoIntsDec>.Get("Concrete").a);
            Assert.AreEqual(100, Dec.Database<TwoIntsDec>.Get("Concrete").b);
        }

        [Test]
        public void DuplicateModule([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(TwoIntsDec) } });

            var parser = new Dec.ParserModular();
            parser.CreateModule("Base").AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <TwoIntsDec decName=""First"" />
                </Decs>");
            ExpectErrors(() => parser.CreateModule("Base").AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <TwoIntsDec decName=""Second"" />
                </Decs>"));
            parser.Finish();

            DoParserTests(mode);

            Assert.IsNotNull(Dec.Database<TwoIntsDec>.Get("First"));
            Assert.IsNotNull(Dec.Database<TwoIntsDec>.Get("Second"));
        }

        [Test]
        public void SingularModulePatch([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(TwoIntsDec) } });

            var parser = new Dec.ParserModular();
            parser.CreateModule("Base").AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <TwoIntsDec decName=""First"">
                        <a>1</a>
                        <b>2</b>
                    </TwoIntsDec>
                    <TwoIntsDec decName=""First"" mode=""patch"">
                        <a>3</a>
                        <b>4</b>
                    </TwoIntsDec>
                </Decs>");
            ExpectErrors(() => parser.Finish());

            DoParserTests(mode);

            var first = Dec.Database<TwoIntsDec>.Get("First");
            Assert.IsNotNull(first);
            Assert.AreEqual(1, first.a);
            Assert.AreEqual(2, first.b);
        }
    }
}
