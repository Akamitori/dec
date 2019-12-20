namespace DefTest
{
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [TestFixture]
    public class Defs : Base
    {
        [Test]
	    public void TrivialParse()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(StubDef) });
            parser.AddString(@"
                <Defs>
                    <StubDef defName=""TestDef"">
                    </StubDef>
                </Defs>");
            parser.Finish();

            Assert.IsNotNull(Def.Database<StubDef>.Get("TestDef"));
	    }

        [Test]
	    public void TrivialEmptyParse()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(StubDef) });
            parser.AddString(@"
                <Defs>
                    <StubDef defName=""TestDef"" />
                </Defs>");
            parser.Finish();

            Assert.IsNotNull(Def.Database<StubDef>.Get("TestDef"));
	    }
        
        [Test]
	    public void NonDefType()
	    {
            Def.Parser parser = null;
            ExpectErrors(() => parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(bool), typeof(StubDef) }));
            parser.AddString(@"
                <Defs>
                    <StubDef defName=""TestDef"" />
                </Defs>");
            parser.Finish();

            Assert.IsNotNull(Def.Database<StubDef>.Get("TestDef"));
	    }

        [Test]
	    public void MissingDefType()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(StubDef) });
            ExpectErrors(() => parser.AddString(@"
                <Defs>
                    <NonexistentDef defName=""TestDefA"" />
                    <StubDef defName=""TestDefB"" />
                </Defs>"));
            parser.Finish();

            Assert.IsNull(Def.Database<StubDef>.Get("TestDefA"));
            Assert.IsNotNull(Def.Database<StubDef>.Get("TestDefB"));
	    }

        [Test]
	    public void MissingDefName()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(StubDef) });
            ExpectErrors(() => parser.AddString(@"
                <Defs>
                    <StubDef />
                </Defs>"));
            parser.Finish();
	    }

        [Test]
	    public void InvalidDefName()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(StubDef) });
            ExpectErrors(() => parser.AddString(@"
                <Defs>
                    <StubDef defName=""1NumberPrefix"" />
                    <StubDef defName=""Contains Spaces"" />
                    <StubDef defName=""HasPunctuation!"" />
                </Defs>"));
            parser.Finish();

            Assert.IsNull(Def.Database<StubDef>.Get("1NumberPrefix"));
            Assert.IsNull(Def.Database<StubDef>.Get("Contains Spaces"));
            Assert.IsNull(Def.Database<StubDef>.Get("HasPunctuation!"));
	    }

        public class IntDef : Def.Def
        {
            public int value = 4;
        }

        [Test]
	    public void DuplicateField()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(IntDef) });
            parser.AddString(@"
                <Defs>
                    <IntDef defName=""TestDef"">
                        <value>3</value>
                        <value>6</value>
                    </IntDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            var result = Def.Database<IntDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(6, result.value);
	    }

        [Test]
	    public void DuplicateDef()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(IntDef) });
            ExpectErrors(() => parser.AddString(@"
                <Defs>
                    <IntDef defName=""TestDef"">
                        <value>10</value>
                    </IntDef>
                    <IntDef defName=""TestDef"">
                        <value>20</value>
                    </IntDef>
                </Defs>"));
            parser.Finish();

            var result = Def.Database<IntDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(20, result.value);
	    }

        public class DeepParentDef : Def.Def
        {
            public int value = 4;
        }

        public class DeepChildDef : DeepParentDef
        {
            
        }

        [Test]
	    public void HierarchyDeepField()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(DeepChildDef) });
            parser.AddString(@"
                <Defs>
                    <DeepChildDef defName=""TestDef"">
                        <value>12</value>
                    </DeepChildDef>
                </Defs>");
            parser.Finish();

            var result = Def.Database<DeepParentDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(12, result.value);
	    }

        public class DupeParentDef : Def.Def
        {
            public int value = 4;
        }

        public class DupeChildDef : DupeParentDef
        {
            new public int value = 8;
        }

        [Test]
	    public void HierarchyDuplicateField()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(DupeChildDef) });
            parser.AddString(@"
                <Defs>
                    <DupeChildDef defName=""TestDef"">
                        <value>12</value>
                    </DupeChildDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            var result = (DupeChildDef)Def.Database<DupeParentDef>.Get("TestDef");
            Assert.IsNotNull(result);

            Assert.AreEqual(12, result.value);
            Assert.AreEqual(4, ((DupeParentDef)result).value);
	    }

        [Test]
	    public void ExtraAttribute()
	    {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(StubDef) });
            parser.AddString(@"
                <Defs>
                    <StubDef defName=""TestDef"" invalidAttribute=""hello"" />
                </Defs>");
            ExpectErrors(() => parser.Finish());

            Assert.IsNotNull(Def.Database<StubDef>.Get("TestDef"));
	    }

        public class StubBetaDef : Def.Def
        {

        }

        public class StubChildDef : StubDef
        {

        }

        [Test]
	    public void DebugPrint()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(StubDef) });
            parser.AddString(@"
                <Defs>
                    <StubDef defName=""TestDef"" />
                </Defs>");
            parser.Finish();

            Assert.IsNotNull(Def.Database<StubDef>.Get("TestDef"));

            Assert.AreEqual(Def.Database<StubDef>.Get("TestDef").ToString(), "TestDef");
        }

        public class ErrorDef : Def.Def
        {
            public override IEnumerable<string> ConfigErrors()
            {
                foreach (var err in base.ConfigErrors())
                {
                    yield return err;
                }

                yield return "I am never valid";
            }
        }

        [Test]
	    public void ConfigErrors()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(ErrorDef) });
            parser.AddString(@"
                <Defs>
                    <ErrorDef defName=""TestDef"" />
                </Defs>");
            ExpectErrors(() => parser.Finish());

            Assert.IsNotNull(Def.Database<ErrorDef>.Get("TestDef"));
        }

        public class PostLoadDef : Def.Def
        {
            public bool initted = false;

            public override IEnumerable<string> PostLoad()
            {
                foreach (var err in base.PostLoad())
                {
                    yield return err;
                }

                initted = true;
            }
        }

        [Test]
	    public void PostLoad()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[]{ typeof(PostLoadDef) });
            parser.AddString(@"
                <Defs>
                    <PostLoadDef defName=""TestDef"" />
                </Defs>");
            parser.Finish();

            Assert.IsNotNull(Def.Database<PostLoadDef>.Get("TestDef"));
            Assert.IsTrue(Def.Database<PostLoadDef>.Get("TestDef").initted);
        }

        class DefMemberDef : Def.Def
        {
            public Def.Def invalidReference;
        }

        [Test]
        public void DefMember()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[] { typeof(DefMemberDef) });
            parser.AddString(@"
                <Defs>
                    <DefMemberDef defName=""TestDef"">
                        <invalidReference>TestDef</invalidReference>
                    </DefMemberDef>
                </Defs>");
            ExpectErrors(() => parser.Finish());

            Assert.IsNotNull(Def.Database<DefMemberDef>.Get("TestDef"));
            Assert.IsNull(Def.Database<DefMemberDef>.Get("TestDef").invalidReference);
        }

        class SelfReferentialDef : Def.Def
        {
            public SelfReferentialDef recursive;
        }

        [Test]
        public void SelfReferential()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[] { typeof(SelfReferentialDef) });
            parser.AddString(@"
                <Defs>
                    <SelfReferentialDef defName=""TestDef"">
                        <recursive>TestDef</recursive>
                    </SelfReferentialDef>
                </Defs>");
            parser.Finish();

            Assert.IsNotNull(Def.Database<SelfReferentialDef>.Get("TestDef"));
            Assert.AreSame(Def.Database<SelfReferentialDef>.Get("TestDef"), Def.Database<SelfReferentialDef>.Get("TestDef").recursive);
        }

        class LooseMatchDef : Def.Def
        {
            public string cat;
            public string snake_case;
            public string camelCase;
        }

        [Test]
        public void LooseMatchCapitalization()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[] { typeof(LooseMatchDef) });
            parser.AddString(@"
                <Defs>
                    <LooseMatchDef defName=""TestDef"">
                        <Cat>words</Cat>
                    </LooseMatchDef>
                </Defs>");
            ExpectErrors(() => parser.Finish(), err => err.Contains("cat"));
        }

        [Test]
        public void LooseMatchSnakeToCamel()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[] { typeof(LooseMatchDef) });
            parser.AddString(@"
                <Defs>
                    <LooseMatchDef defName=""TestDef"">
                        <snakeCase>words</snakeCase>
                    </LooseMatchDef>
                </Defs>");
            ExpectErrors(() => parser.Finish(), err => err.Contains("snake_case"));
        }

        [Test]
        public void LooseMatchCamelToSnake()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[] { typeof(LooseMatchDef) });
            parser.AddString(@"
                <Defs>
                    <LooseMatchDef defName=""TestDef"">
                        <camel_case>words</camel_case>
                    </LooseMatchDef>
                </Defs>");
            ExpectErrors(() => parser.Finish(), err => err.Contains("camelCase"));
        }

        [Test]
        public void ForbiddenField()
        {
            var parser = new Def.Parser(explicitOnly: true, explicitTypes: new Type[] { typeof(StubDef) });

            // This is a little silly because, as of this writing, DefName is a property and we don't even support writing to properties.
            // So we're not really testing forbidden fields here. We're really just double-checking the fact that properties can't be written to.
            // But someday I'll probably support properties, and then this had better work.
            parser.AddString(@"
                <Defs>
                    <StubDef defName=""TestDef"">
                        <DefName>NotTestDef</DefName>
                    </StubDef>
                </Defs>");

            // Just in case I rename it back to lowercase, make sure we don't just get a spelling mismatch error here.
            ExpectErrors(() => parser.Finish(), err => !err.Contains("defName"));

            Assert.AreEqual("TestDef", Def.Database<StubDef>.Get("TestDef").DefName);
        }
    }
}
