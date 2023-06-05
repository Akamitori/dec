namespace DecTest
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class Xml : Base
    {
        [Test]
        public void DTDParse([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(StubDec) } });

            var parser = new Dec.Parser();
            parser.AddString(@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
                <Decs>
                    <StubDec decName=""TestDec"">
                    </StubDec>
                </Decs>");
            parser.Finish();

            DoParserTests(mode);

            Assert.IsNotNull(Dec.Database<StubDec>.Get("TestDec"));
        }

        [Test]
        public void IncorrectRoot([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(StubDec) } });

            var parser = new Dec.Parser();
            ExpectWarnings(() => parser.AddString(@"
                <NotDecs>
                    <StubDec decName=""TestDec"" />
                </NotDecs>"));
            parser.Finish();

            DoParserTests(mode);

            Assert.IsNotNull(Dec.Database<StubDec>.Get("TestDec"));
        }

        [Test]
        public void MultipleRoot([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(StubDec) } });

            var parser = new Dec.Parser();
            ExpectErrors(() => parser.AddString(@"
                <Decs>
                    <StubDec decName=""TestDecA"" />
                </Decs>
                <Decs>
                    <StubDec decName=""TestDecB"" />
                </Decs>"));
            parser.Finish();

            DoParserTests(mode);

            // Currently not providing any guarantees on whether these get parsed; I'd actually like for them to get parsed, but doing so is tricky
        }

        [Test]
        public void MultiXml([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[]{ typeof(StubDec) } });

            var parser = new Dec.Parser();
            parser.AddString(@"
                <Decs>
                    <StubDec decName=""TestDecA"" />
                </Decs>");
            parser.AddString(@"
                <Decs>
                    <StubDec decName=""TestDecB"" />
                </Decs>");
            parser.Finish();

            DoParserTests(mode);

            Assert.IsNotNull(Dec.Database<StubDec>.Get("TestDecA"));
            Assert.IsNotNull(Dec.Database<StubDec>.Get("TestDecB"));
        }

        [Test]
        public void ProvidedFilenameForXml([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(StubDec) } });

            var parser = new Dec.Parser();
            ExpectErrors(() => parser.AddString(@"test.xml"));
            parser.Finish();

            DoParserTests(mode);
        }

        [Test]
        public void ProperStringName([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { });

            var parser = new Dec.Parser();
            ExpectErrors(() => parser.AddString(@"
                <Decs>
                    <StubDec decName=""TestDecA"" />
                </Decs>", "TestStringName"), errorValidator: str => str.StartsWith("TestStringName"));
            parser.Finish();

            DoParserTests(mode);
        }

        [Test]
        public void Garbage([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { });

            var parser = new Dec.Parser();
            ExpectErrors(() => parser.AddString(@"�SimpleDec decName=""Hello""><value>3</value></SimpleDec>"));
            parser.Finish();

            DoParserTests(mode);
        }

        [Test]
        public void Empty([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { });

            var parser = new Dec.Parser();
            ExpectErrors(() => parser.AddString(@""));
            parser.Finish();

            DoParserTests(mode);
        }
    }
}
