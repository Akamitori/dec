namespace Dec
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;

    internal class ReaderFileDecXml : ReaderFileDec
    {
        public static ReaderFileDecXml Create(string input, string identifier)
        {
            XDocument doc;

            try
            {
                doc = XDocument.Parse(input, LoadOptions.SetLineInfo);
            }
            catch (System.Xml.XmlException e)
            {
                Dbg.Ex(e);
                return null;
            }

            var result = new ReaderFileDecXml();
            result.doc = doc;
            result.fileIdentifier = identifier;
            return result;
        }

        public override List<ReaderDec> ParseDecs()
        {
            if (doc.Elements().Count() > 1)
            {
                // This isn't testable, unfortunately; XDocument doesn't even support multiple root elements.
                Dbg.Err($"{fileIdentifier}: Found {doc.Elements().Count()} root elements instead of the expected 1");
            }

            var result = new List<ReaderDec>();

            foreach (var rootElement in doc.Elements())
            {
                var rootContext = new InputContext(fileIdentifier, rootElement);
                if (rootElement.Name.LocalName != "Decs")
                {
                    Dbg.Wrn($"{rootContext}: Found root element with name `{rootElement.Name.LocalName}` when it should be `Decs`");
                }

                foreach (var decElement in rootElement.Elements())
                {
                    var readerDec = new ReaderDec();

                    readerDec.inputContext = new InputContext(fileIdentifier, decElement);
                    string typeName = decElement.Name.LocalName;

                    readerDec.type = UtilType.ParseDecFormatted(typeName, readerDec.inputContext);
                    if (readerDec.type == null || !typeof(Dec).IsAssignableFrom(readerDec.type))
                    {
                        Dbg.Err($"{readerDec.inputContext}: {typeName} is not a valid root Dec type");
                        continue;
                    }

                    if (decElement.Attribute("decName") == null)
                    {
                        Dbg.Err($"{readerDec.inputContext}: No dec name provided, add a `decName=` attribute to the {typeName} tag (example: <{typeName} decName=\"TheNameOfYour{typeName}\">)");
                        continue;
                    }

                    readerDec.name = decElement.Attribute("decName").Value;
                    if (!Util.ValidateDecName(readerDec.name, readerDec.inputContext))
                    {
                        continue;
                    }

                    // Consume decName so we know it's not hanging around
                    decElement.Attribute("decName").Remove();

                    // Check to see if we're abstract
                    {
                        var abstractAttribute = decElement.Attribute("abstract");
                        if (abstractAttribute != null)
                        {
                            if (!bool.TryParse(abstractAttribute.Value, out readerDec.abstrct))
                            {
                                Dbg.Err($"{readerDec.inputContext}: Error encountered when parsing abstract attribute");
                            }

                            abstractAttribute.Remove();
                        }
                    }

                    // Get our parent info
                    {
                        var parentAttribute = decElement.Attribute("parent");
                        if (parentAttribute != null)
                        {
                            readerDec.parent = parentAttribute.Value;

                            parentAttribute.Remove();
                        }
                    }

                    // Everything looks good!
                    readerDec.node = new ReaderNodeXml(decElement, fileIdentifier);

                    result.Add(readerDec);
                }
            }

            return result;
        }

        private XDocument doc;
        private string fileIdentifier;
    }

    internal class ReaderFileRecorderXml : ReaderFileRecorder
    {
        public static ReaderFileRecorderXml Create(string input, string identifier)
        {
            XDocument doc;

            try
            {
                doc = XDocument.Parse(input, LoadOptions.SetLineInfo);
            }
            catch (System.Xml.XmlException e)
            {
                Dbg.Ex(e);
                return null;
            }

            if (doc.Elements().Count() > 1)
            {
                // This isn't testable, unfortunately; XDocument doesn't even support multiple root elements.
                Dbg.Err($"{identifier}: Found {doc.Elements().Count()} root elements instead of the expected 1");
            }

            var record = doc.Elements().First();
            if (record.Name.LocalName != "Record")
            {
                Dbg.Wrn($"{new InputContext(identifier, record)}: Found root element with name `{record.Name.LocalName}` when it should be `Record`");
            }

            var recordFormatVersion = record.ElementNamed("recordFormatVersion");
            if (recordFormatVersion == null)
            {
                Dbg.Err($"{new InputContext(identifier, record)}: Missing record format version, assuming the data is up-to-date");
            }
            else if (recordFormatVersion.GetText() != "1")
            {
                Dbg.Err($"{new InputContext(identifier, recordFormatVersion)}: Unknown record format version {recordFormatVersion.GetText()}, expected 1 or earlier");

                // I would rather not guess about this
                return null;
            }

            var result = new ReaderFileRecorderXml();
            result.record = record;
            result.fileIdentifier = identifier;

            return result;
        }

        public override List<ReaderRef> ParseRefs()
        {
            var result = new List<ReaderRef>();

            var refs = record.ElementNamed("refs");
            if (refs != null)
            {
                foreach (var reference in refs.Elements())
                {
                    var readerRef = new ReaderRef();

                    var context = new InputContext(fileIdentifier, reference);

                    if (reference.Name.LocalName != "Ref")
                    {
                        Dbg.Wrn($"{context}: Reference element should be named 'Ref'");
                    }

                    readerRef.id = reference.Attribute("id")?.Value;
                    if (readerRef.id == null)
                    {
                        Dbg.Err($"{context}: Missing reference ID");
                        continue;
                    }

                    // Further steps don't know how to deal with this, so we just strip it
                    reference.Attribute("id").Remove();

                    var className = reference.Attribute("class")?.Value;
                    if (className == null)
                    {
                        Dbg.Err($"{context}: Missing reference class name");
                        continue;
                    }

                    readerRef.type = (Type)Serialization.ParseString(className, typeof(Type), null, context);
                    if (readerRef.type.IsValueType)
                    {
                        Dbg.Err($"{context}: Reference assigned type {readerRef.type}, which is a value type");
                        continue;
                    }

                    readerRef.node = new ReaderNodeXml(reference, fileIdentifier);
                    result.Add(readerRef);
                }
            }

            return result;
        }

        public override ReaderNode ParseNode()
        {
            var data = record.ElementNamed("data");
            if (data == null)
            {
                Dbg.Err($"{new InputContext(fileIdentifier, record)}: No data element provided. This is not very recoverable.");

                return null;
            }

            return new ReaderNodeXml(data, fileIdentifier);
        }

        private XElement record;
        private string fileIdentifier;
    }

    internal class ReaderNodeXml : ReaderNode
    {
        public ReaderNodeXml(XElement xml, string fileIdentifier)
        {
            this.xml = xml;
            this.fileIdentifier = fileIdentifier;
        }

        public override InputContext GetInputContext()
        {
            return new InputContext(fileIdentifier, xml);
        }

        public override ReaderNode GetChildNamed(string name)
        {
            var child = xml.ElementNamed(name);
            return child == null ? null : new ReaderNodeXml(child, fileIdentifier);
        }

        public override string GetText()
        {
            return xml.GetText();
        }

        public override string GetMetadata(Metadata metadata)
        {
            return xml.Attribute(metadata.ToLowerString())?.Value;
        }

        private readonly HashSet<string> metadataNames = Util.GetEnumValues<Metadata>().Select(metadata => metadata.ToLowerString()).ToHashSet();
        public override string GetMetadataUnrecognized()
        {
            if (!xml.HasAttributes)
            {
                return null;
            }

            var unrecognized = string.Join(", ", xml.Attributes().Select(attr => attr.Name.LocalName).Where(name => !metadataNames.Contains(name)));
            return unrecognized == string.Empty ? null : unrecognized;
        }

        public override int GetChildCount()
        {
            return xml.Elements().Count();
        }

        public override void ParseList(IList list, Type referencedType, ReaderContext readerContext, Recorder.Context recorderContext)
        {
            var recorderChildContext = recorderContext.CreateChild();

            foreach (var fieldElement in xml.Elements())
            {
                if (fieldElement.Name.LocalName != "li")
                {
                    var elementContext = new InputContext(fileIdentifier, fieldElement);
                    Dbg.Err($"{elementContext}: Tag should be <li>, is <{fieldElement.Name.LocalName}>");
                }

                list.Add(Serialization.ParseElement(new ReaderNodeXml(fieldElement, fileIdentifier), referencedType, null, readerContext, recorderChildContext));
            }
        }

        public override void ParseArray(Array array, Type referencedType, ReaderContext readerContext, Recorder.Context recorderContext, int startOffset)
        {
            var recorderChildContext = recorderContext.CreateChild();

            int i = 0;
            foreach (var fieldElement in xml.Elements())
            {
                if (fieldElement.Name.LocalName != "li")
                {
                    var elementContext = new InputContext(fileIdentifier, fieldElement);
                    Dbg.Err($"{elementContext}: Tag should be <li>, is <{fieldElement.Name.LocalName}>");
                }

                array.SetValue(Serialization.ParseElement(new ReaderNodeXml(fieldElement, fileIdentifier), referencedType, null, readerContext, recorderChildContext), startOffset + i++);
            }
        }

        public override void ParseDictionary(IDictionary dict, Type referencedKeyType, Type referencedValueType, ReaderContext readerContext, Recorder.Context recorderContext, bool permitPatch)
        {
            var recorderChildContext = recorderContext.CreateChild();

            // avoid the heap allocation if we can
            var writtenFields = permitPatch ? new HashSet<object>() : null;

            foreach (var fieldElement in xml.Elements())
            {
                var elementContext = new InputContext(fileIdentifier, fieldElement);

                if (fieldElement.Name.LocalName == "li")
                {
                    // Treat this like a key/value pair
                    var keyNode = fieldElement.ElementNamedWithFallback("key", elementContext, "Dictionary includes li tag without a `key`");
                    var valueNode = fieldElement.ElementNamedWithFallback("value", elementContext, "Dictionary includes li tag without a `value`");

                    if (keyNode == null)
                    {
                        // error has already been generated
                        continue;
                    }

                    if (valueNode == null)
                    {
                        // error has already been generated
                        continue;
                    }

                    var key = Serialization.ParseElement(new ReaderNodeXml(keyNode, fileIdentifier), referencedKeyType, null, readerContext, recorderChildContext);

                    if (key == null)
                    {
                        Dbg.Err($"{new InputContext(fileIdentifier, keyNode)}: Dictionary includes null key, skipping pair");
                        continue;
                    }

                    if (dict.Contains(key) && (writtenFields == null || writtenFields.Contains(key)))
                    {
                        Dbg.Err($"{elementContext}: Dictionary includes duplicate key `{key.ToString()}`");
                    }
                    writtenFields?.Add(key);

                    dict[key] = Serialization.ParseElement(new ReaderNodeXml(valueNode, fileIdentifier), referencedValueType, null, readerContext, recorderChildContext);
                }
                else
                {
                    var key = Serialization.ParseString(fieldElement.Name.LocalName, referencedKeyType, null, elementContext);

                    if (key == null)
                    {
                        // it's really rare for this to happen, I think you could do it with a converter but that's it
                        Dbg.Err($"{elementContext}: Dictionary includes null key, skipping pair");

                        // just in case . . .
                        if (string.Compare(fieldElement.Name.LocalName, "li", true, System.Globalization.CultureInfo.InvariantCulture) == 0)
                        {
                            Dbg.Err($"{elementContext}: Did you mean to write `li`? This field is case-sensitive.");
                        }

                        continue;
                    }

                    if (dict.Contains(key) && (writtenFields == null || writtenFields.Contains(key)))
                    {
                        Dbg.Err($"{elementContext}: Dictionary includes duplicate key `{key.ToString()}`");
                    }
                    writtenFields?.Add(key);

                    dict[key] = Serialization.ParseElement(new ReaderNodeXml(fieldElement, fileIdentifier), referencedValueType, null, readerContext, recorderChildContext);
                }
            }
        }

        public override void ParseHashset(object hashset, Type referencedType, ReaderContext readerContext, Recorder.Context recorderContext, bool permitPatch)
        {
            var containsFunction = hashset.GetType().GetMethod("Contains");
            var addFunction = hashset.GetType().GetMethod("Add");

            var recorderChildContext = recorderContext.CreateChild();
            var keyParam = new object[1];   // this is just to cut down on GC churn

            // avoid the heap allocation if we can
            var writtenFields = permitPatch ? new HashSet<object>() : null;

            foreach (var fieldElement in xml.Elements())
            {
                var elementContext = new InputContext(fileIdentifier, fieldElement);

                // There's a potential bit of ambiguity here if someone does <li /> and expects that to be an actual string named "li".
                // Practically, I think this is less likely than someone doing <li></li> and expecting that to be the empty string.
                // And there's no other way to express the empty string.
                // So . . . we treat that like the empty string.
                if (fieldElement.Name.LocalName == "li")
                {
                    // Treat this like a full node
                    var key = Serialization.ParseElement(new ReaderNodeXml(fieldElement, fileIdentifier), referencedType, null, readerContext, recorderChildContext);

                    if (key == null)
                    {
                        Dbg.Err($"{elementContext}: HashSet includes null key, skipping");
                        continue;
                    }

                    keyParam[0] = key;
                    
                    if ((bool)containsFunction.Invoke(hashset, keyParam) && (writtenFields == null || writtenFields.Contains(key)))
                    {
                        Dbg.Err($"{elementContext}: HashSet includes duplicate key `{key.ToString()}`");
                    }
                    writtenFields?.Add(key);

                    addFunction.Invoke(hashset, keyParam);
                }
                else
                {
                    if (fieldElement.HasElements)
                    {
                        Dbg.Err($"{elementContext}: HashSet non-li member includes data, ignoring");
                    }

                    var key = Serialization.ParseString(fieldElement.Name.LocalName, referencedType, null, elementContext);

                    if (key == null)
                    {
                        // it's really rare for this to happen, I think you could do it with a converter but that's it
                        Dbg.Err($"{elementContext}: HashSet includes null key, skipping pair");
                        continue;
                    }

                    keyParam[0] = key;

                    if ((bool)containsFunction.Invoke(hashset, keyParam) && (writtenFields == null || writtenFields.Contains(key)))
                    {
                        Dbg.Err($"{elementContext}: HashSet includes duplicate key `{key.ToString()}`");
                    }
                    writtenFields?.Add(key);

                    addFunction.Invoke(hashset, keyParam);
                }
            }
        }

        public override void ParseTuple(object[] parameters, Type referencedType, IList<string?> parameterNames, ReaderContext readerContext, Recorder.Context recorderContext)
        {
            int expectedCount = referencedType.GenericTypeArguments.Length;
            var recorderChildContext = recorderContext.CreateChild();

            var elements = xml.Elements().ToList();

            bool hasNonLi = false;
            foreach (var elementField in elements)
            {
                if (elementField.Name.LocalName != "li")
                {
                    hasNonLi = true;
                }
            }

            if (!hasNonLi)
            {
                // Treat it like an indexed array

                if (elements.Count != parameters.Length)
                {
                    Dbg.Err($"{GetInputContext()}: Tuple expects {expectedCount} parameters but got {elements.Count}");
                }

                for (int i = 0; i < Math.Min(parameters.Length, elements.Count); ++i)
                {
                    parameters[i] = Serialization.ParseElement(new ReaderNodeXml(elements[i], fileIdentifier), referencedType.GenericTypeArguments[i], null, readerContext, recorderChildContext);
                }

                // fill in anything missing
                for (int i = Math.Min(parameters.Length, elements.Count); i < parameters.Length; ++i)
                {
                    parameters[i] = Serialization.GenerateResultFallback(null, referencedType.GenericTypeArguments[i]);
                }
            }
            else
            {
                // We're doing named lookups instead
                if (parameterNames == null)
                {
                    parameterNames = Util.DefaultTupleNames;
                }

                if (parameterNames.Count < expectedCount)
                {
                    Dbg.Err($"{GetInputContext()}: Not enough tuple names (this honestly shouldn't even be possible)");

                    // TODO: handle it
                }

                bool[] seen = new bool[expectedCount];
                foreach (var elementItem in elements)
                {
                    var elementContext = new InputContext(fileIdentifier, elementItem);

                    int index = parameterNames.FirstIndexOf(n => n == elementItem.Name.LocalName);

                    if (index == -1)
                    {
                        Dbg.Err($"{elementContext}: Found field with unexpected name `{elementItem.Name.LocalName}`");
                        continue;
                    }

                    if (seen[index])
                    {
                        Dbg.Err($"{elementContext}: Found duplicate of field `{elementItem.Name.LocalName}`");
                    }

                    seen[index] = true;
                    parameters[index] = Serialization.ParseElement(new ReaderNodeXml(elementItem, fileIdentifier), referencedType.GenericTypeArguments[index], null, readerContext, recorderChildContext);
                }

                for (int i = 0; i < seen.Length; ++i)
                {
                    if (!seen[i])
                    {
                        Dbg.Err($"{GetInputContext()}: Missing field with name `{parameterNames[i]}`");

                        // Patch it up as best we can
                        parameters[i] = Serialization.GenerateResultFallback(null, referencedType.GenericTypeArguments[i]);
                    }
                }
            }
        }

        public override void ParseReflection(object obj, ReaderContext readerContext, Recorder.Context recorderContext)
        {
            var recorderChildContext = recorderContext.CreateChild();
            var setFields = new HashSet<string>();

            var type = obj.GetType();

            foreach (var fieldElement in xml.Elements())
            {
                // Check for fields that have been set multiple times
                string fieldName = fieldElement.Name.LocalName;
                if (setFields.Contains(fieldName))
                {
                    Dbg.Err($"{new InputContext(fileIdentifier, fieldElement)}: Duplicate field `{fieldName}`");
                    // Just allow us to fall through; it's an error, but one with a reasonably obvious handling mechanism
                }
                setFields.Add(fieldName);

                var fieldElementInfo = type.GetFieldFromHierarchy(fieldName);
                if (fieldElementInfo == null)
                {
                    // Try to find a close match, if we can, just for a better error message
                    string match = null;
                    string canonicalFieldName = Util.LooseMatchCanonicalize(fieldName);

                    foreach (var testField in type.GetSerializableFieldsFromHierarchy())
                    {
                        if (Util.LooseMatchCanonicalize(testField.Name) == canonicalFieldName)
                        {
                            match = testField.Name;

                            // We could in theory do something overly clever where we try to find the best name, but I really don't care that much; this is meant as a quick suggestion, not an ironclad solution.
                            break;
                        }
                    }

                    if (match != null)
                    {
                        Dbg.Err($"{new InputContext(fileIdentifier, fieldElement)}: Field `{fieldName}` does not exist in type {type}; did you mean `{match}`?");
                    }
                    else
                    {
                        Dbg.Err($"{new InputContext(fileIdentifier, fieldElement)}: Field `{fieldName}` does not exist in type {type}");
                    }

                    continue;
                }

                if (fieldElementInfo.GetCustomAttribute<IndexAttribute>() != null)
                {
                    Dbg.Err($"{new InputContext(fileIdentifier, fieldElement)}: Attempting to set index field `{fieldName}`; these are generated by the dec system");
                    continue;
                }

                if (fieldElementInfo.GetCustomAttribute<NonSerializedAttribute>() != null)
                {
                    Dbg.Err($"{new InputContext(fileIdentifier, fieldElement)}: Attempting to set nonserialized field `{fieldName}`");
                    continue;
                }

                fieldElementInfo.SetValue(obj, Serialization.ParseElement(new ReaderNodeXml(fieldElement, fileIdentifier), fieldElementInfo.FieldType, fieldElementInfo.GetValue(obj), readerContext, recorderChildContext, fieldInfo: fieldElementInfo));
            }
        }

        private XElement xml;
        private string fileIdentifier;
    }
}