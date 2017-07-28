using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal class StringReference
    {
        public StringReference(Tokenizer lineTokenizer, int offset, string value)
        {
            var positionalTokenizer = (PositionalTokenizer)lineTokenizer;
            Line = positionalTokenizer.Line;
            Column = positionalTokenizer.Column + offset;
            Value = value;
        }

        public string Value { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
    }
}
