namespace DecTest
{
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class Bespoke : Base
    {
        public class Base
        {
            public int data;

        }

        public class Derived : Base { }
        public class DerivedAnother : Base
        {
            public int more;
        }

        public class KeyTypeDictHolderDec : Dec.Dec, Dec.IRecordable
        {
            public System.Collections.Generic.Dictionary<Type, Base> dict;

            public void Record(Dec.Recorder recorder)
            {
                recorder.Bespoke_KeyTypeDict().Record(ref dict, "dict");
            }
        }

        [Test]
        public void KeyTypeDict([Values] ParserMode mode)
        {
            UpdateTestParameters(new Dec.Config.UnitTestParameters { explicitTypes = new Type[] { typeof(KeyTypeDictHolderDec), typeof(Derived), typeof(DerivedAnother) } });

            var parser = new Dec.Parser();
            parser.AddString(Dec.Parser.FileType.Xml, @"
                <Decs>
                    <KeyTypeDictHolderDec decName=""TestDec"">
                        <dict>
                            <Derived><data>1</data></Derived>
                            <DerivedAnother><data>2</data><more>3</more></DerivedAnother>
                         </dict>
                    </KeyTypeDictHolderDec>
                </Decs>");
            parser.Finish();

            DoParserTests(mode);

            var ktd = Dec.Database<KeyTypeDictHolderDec>.Get("TestDec");

            Assert.AreEqual(2, ktd.dict.Count);
            Assert.AreEqual(1, (ktd.dict[typeof(Derived)] as Derived).data);
            Assert.AreEqual(2, (ktd.dict[typeof(DerivedAnother)] as DerivedAnother).data);
            Assert.AreEqual(3, (ktd.dict[typeof(DerivedAnother)] as DerivedAnother).more);
        }
    }
}
