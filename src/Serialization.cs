namespace Dec
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;

    /// <summary>
    /// Internal serialization utilities.
    /// </summary>
    internal static class Serialization
    {
        // Initialize it to empty in order to support Recorder operations without Dec initialization.
        // At some point we'll figure out how to support Converters at that point as well.
        internal static Dictionary<Type, Converter> Converters = new Dictionary<Type, Converter>();

        internal static void Initialize()
        {
            Converters = new Dictionary<Type, Converter>();

            IEnumerable<Type> conversionTypes;
            if (Config.TestParameters == null)
            {
                conversionTypes = UtilReflection.GetAllTypes().Where(t => t.IsSubclassOf(typeof(Converter)) && !t.IsAbstract);
            }
            else if (Config.TestParameters.explicitConverters != null)
            {
                conversionTypes = Config.TestParameters.explicitConverters;
            }
            else
            {
                conversionTypes = Enumerable.Empty<Type>();
            }

            foreach (var type in conversionTypes)
            {
                var converter = (Converter)type.CreateInstanceSafe("converter", () => "converter");

                if (converter != null)
                {
                    var convertedTypes = converter.HandledTypes();
                    if (convertedTypes.Count == 0)
                    {
                        Dbg.Err($"{type} is a Dec.Converter, but doesn't convert anything");
                    }

                    foreach (var convertedType in convertedTypes)
                    {
                        if (Converters.ContainsKey(convertedType))
                        {
                            Dbg.Err($"Converters {Converters[convertedType].GetType()} and {type} both generate result {convertedType}");
                        }

                        Converters[convertedType] = converter;
                    }
                }
            }
        }

        private static object GenerateResultFallback(object model, Type type)
        {
            if (model != null)
            {
                return model;
            }
            else if (type.IsValueType)
            {
                // We don't need Safe here because all value types are required to have a default constructor.
                return Activator.CreateInstance(type);
            }
            else
            {
                return null;
            }
        }

        internal enum ParseMode
        {
            Default,
            Replace,
            //ReplaceOrCreate, // NYI
            Patch,
            //PatchOrCreate, // NYI
            //Create, //NYI
            Append,
            //Delete, //NYI
            //ReplaceIfExists, //NYI
            //PatchIfExists, //NYI
            //DeleteIfExists, //NYI
        }
        internal static ParseMode ParseModeFromString(ReaderContext context, XElement element, string str)
        {
            if (str == null)
            {
                return ParseMode.Default;
            }
            else if (str == "replace")
            {
                return ParseMode.Replace;
            }
            else if (str == "patch")
            {
                return ParseMode.Patch;
            }
            else if (str == "append")
            {
                return ParseMode.Append;
            }
            else
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid `{str}` mode!");

                return ParseMode.Default;
            }
        }

        internal static object ParseElement(XElement element, Type type, object original, ReaderContext context, Recorder.Context recContext, FieldInfo fieldInfo = null, bool isRootDec = false, bool hasReferenceId = false)
        {
            // We keep the original around in case of error, but do all our manipulation on a result object.
            object result = original;

            // The first thing we do is parse all our attributes. This is because we want to verify that there are no attributes being ignored.
            // Don't return anything until we do our element.HasAtttributes check!

            // The interaction between these is complicated!
            string nullAttribute = element.ConsumeAttribute("null");
            string refAttribute = element.ConsumeAttribute("ref");
            string classAttribute = element.ConsumeAttribute("class");
            string modeAttribute = element.ConsumeAttribute("mode");

            if (nullAttribute != null)
            {
                if (!bool.TryParse(nullAttribute, out bool nullValue))
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid `null` attribute");
                    nullAttribute = null;
                }
                else if (!nullValue)
                {
                    // why did you specify this >:(
                    nullAttribute = null;
                }
            }

            if (refAttribute != null && !context.RecorderMode)
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Found a reference tag while not evaluating Recorder mode, ignoring it");
                refAttribute = null;
            }

            ParseMode parseMode = ParseModeFromString(context, element, modeAttribute);

            // Some of these are redundant and that's OK
            if (nullAttribute != null && (refAttribute != null || classAttribute != null || modeAttribute != null))
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Null element may not have ref, class, or mode specified; guessing wildly at intentions");
            }
            else if (refAttribute != null && (nullAttribute != null || classAttribute != null || modeAttribute != null))
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Ref element may not have null, class, or mode specified; guessing wildly at intentions");
            }
            else if (classAttribute != null && (nullAttribute != null || refAttribute != null))
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Class-specified element may not have null or ref specified; guessing wildly at intentions");
            }
            else if (modeAttribute != null && (nullAttribute != null || refAttribute != null))
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Mode-specified element may not have null or ref specified; guessing wildly at intentions");
            }

            if (element.HasAttributes)
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Has unknown attributes");
            }

            if (refAttribute != null)
            {
                // Ref is the highest priority, largely because I think it's cool

                if (!recContext.Referenceable)
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Found a reference in a non-referenceable context (most likely within a Recorder.WithFactory(), which disallows shared references), using it anyway");
                }

                if (context.refs == null || !context.refs.ContainsKey(refAttribute))
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Found a reference object {refAttribute} without a valid reference mapping");
                    return result;
                }

                object refObject = context.refs[refAttribute];
                if (!type.IsAssignableFrom(refObject.GetType()))
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Reference object {refAttribute} is of type {refObject.GetType()}, which cannot be converted to expected type {type}");
                    return result;
                }

                return refObject;
            }
            else if (nullAttribute != null)
            {
                // guaranteed to be true at this point
                return null;

                // Note: It may seem wrong that we can return null along with a non-null model.
                // The problem is that this is meant to be able to override defaults. If the default if an object, explicitly setting it to null *should* clear the object out.
                // If we actually need a specific object to be returned, for whatever reason, the caller has to do the comparison.
            }
            else if (classAttribute != null)
            {
                var possibleType = (Type)ParseString(classAttribute, typeof(Type), null, context.sourceName, element.LineNumber());
                if (!type.IsAssignableFrom(possibleType))
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Explicit type {classAttribute} cannot be assigned to expected type {type}");
                }
                else if (result != null && result.GetType() != possibleType)
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Explicit type {classAttribute} does not match already-provided instance {type}");
                }
                else
                {
                    type = possibleType;
                }
            }

            // Converters may do their own processing, so we'll just defer off to them now
            if (Converters.ContainsKey(type))
            {
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Patch:
                        // easy, done
                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for a Converter parse, defaulting to Patch");
                        goto case ParseMode.Patch;
                }

                // context might be null; that's OK at the moment
                try
                {
                    result = Converters[type].Record(result, type, new RecorderReader(element, context));
                }
                catch (Exception e)
                {
                    Dbg.Ex(e);

                    result = GenerateResultFallback(result, type);
                }

                // This is an important check if we have a referenced type, because if we've changed the result, references won't link up to it properly.
                // Outside referenced types, it doesn't matter - we want to give people as much control over modification as possible.
                if (original != null && hasReferenceId && result != original)
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Converter {Converters[type].GetType()} for {type} ignored the model {original} while reading a referenced object; this may cause lost data");
                    return result;
                }

                if (result != null && !type.IsAssignableFrom(result.GetType()))
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Converter {Converters[type].GetType()} for {type} returned unexpected type {result.GetType()}");
                    result = GenerateResultFallback(original, type);
                }

                return result;
            }

            // After this point we won't be using a converter in any way, we'll be requiring native Dec types (as native as it gets, at least)

            bool hasChildren = element.Elements().Any();
            bool hasText = element.Nodes().OfType<XText>().Any();

            if (hasChildren && hasText)
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Cannot have both text and child nodes in XML - this is probably a typo, maybe you have the wrong number of close tags or added text somewhere you didn't mean to?");

                // we'll just fall through and try to parse anyway, though
            }

            if (typeof(Dec).IsAssignableFrom(type) && hasChildren && !isRootDec)
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Defining members of an item of type {type}, derived from Dec.Dec, is not supported within an outer Dec. Either reference a {type} defined independently or remove {type}'s inheritance from Dec.");
                return null;
            }

            // Special case: IRecordables
            if (typeof(IRecordable).IsAssignableFrom(type))
            {
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Patch:
                        // easy, done
                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for an IRecordable parse, defaulting to Patch");
                        goto case ParseMode.Patch;
                }

                IRecordable recordable = null;

                if (result != null)
                {
                    recordable = (IRecordable)result;
                }
                else if (recContext.factories == null)
                {
                    recordable = (IRecordable)type.CreateInstanceSafe("recordable", () => $"{context.sourceName}:{element.LineNumber()}");
                }
                else
                {
                    // Iterate back to the appropriate type.
                    Type targetType = type;
                    Func<Type, object> maker = null;
                    while (targetType != null)
                    {
                        if (recContext.factories.TryGetValue(targetType, out maker))
                        {
                            break;
                        }

                        targetType = targetType.BaseType;
                    }

                    if (maker == null)
                    {
                        recordable = (IRecordable)type.CreateInstanceSafe("recordable", () => $"{context.sourceName}:{element.LineNumber()}");
                    }
                    else
                    {
                        // want to propogate this throughout the factories list to save on time later
                        // we're actually doing the same BaseType thing again, starting from scratch
                        Type writeType = type;
                        while (writeType != targetType)
                        {
                            recContext.factories[writeType] = maker;
                            writeType = writeType.BaseType;
                        }

                        // oh right and I guess we should actually make the thing too
                        var obj = maker(type);

                        if (obj == null)
                        {
                            // fall back to default behavior
                            recordable = (IRecordable)type.CreateInstanceSafe("recordable", () => $"{context.sourceName}:{element.LineNumber()}");
                        }
                        else if (!type.IsAssignableFrom(obj.GetType()))
                        {
                            Dbg.Err($"Custom factory generated {obj.GetType()} when {type} was expected; falling back on a default object");
                            recordable = (IRecordable)type.CreateInstanceSafe("recordable", () => $"{context.sourceName}:{element.LineNumber()}");
                        }
                        else
                        {
                            // now that we've checked this is of the right type
                            recordable = (IRecordable)obj;
                        }
                    }
                }

                if (recordable != null)
                {
                    recordable.Record(new RecorderReader(element, context));

                    // TODO: support indices if this is within the Dec system?
                }

                return recordable;
            }

            // All our standard text-using options
            if (hasText ||
                (typeof(Dec).IsAssignableFrom(type) && !isRootDec) ||
                type == typeof(Type) ||
                type == typeof(string) ||
                type.IsPrimitive)
            {
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Replace:
                        // easy, done
                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for a simple string parse, defaulting to Replace");
                        goto case ParseMode.Replace;
                }

                if (hasChildren)
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Child nodes are not valid when parsing {type}");
                }

                return ParseString(element.GetText(), type, original, context.sourceName, element.LineNumber());
            }

            // Nothing past this point even supports text, so let's just get angry and break stuff.
            if (hasText)
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Text detected in a situation where it is invalid; will be ignored");
            }

            // Special case: Lists
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Replace:
                        // If you have a default list, but specify it in XML, we assume this is a full override. Clear the original list to cut down on GC churn.
                        // TODO: Is some bozo going to store the same "constant" global list on init, then be surprised when we re-use the list instead of creating a new one? Detect this and yell about it I guess.
                        // If you are reading this because you're the bozo, [insert angry emoji here], but also feel free to be annoyed that I haven't fixed it yet despite realizing it's a problem. Ping me on Discord, I'll take care of it, sorry 'bout that.
                        if (result != null)
                        {
                            ((IList)result).Clear();
                        }
                        break;

                    case ParseMode.Append:
                        // we're good
                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for a List parse, defaulting to Replace");
                        goto case ParseMode.Replace;
                }

                // List<> handling
                Type referencedType = type.GetGenericArguments()[0];

                var list = (IList)(result ?? Activator.CreateInstance(type));

                foreach (var fieldElement in element.Elements())
                {
                    if (fieldElement.Name.LocalName != "li")
                    {
                        Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: Tag should be <li>, is <{fieldElement.Name.LocalName}>");
                    }

                    list.Add(ParseElement(fieldElement, referencedType, null, context, recContext));
                }

                return list;
            }

            // Special case: Arrays
            if (type.IsArray)
            {
                Type referencedType = type.GetElementType();

                var elements = element.Elements().ToArray();

                Array array;
                int startOffset = 0;

                // This is a bit extra-complicated because we can't append stuff after the fact, we need to figure out what our length is when we create the object.
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Replace:
                        // This is a full override, so we're going to create it here.
                        // We don't bother falling back on model; we probably need to recreate it anyway with the right length.
                        array = (Array)Activator.CreateInstance(type, new object[] { elements.Length });
                        break;

                    case ParseMode.Append:
                        if (result == null)
                        {
                            goto case ParseMode.Replace;
                        }

                        // This is jankier; we create it here with the intended final length, then copy the elements over, all because arrays can't be resized
                        // (yes, I know, that's the point of arrays, I'm not complaining, just . . . grumbling a little)
                        var oldArray = (Array)result;
                        startOffset = oldArray.Length;
                        array = (Array)Activator.CreateInstance(type, new object[] { startOffset + elements.Length });
                        oldArray.CopyTo(array, 0);

                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for an Array parse, defaulting to Replace");
                        goto case ParseMode.Replace;
                }

                for (int i = 0; i < elements.Length; ++i)
                {
                    var fieldElement = elements[i];
                    if (fieldElement.Name.LocalName != "li")
                    {
                        Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: Tag should be <li>, is <{fieldElement.Name.LocalName}>");
                    }

                    array.SetValue(ParseElement(fieldElement, referencedType, null, context, recContext), startOffset + i);
                }

                return array;
            }

            // Special case: Dictionaries
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                HashSet<object> replaceableElements = null;
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Replace:
                        // If you have a default dict, but specify it in XML, we assume this is a full override. Clear the original list to cut down on GC churn.
                        // TODO: Is some bozo going to store the same "constant" global dict on init, then be surprised when we re-use the dict instead of creating a new one? Detect this and yell about it I guess.
                        // If you are reading this because you're the bozo, [insert angry emoji here], but also feel free to be annoyed that I haven't fixed it yet despite realizing it's a problem. Ping me on Discord, I'll take care of it, sorry 'bout that.
                        if (result != null)
                        {
                            ((IDictionary)result).Clear();
                        }
                        break;

                    case ParseMode.Patch:
                        if (original != null)
                        {
                            // set up the replaceableElements
                            replaceableElements = new HashSet<object>();
                            var originalDict = (IDictionary)original;
                            foreach (var originalKey in originalDict.Keys)
                            {
                                replaceableElements.Add(originalKey);
                            }
                        }
                        break;

                    case ParseMode.Append:
                        // nothing needs to be done, our existing dupe checking will solve it
                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for a Dictionary parse, defaulting to Replace");
                        goto case ParseMode.Replace;
                }

                // Dictionary<> handling
                Type keyType = type.GetGenericArguments()[0];
                Type valueType = type.GetGenericArguments()[1];

                var dict = (IDictionary)(result ?? Activator.CreateInstance(type));

                foreach (var fieldElement in element.Elements())
                {
                    if (fieldElement.Name.LocalName == "li")
                    {
                        // Treat this like a key/value pair
                        var keyNode = fieldElement.ElementNamed("key");
                        var valueNode = fieldElement.ElementNamed("value");

                        if (keyNode == null)
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: Dictionary includes li tag without a key");
                            continue;
                        }

                        if (valueNode == null)
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: Dictionary includes li tag without a value");
                            continue;
                        }

                        var key = ParseElement(keyNode, keyType, null, context, recContext);

                        if (key == null)
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: Dictionary includes null key, skipping pair");
                            continue;
                        }

                        if (replaceableElements != null && replaceableElements.Contains(key))
                        {
                            // this can be re-specified only exactly once
                            replaceableElements.Remove(key);
                        }
                        else if (dict.Contains(key))
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: Dictionary includes duplicate key {key.ToString()}");
                        }

                        dict[key] = ParseElement(valueNode, valueType, null, context, recContext);
                    }
                    else
                    {
                        var key = ParseString(fieldElement.Name.LocalName, keyType, null, context.sourceName, fieldElement.LineNumber());

                        if (key == null)
                        {
                            // it's really rare for this to happen, I think you could do it with a converter but that's it
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: Dictionary includes null key, skipping pair");
                            continue;
                        }

                        if (replaceableElements != null && replaceableElements.Contains(key))
                        {
                            // this can be re-specified only exactly once
                            replaceableElements.Remove(key);
                        }
                        else if (dict.Contains(key))
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: Dictionary includes duplicate key {fieldElement.Name.LocalName}");
                        }

                        dict[key] = ParseElement(fieldElement, valueType, null, context, recContext);
                    }
                }

                return dict;
            }

            // Special case: HashSet
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                // HashSet<> handling
                // This is a gigantic pain because HashSet<> doesn't inherit from any non-generic interface that provides the functionality we want
                // So we're stuck doing it all through object and reflection
                // Thanks, HashSet
                // This might be a performance problem and we'll . . . deal with it later I guess?
                // This might actually be a good first place to use IL generation.

                HashSet<object> replaceableElements = null;
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Replace:
                        // If you have a default set, but specify it in XML, we assume this is a full override. Clear the original set to cut down on GC churn.
                        // TODO: Is some bozo going to store the same "constant" global set on init, then be surprised when we re-use the set instead of creating a new one? Detect this and yell about it I guess.
                        // If you are reading this because you're the bozo, [insert angry emoji here], but also feel free to be annoyed that I haven't fixed it yet despite realizing it's a problem. Ping me on Discord, I'll take care of it, sorry 'bout that.
                        if (result != null)
                        {
                            // Did you know there's no non-generic interface that HashSet<> supports that includes a Clear function?
                            // Fun fact:
                            // That thing I just wrote!
                            var clearFunction = result.GetType().GetMethod("Clear");
                            clearFunction.Invoke(result, null);
                        }
                        break;

                    case ParseMode.Patch:
                        if (original != null)
                        {
                            // set up the replaceableElements
                            replaceableElements = new HashSet<object>();
                            foreach (var originalElement in (IEnumerable)original)
                            {
                                replaceableElements.Add(originalElement);
                            }
                        }
                        break;

                    case ParseMode.Append:
                        // nothing needs to be done, our existing dupe checking will solve it
                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for a HashSet parse, defaulting to Replace");
                        goto case ParseMode.Replace;
                }

                Type keyType = type.GetGenericArguments()[0];

                var set = result ?? Activator.CreateInstance(type);
                
                var containsFunction = set.GetType().GetMethod("Contains");
                var addFunction = set.GetType().GetMethod("Add");

                foreach (var fieldElement in element.Elements())
                {
                    // There's a potential bit of ambiguity here if someone does <li /> and expects that to be an actual string named "li".
                    // Practically, I think this is less likely than someone doing <li></li> and expecting that to be the empty string.
                    // And there's no other way to express the empty string.
                    // So . . . we treat that like the empty string.
                    if (fieldElement.Name.LocalName == "li")
                    {
                        // Treat this like a full node
                        var key = ParseElement(fieldElement, keyType, null, context, recContext);

                        if (key == null)
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: HashSet includes null key, skipping");
                            continue;
                        }

                        var keyParam = new object[] { key };

                        if (replaceableElements != null && replaceableElements.Contains(key))
                        {
                            // this can be re-specified only exactly once
                            replaceableElements.Remove(key);
                        }
                        else if ((bool)containsFunction.Invoke(set, keyParam))
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: HashSet includes duplicate key {key.ToString()}");
                        }

                        addFunction.Invoke(set, keyParam);
                    }
                    else
                    {
                        if (fieldElement.HasElements)
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: HashSet non-li member includes data, ignoring");
                        }

                        var key = ParseString(fieldElement.Name.LocalName, keyType, null, context.sourceName, fieldElement.LineNumber());

                        if (key == null)
                        {
                            // it's really rare for this to happen, I think you could do it with a converter but that's it
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: HashSet includes null key, skipping pair");
                            continue;
                        }

                        var keyParam = new object[] { key };

                        if (replaceableElements != null && replaceableElements.Contains(key))
                        {
                            // this can be re-specified only exactly once
                            replaceableElements.Remove(key);
                        }
                        else if ((bool)containsFunction.Invoke(set, keyParam))
                        {
                            Dbg.Err($"{context.sourceName}:{fieldElement.LineNumber()}: HashSet includes duplicate key {fieldElement.Name.LocalName}");
                        }

                        addFunction.Invoke(set, keyParam);
                    }
                }

                return set;
            }

            // Special case: A bucket of tuples
            // These are all basically identical, but AFAIK there's no good way to test them all in a better way.
            if (type.IsGenericType && (
                    type.GetGenericTypeDefinition() == typeof(Tuple<>) ||
                    type.GetGenericTypeDefinition() == typeof(Tuple<,>) ||
                    type.GetGenericTypeDefinition() == typeof(Tuple<,,>) ||
                    type.GetGenericTypeDefinition() == typeof(Tuple<,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(Tuple<,,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(Tuple<,,,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(Tuple<,,,,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(Tuple<,,,,,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(ValueTuple<>) ||
                    type.GetGenericTypeDefinition() == typeof(ValueTuple<,>) ||
                    type.GetGenericTypeDefinition() == typeof(ValueTuple<,,>) ||
                    type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,>) ||
                    type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,,>)
                    ))
            {
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Replace:
                        // easy, done
                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for a Tuple parse, defaulting to Replace");
                        goto case ParseMode.Replace;
                }

                int expectedCount = type.GenericTypeArguments.Length;

                object[] parameters = new object[expectedCount];
                var elements = element.Elements().ToList();

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
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Tuple expects {expectedCount} parameters but got {elements.Count}");
                    }

                    for (int i = 0; i < Math.Min(parameters.Length, elements.Count); ++i)
                    {
                        parameters[i] = ParseElement(elements[i], type.GenericTypeArguments[i], null, context, recContext);
                    }

                    // fill in anything missing
                    for (int i = Math.Min(parameters.Length, elements.Count); i < parameters.Length; ++i)
                    {
                        parameters[i] = GenerateResultFallback(null, type.GenericTypeArguments[i]);
                    }
                }
                else
                {
                    // We're doing named lookups instead
                    var names = fieldInfo?.GetCustomAttribute<System.Runtime.CompilerServices.TupleElementNamesAttribute>()?.TransformNames;
                    if (names == null)
                    {
                        names = Util.DefaultTupleNames;
                    }

                    if (names.Count < expectedCount)
                    {
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Not enough tuple names (this honestly shouldn't even be possible)");

                        // TODO: handle it
                    }

                    bool[] seen = new bool[expectedCount];
                    foreach (var elementItem in elements)
                    {
                        int index = names.FirstIndexOf(n => n == elementItem.Name.LocalName);

                        if (index == -1)
                        {
                            Dbg.Err($"{context.sourceName}:{elementItem.LineNumber()}: Found field with unexpected name {elementItem.Name.LocalName}");
                            continue;
                        }

                        if (seen[index])
                        {
                            Dbg.Err($"{context.sourceName}:{elementItem.LineNumber()}: Found duplicate of field {elementItem.Name.LocalName}");
                        }

                        seen[index] = true;
                        parameters[index] = ParseElement(elementItem, type.GenericTypeArguments[index], null, context, recContext);
                    }

                    for (int i = 0; i < seen.Length; ++i)
                    {
                        if (!seen[i])
                        {
                            Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Missing field with name {names[i]}");

                            // Patch it up as best we can
                            parameters[i] = GenerateResultFallback(null, type.GenericTypeArguments[i]);
                        }
                    }
                }

                // construct!
                return Activator.CreateInstance(type, parameters);
            }

            // At this point, we're either a class or a struct, and we need to do the reflection thing

            // If we have refs, something has gone wrong; we should never be doing reflection inside a Record system.
            // This is a really ad-hoc way of testing this and should be fixed.
            // One big problem here is that I'm OK with security vulnerabilities in dec xmls. Those are either supplied by the developer or by mod authors who are intended to have full code support anyway.
            // I'm less OK with security vulnerabilities in save files. Nobody expects a savefile can compromise their system.
            // And the full reflection system is probably impossible to secure, whereas the Record system should be secureable.
            if (context.RecorderMode)
            {
                Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Falling back to reflection within a Record system while parsing a {type}; this is currently not allowed for security reasons. Either you shouldn't be trying to serialize this, or it should implement Dec.IRecorder (https://zorbathut.github.io/dec/release/documentation/serialization.html), or you need a Dec.Converter (https://zorbathut.github.io/dec/release/documentation/custom.html)");
                return result;
            }

            if (!isRootDec)
            {
                switch (parseMode)
                {
                    case ParseMode.Default:
                    case ParseMode.Patch:
                        // easy, done
                        break;

                    default:
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Invalid mode {parseMode} provided for a composite reflection parse, defaulting to Patch");
                        goto case ParseMode.Patch;
                }
            }
            else
            {
                if (parseMode != ParseMode.Default)
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Mode provided for root Dec; this is currently not supported in any form");
                }
            }

            // If we haven't been given a template class from our parent, go ahead and init to defaults
            if (result == null)
            {
                result = type.CreateInstanceSafe("object", () => $"{context.sourceName}:{element.LineNumber()}");

                if (result == null)
                {
                    // error already reported
                    return result;
                }
            }

            var setFields = new HashSet<string>();
            foreach (var fieldElement in element.Elements())
            {
                // Check for fields that have been set multiple times
                string fieldName = fieldElement.Name.LocalName;
                if (setFields.Contains(fieldName))
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Duplicate field {fieldName}");
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
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Field {fieldName} does not exist in type {type}; did you mean {match}?");
                    }
                    else
                    {
                        Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Field {fieldName} does not exist in type {type}");
                    }
                    
                    continue;
                }

                if (fieldElementInfo.GetCustomAttribute<IndexAttribute>() != null)
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Attempting to set index field {fieldName}; these are generated by the dec system");
                    continue;
                }

                if (fieldElementInfo.GetCustomAttribute<NonSerializedAttribute>() != null)
                {
                    Dbg.Err($"{context.sourceName}:{element.LineNumber()}: Attempting to set nonserialized field {fieldName}");
                    continue;
                }

                fieldElementInfo.SetValue(result, ParseElement(fieldElement, fieldElementInfo.FieldType, fieldElementInfo.GetValue(result), context, recContext, fieldInfo: fieldElementInfo));
            }

            // Set up our index fields; this has to happen last in case we're a struct
            Index.Register(ref result);

            return result;
        }

        internal static object ParseString(string text, Type type, object original, string inputName, int lineNumber)
        {
            // Special case: Converter override
            // This is redundant if we're being called from ParseElement, but we aren't always.
            if (Converters.ContainsKey(type))
            {
                object result;

                try
                {
                    result = Converters[type].FromString(text, type, inputName, lineNumber);
                }
                catch (Exception e)
                {
                    Dbg.Ex(e);

                    if (type.IsValueType)
                    {
                        result = Activator.CreateInstance(type);
                    }
                    else
                    {
                        result = null;
                    }
                }

                if (result != null && !type.IsAssignableFrom(result.GetType()))
                {
                    Dbg.Err($"{inputName}:{lineNumber}: Converter {Converters[type].GetType()} for {type} returned unexpected type {result.GetType()}");
                    return null;
                }

                return result;
            }

            // Special case: decs
            if (typeof(Dec).IsAssignableFrom(type))
            {
                if (text == "" || text == null)
                {
                    // you reference nothing, you get the null (even if this isn't a specified type; null is null, after all)
                    return null;
                }
                else
                {
                    if (type.GetDecRootType() == null)
                    {
                        Dbg.Err($"{inputName}:{lineNumber}: Non-hierarchy decs cannot be used as references");
                        return null;
                    }

                    Dec result = Database.Get(type, text);
                    if (result == null)
                    {
                        // This feels very hardcoded, but these are also *by far* the most common errors I've seen, and I haven't come up with a better and more general solution
                        if (text.Contains(" "))
                        {
                            Dbg.Err($"{inputName}:{lineNumber}: Dec name \"{text}\" is not a valid identifier; consider removing spaces");
                        }
                        else if (text.Contains("\""))
                        {
                            Dbg.Err($"{inputName}:{lineNumber}: Dec name \"{text}\" is not a valid identifier; consider removing quotes");
                        }
                        else if (!Parser.DecNameValidator.IsMatch(text))
                        {
                            Dbg.Err($"{inputName}:{lineNumber}: Dec name \"{text}\" is not a valid identifier; dec identifiers must be valid C# identifiers");
                        }
                        else
                        {
                            Dbg.Err($"{inputName}:{lineNumber}: Couldn't find {type} named {text}");
                        }
                    }
                    return result;
                }
            }

            // Special case: types
            if (type == typeof(Type))
            {
                if (text == "")
                {
                    return null;
                }

                return UtilType.ParseDecFormatted(text, inputName, lineNumber);
            }

            // Various non-composite-type special-cases
            if (text != "")
            {
                // If we've got text, treat us as an object of appropriate type
                try
                {
                    return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(text);
                }
                catch (System.Exception e)  // I would normally not catch System.Exception, but TypeConverter is wrapping FormatException in an Exception for some reason
                {
                    Dbg.Err($"{inputName}:{lineNumber}: {e.ToString()}");
                    return original;
                }
            }
            else if (type == typeof(string))
            {
                // If we don't have text, and we're a string, return ""
                return "";
            }
            else
            {
                // If we don't have text, and we've fallen down to this point, that's an error (and return original value I guess)
                Dbg.Err($"{inputName}:{lineNumber}: Empty field provided for type {type}");
                return original;
            }
        }

        internal static Type TypeSystemRuntimeType = Type.GetType("System.RuntimeType");
        internal static void ComposeElement(WriterNode node, object value, Type fieldType, Recorder.Context recContext, FieldInfo fieldInfo = null, bool isRootDec = false)
        {
            // Handle Dec types, if this isn't a root (otherwise we'd just reference ourselves and that's kind of pointless)
            if (!isRootDec && value is Dec)
            {
                // Dec types are special in a few ways.
                // First off, they don't include their type data, because we assume it's of a type provided by the structure.
                // Second, we represent null values as an empty string, not as a null tag.
                // (We'll accept the null tag if you insist, we just have a cleaner special case.)
                // Null tag stuff is done further down, in the null check.

                var rootType = value.GetType().GetDecRootType();
                if (!rootType.IsAssignableFrom(fieldType))
                {
                    // The user has a Dec.Dec or similar, and it has a Dec assigned to it.
                    // This is a bit weird and is something we're not happy with; this means we need to include the Dec type along with it.
                    // But we're OK with that, honestly. We just do that.
                    // If you're saving something like this you don't get to rename Dec classes later on, but, hey, deal with it.
                    // We do, however, tag it with the root type, not the derived type; this is the most general type that still lets us search things in the future.
                    node.TagClass(rootType);
                }
                
                node.WriteDec(value as Dec);

                return;
            }

            // Everything represents "null" with an explicit XML tag, so let's just do that
            // Maybe at some point we want to special-case this for the empty Dec link
            if (value == null)
            {
                if (typeof(Dec).IsAssignableFrom(fieldType))
                {
                    node.WriteDec(null);
                }
                else
                {
                    node.WriteExplicitNull();
                }

                return;
            }

            var valType = value.GetType();

            // This is our value's type, but we may need a little bit of tinkering to make it useful.
            // The current case I know of is System.RuntimeType, which appears if we call .GetType() on a Type.
            // I assume there is a complicated internal reason for this; good news, we can ignore it and just pretend it's a System.Type.
            // Bad news: it's actually really hard to detect this case because System.RuntimeType is private.
            // That's why we have the annoying `static` up above.
            if (valType == TypeSystemRuntimeType)
            {
                valType = typeof(Type);
            }

            // If we have a type that isn't the expected type, tag it. We need to do this before any further handling because everything fits in `object`.
            if (valType != fieldType)
            {
                node.TagClass(valType);
            }

            // Do all our unreferencables first
            if (valType.IsPrimitive)
            {
                node.WritePrimitive(value);

                return;
            }

            if (value is System.Enum)
            {
                node.WriteEnum(value);

                return;
            }

            if (value is string)
            {
                node.WriteString(value as string);

                return;
            }

            if (value is Type)
            {
                node.WriteType(value as Type);

                return;
            }

            // Check to see if we should make this into a ref
            if (!valType.IsValueType)
            {
                if (node.WriteReference(value))
                {
                    // The ref system has set up the appropriate tagging, so we're done!
                    return;
                }

                // Either this isn't a reference yet, or we don't even support references in this mode. So keep on processing.
            }

            if (valType.IsArray)
            {
                node.WriteArray(value as Array);

                return;
            }

            if (valType.IsGenericType && valType.GetGenericTypeDefinition() == typeof(List<>))
            {
                node.WriteList(value as IList);

                return;
            }

            if (valType.IsGenericType && valType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                node.WriteDictionary(value as IDictionary);

                return;
            }

            if (valType.IsGenericType && valType.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                node.WriteHashSet(value as IEnumerable);

                return;
            }

            if (valType.IsGenericType && (
                    valType.GetGenericTypeDefinition() == typeof(Tuple<>) ||
                    valType.GetGenericTypeDefinition() == typeof(Tuple<,>) ||
                    valType.GetGenericTypeDefinition() == typeof(Tuple<,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(Tuple<,,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(Tuple<,,,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(Tuple<,,,,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(Tuple<,,,,,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(Tuple<,,,,,,,>)
                ))
            {
                node.WriteTuple(value, fieldInfo?.GetCustomAttribute<System.Runtime.CompilerServices.TupleElementNamesAttribute>());

                return;
            }

            if (valType.IsGenericType && (
                    valType.GetGenericTypeDefinition() == typeof(ValueTuple<>) ||
                    valType.GetGenericTypeDefinition() == typeof(ValueTuple<,>) ||
                    valType.GetGenericTypeDefinition() == typeof(ValueTuple<,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(ValueTuple<,,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,>) ||
                    valType.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,,>)
                ))
            {
                node.WriteValueTuple(value, fieldInfo?.GetCustomAttribute<System.Runtime.CompilerServices.TupleElementNamesAttribute>());

                return;
            }

            if (value is IRecordable)
            {
                node.WriteRecord(value as IRecordable);

                return;
            }

            {
                // Look for a converter; that's the only way to handle this before we fall back to reflection
                var converter = Serialization.Converters.TryGetValue(valType);
                if (converter != null)
                {
                    node.WriteConvertible(converter, value);
                    return;
                }
            }

            if (!node.AllowReflection)
            {
                Dbg.Err($"Couldn't find a composition method for type {valType}; either you shouldn't be trying to serialize it, or it should implement Dec.IRecorder (https://zorbathut.github.io/dec/release/documentation/serialization.html), or you need a Dec.Converter (https://zorbathut.github.io/dec/release/documentation/custom.html)");
                return;
            }

            // We absolutely should not be doing reflection when in recorder mode; that way lies madness.
            
            foreach (var field in valType.GetSerializableFieldsFromHierarchy())
            {
                ComposeElement(node.CreateMember(field, recContext), field.GetValue(value), field.FieldType, recContext, fieldInfo: field);
            }

            return;
        }

        internal static void Clear()
        {
            Converters = new Dictionary<Type, Converter>();
        }
    }
}
