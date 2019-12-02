namespace DefTest
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    [TestFixture]
    public class Record : Base
    {
        public class PrimitivesRecordable : Def.IRecordable
        {
            public int intValue;
            public float floatValue;
            public bool boolValue;
            public string stringValue;

            public void Record(Def.Recorder record)
            {
                record.Record(ref intValue, "intValue");
                record.Record(ref floatValue, "floatValue");
                record.Record(ref boolValue, "boolValue");
                record.Record(ref stringValue, "stringValue");
            }
        }

        [Test]
	    public void Primitives()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ });
            parser.AddString(@"
                <Defs>
                </Defs>");
            parser.Finish();

            var primitives = new PrimitivesRecordable();
            primitives.intValue = 42;
            primitives.floatValue = 0.1234f;
            primitives.boolValue = true;
            primitives.stringValue = "<This is a test string value with some XML-sensitive characters.>";

            string serialized = Def.Recorder.Write(primitives, pretty: true);
            var deserialized = Def.Recorder.Read<PrimitivesRecordable>(serialized);

            Assert.AreEqual(primitives.intValue, deserialized.intValue);
            Assert.AreEqual(primitives.floatValue, deserialized.floatValue);
            Assert.AreEqual(primitives.boolValue, deserialized.boolValue);
            Assert.AreEqual(primitives.stringValue, deserialized.stringValue);
        }

        public class ConverterRecordable : Def.IRecordable
        {
            public Converted convertable;

            public void Record(Def.Recorder record)
            {
                record.Record(ref convertable, "convertable");
            }
        }
        public class Converted
        {
            public int a;
            public int b;
            public int c;
        }
        public class ConvertedConverter : Def.Converter
        {
            public override HashSet<Type> HandledTypes()
            {
                return new HashSet<Type> { typeof(Converted) };
            }

            public override object FromString(string input, Type type, string inputName, int lineNumber)
            {
                var match = Regex.Match(input, "(-?[0-9]+) (-?[0-9]+) (-?[0-9]+)");
                return new Converted { a = int.Parse(match.Groups[1].Value), b = int.Parse(match.Groups[2].Value), c = int.Parse(match.Groups[3].Value) };
            }

            public override string ToString(object input)
            {
                var converted = input as Converted;
                return $"{converted.a} {converted.b} {converted.c}";
            }
        }

        [Test]
        public void Converter()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[] { }, explicitConversionTypes: new Type[] { typeof(ConvertedConverter) });
            parser.AddString(@"
                <Defs>
                </Defs>");
            parser.Finish();

            var converted = new ConverterRecordable();
            converted.convertable = new Converted();
            converted.convertable.a = 42;
            converted.convertable.b = 1234;
            converted.convertable.c = -40;

            string serialized = Def.Recorder.Write(converted, pretty: true);
            var deserialized = Def.Recorder.Read<ConverterRecordable>(serialized);

            Assert.AreEqual(converted.convertable.a, deserialized.convertable.a);
            Assert.AreEqual(converted.convertable.b, deserialized.convertable.b);
            Assert.AreEqual(converted.convertable.c, deserialized.convertable.c);
        }
    }
}
