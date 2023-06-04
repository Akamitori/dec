namespace Dec
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Xml.Linq;

    internal abstract class Writer
    {
        public abstract bool AllowReflection { get; }
    }

    internal abstract class WriterNode
    {
        private Recorder.Context context;

        public WriterNode(Recorder.Context context)
        {
            this.context = context;
        }

        public Recorder.Context RecorderContext { get { return context; } }
        public abstract bool AllowReflection { get; }

        // I'm not real happy with the existence of this function; it's kind of a hack so that a shared Converter that writes a string or an int can avoid errors
        public void MakeRecorderContextChild()
        {
            context = context.CreateChild();
        }

        // this needs to be more abstract
        public abstract WriterNode CreateChild(string label, Recorder.Context context);

        public abstract WriterNode CreateMember(System.Reflection.FieldInfo field, Recorder.Context context);

        public abstract void WritePrimitive(object value);
        public abstract void WriteEnum(object value);
        public abstract void WriteString(string value);
        public abstract void WriteType(Type value);
        public abstract void WriteDec(Dec value);
        public abstract void WriteExplicitNull();
        public abstract bool WriteReference(object value);
        public abstract void WriteArray(Array value);
        public abstract void WriteList(IList value);
        public abstract void WriteDictionary(IDictionary value);
        public abstract void WriteHashSet(IEnumerable value);
        public abstract void WriteTuple(object value, System.Runtime.CompilerServices.TupleElementNamesAttribute names);
        public abstract void WriteValueTuple(object value, System.Runtime.CompilerServices.TupleElementNamesAttribute names);
        public abstract void WriteRecord(IRecordable value);
        public abstract void WriteConvertible(Converter converter, object value);

        public abstract void TagClass(Type type);

        // general behavior that polymorphics should not reimplement (so far at least?)
        protected bool flaggedAsClass = false;
        protected bool flaggedAsThis = false;

        // attempts to flag as self, posts error if it can't
        public bool FlagAsThis()
        {
            if (flaggedAsClass)
            {
                Dbg.Err("RecordAsThis() called on a node that was already polymorphic; this does not work, RecordAsThis() can be used only if every class involved in the This() chain is of expected type");
                return false;
            }

            flaggedAsThis = true;
            return true;
        }
        protected bool FlagAsClass()
        {
            if (flaggedAsThis)
            {
                Dbg.Err("Polymorphic Record() detected after a RecordAsThis(); this does not work, RecordAsThis() can be used only if every class involved in the This() chain is of expected type");
                return false;
            }

            flaggedAsClass = true;
            return true;
        }
    }
}
