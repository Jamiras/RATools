using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using System;

namespace RATools.Parser.Functions
{
    internal class RichPresenceAsciiStringLookupFunction : FunctionDefinitionExpression
    {
        public RichPresenceAsciiStringLookupFunction()
            : base("rich_presence_ascii_string_lookup")
        {
            Parameters.Add(new VariableDefinitionExpression("name"));
            Parameters.Add(new VariableDefinitionExpression("address"));
            Parameters.Add(new VariableDefinitionExpression("dictionary"));

            Parameters.Add(new VariableDefinitionExpression("fallback"));
            DefaultParameters["fallback"] = new StringConstantExpression("");
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var dictionary = GetDictionaryParameter(scope, "dictionary", out result);
            if (dictionary == null)
                return false;
            if (dictionary.Count == 0)
            {
                result = new ErrorExpression("dictionary is empty", dictionary);
                return false;
            }

            var fallback = GetStringParameter(scope, "fallback", out result);
            if (fallback == null)
                return false;

            var address = GetIntegerParameter(scope, "address", out result);
            if (address == null)
                return false;

            var maxLength = 0xFFFFFF;
            foreach (var pair in dictionary.Entries)
            {
                var stringKey = pair.Key as StringConstantExpression;
                if (stringKey == null)
                {
                    result = new ConversionErrorExpression(pair.Key, ExpressionType.StringConstant);
                    return false;
                }

                maxLength = Math.Min(stringKey.Value.Length + 1, maxLength);
            }

            int offset = 0;
            int length = 4;
            DictionaryExpression hashedDictionary = null;

            for (int i = 0; i < maxLength - 3; i += 4)
            {
                offset = i;
                hashedDictionary = BuildHashedDictionary(dictionary, offset, length);

                if (hashedDictionary != null)
                    break;
            }

            if (hashedDictionary == null)
            {
                // could not find key aligned to 4 bytes. try intermediate offsets
                for (int i = 2; i < maxLength - 3; i += 4)
                {
                    offset = i;
                    hashedDictionary = BuildHashedDictionary(dictionary, offset, length);

                    if (hashedDictionary != null)
                        break;
                }

                // still no match, try matching the end of the string
                if (hashedDictionary == null)
                {
                    length = maxLength & 3;
                    if (length > 0)
                    {
                        offset = maxLength & ~3;
                        hashedDictionary = BuildHashedDictionary(dictionary, offset, length);
                    }
                }
            }

            ExpressionBase expression = null;

            if (hashedDictionary == null)
            {
                for (length = 8; length < maxLength; length += 4)
                {
                    hashedDictionary = BuildSummedHashDictionary(dictionary, length);
                    if (hashedDictionary != null)
                    {
                        var summedExpression = new MemoryValueExpression();
                        for (int i = 0; i < length; i += 4)
                            summedExpression.ApplyMathematic(new MemoryAccessorExpression(FieldType.MemoryAddress, FieldSize.DWord, (uint)(address.Value + i)), MathematicOperation.Add);

                        expression = summedExpression;
                        break;
                    }
                }

                if (hashedDictionary == null)
                {
                    result = new ErrorExpression("Could not find a unique sequence of characters within the available keys", dictionary);
                    return false;
                }
            }
            else
            {
                switch (length)
                {
                    case 1: expression = new MemoryAccessorExpression(FieldType.MemoryAddress, FieldSize.Byte, (uint)(address.Value + offset)); break;
                    case 2: expression = new MemoryAccessorExpression(FieldType.MemoryAddress, FieldSize.Word, (uint)(address.Value + offset)); break;
                    case 3: expression = new MemoryAccessorExpression(FieldType.MemoryAddress, FieldSize.TByte, (uint)(address.Value + offset)); break;
                    default: expression = new MemoryAccessorExpression(FieldType.MemoryAddress, FieldSize.DWord, (uint)(address.Value + offset)); break;
                }
            }

            result = new RichPresenceLookupExpression(name, expression) { Items = hashedDictionary, Fallback = fallback };
            CopyLocation(result);
            result.MakeReadOnly();
            return true;
        }

        private static DictionaryExpression BuildSummedHashDictionary(DictionaryExpression dictionary, int length)
        {
            var hashedDictionary = new DictionaryExpression { Location = dictionary.Location };

            foreach (var pair in dictionary.Entries)
            {
                var stringKey = (StringConstantExpression)pair.Key;
                var hash = CreateHashKey(stringKey, 0, 4).Value;
                for (int i = 4; i < length; i += 4)
                    hash += CreateHashKey(stringKey, i, 4).Value;

                var hashKey = new IntegerConstantExpression(hash) { Location = stringKey.Location };
                if (hashedDictionary.GetEntry(hashKey) != null)
                    return null;

                hashedDictionary.Add(hashKey, pair.Value);
            }

            return hashedDictionary;
        }

        private static DictionaryExpression BuildHashedDictionary(DictionaryExpression dictionary, int offset, int length)
        {
            var hashedDictionary = new DictionaryExpression { Location = dictionary.Location };

            foreach (var pair in dictionary.Entries)
            {
                var stringKey = (StringConstantExpression)pair.Key;
                var hashKey = CreateHashKey(stringKey, offset, length);

                if (hashedDictionary.GetEntry(hashKey) != null)
                    return null;

                hashedDictionary.Add(hashKey, pair.Value);
            }

            return hashedDictionary;
        }

        private static IntegerConstantExpression CreateHashKey(StringConstantExpression stringKey, int index, int length)
        {
            int value = (index < stringKey.Value.Length) ? stringKey.Value[index] : 0;
            switch (length)
            {
                case 4:
                    if (index + 3 < stringKey.Value.Length)
                        value |= stringKey.Value[index + 3] << 24;
                    goto case 3;

                case 3:
                    if (index + 2 < stringKey.Value.Length)
                        value |= stringKey.Value[index + 2] << 16;
                    goto case 2;

                case 2:
                    if (index + 1 < stringKey.Value.Length)
                        value |= stringKey.Value[index + 1] << 8;
                    goto default;

                default:
                    break;
            }

            return new IntegerConstantExpression(value) { Location = stringKey.Location };
        }

        public override bool Invoke(InterpreterScope scope, out ExpressionBase result)
        {
            var functionCall = scope.GetContext<FunctionCallExpression>();
            result = new ErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call", functionCall.FunctionName);
            return false;
        }
    }
}
