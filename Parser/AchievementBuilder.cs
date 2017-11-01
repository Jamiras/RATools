using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RATools.Data;
using Jamiras.Components;
using System.Text;
using System.Collections;

namespace RATools.Parser.Internal
{
    [DebuggerDisplay("{Title} Core:{_core.Count} Alts:{_alts.Count}")]
    public class AchievementBuilder
    {
        public AchievementBuilder()
        {
            Current = _core = new List<Requirement>();
            _alts = new List<List<Requirement>>();
        }

        public AchievementBuilder(Achievement source)
            : this()
        {
            Title = source.Title;
            Description = source.Description;
            Points = source.Points;
            Id = source.Id;
            BadgeName = source.BadgeName;

            _core.AddRange(source.CoreRequirements);
            foreach (var alt in source.AlternateRequirements)
                _alts.Add(new List<Requirement>(alt));

            if (_alts.Count > 0)
                Current = _alts.Last();
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public int Points { get; set; }
        public int Id { get; set; }
        public string BadgeName { get; set; }

        private List<Requirement> _core;
        private List<List<Requirement>> _alts;

        internal List<Requirement> Current { get; set; }

        internal bool IsInNot { get; set; }
        internal int EqualityModifier { get; set; }

        public void BeginAlt()
        {
            if (Current == _core || Current.Count > 0)
            {
                var newAlt = new List<Requirement>();
                Current = newAlt;
                _alts.Add(newAlt);
            }
        }

        public Requirement LastRequirement
        {
            get { return Current.Last(); }
        }

        public Achievement ToAchievement()
        {
            var achievement = new Achievement { Title = Title, Description = Description, Points = Points, CoreRequirements = _core.ToArray(), Id = Id, BadgeName = BadgeName };
            var alts = new Requirement[_alts.Count][];
            for (int i = 0; i < _alts.Count; i++)
                alts[i] = _alts[i].ToArray();
            achievement.AlternateRequirements = alts;
            return achievement;
        }

        public void ParseRequirements(Tokenizer tokenizer)
        {
            do
            {
                var requirement = new Requirement();

                if (tokenizer.Match("R:"))
                    requirement.Type = RequirementType.ResetIf;
                else if (tokenizer.Match("P:"))
                    requirement.Type = RequirementType.PauseIf;

                requirement.Left = ReadField(tokenizer);
                requirement.Operator = ReadOperator(tokenizer);
                requirement.Right = ReadField(tokenizer);

                if (requirement.Right.Size == FieldSize.None)
                    requirement.Right = new Field { Type = requirement.Right.Type, Size = requirement.Left.Size, Value = requirement.Right.Value };

                Current.Add(requirement);

                if (tokenizer.NextChar == '.')
                {
                    tokenizer.Advance(); // first period
                    requirement.HitCount = (ushort)ReadNumber(tokenizer);
                    tokenizer.Advance(); // second period
                }
                else if (tokenizer.NextChar == '(') // old format
                {
                    tokenizer.Advance(); // '('
                    requirement.HitCount = (ushort)ReadNumber(tokenizer);
                    tokenizer.Advance(); // ')'
                }

                switch (tokenizer.NextChar)
                {
                    default: 
                        return;

                    case '_': // &&
                        tokenizer.Advance();
                        continue;

                    case 'S': // ||
                        tokenizer.Advance();
                        BeginAlt();
                        continue;
                }

            } while (true);
        }

        private static uint ReadNumber(Tokenizer tokenizer)
        {
            uint value = 0;
            while (tokenizer.NextChar >= '0' && tokenizer.NextChar <= '9')
            {
                value *= 10;
                value += (uint)(tokenizer.NextChar - '0');
                tokenizer.Advance();
            }

            return value;
        }

        private static Field ReadField(Tokenizer tokenizer)
        {
            var fieldType = FieldType.MemoryAddress;
            if (tokenizer.NextChar == 'd')
            {
                fieldType = FieldType.PreviousValue;
                tokenizer.Advance();
            }

            if (!tokenizer.Match("0x"))
                return new Field { Type = FieldType.Value, Value = ReadNumber(tokenizer) };

            FieldSize size = FieldSize.None;
            switch (tokenizer.NextChar)
            {
                case 'M': size = FieldSize.Bit0; tokenizer.Advance(); break;
                case 'N': size = FieldSize.Bit1; tokenizer.Advance(); break;
                case 'O': size = FieldSize.Bit2; tokenizer.Advance(); break;
                case 'P': size = FieldSize.Bit3; tokenizer.Advance(); break;
                case 'Q': size = FieldSize.Bit4; tokenizer.Advance(); break;
                case 'R': size = FieldSize.Bit5; tokenizer.Advance(); break;
                case 'S': size = FieldSize.Bit6; tokenizer.Advance(); break;
                case 'T': size = FieldSize.Bit7; tokenizer.Advance(); break;
                case 'L': size = FieldSize.LowNibble; tokenizer.Advance(); break;
                case 'U': size = FieldSize.HighNibble; tokenizer.Advance(); break;
                case 'H': size = FieldSize.Byte; tokenizer.Advance(); break;
                case 'X': size = FieldSize.DWord; tokenizer.Advance(); break;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9': size = FieldSize.Word; break;
                case ' ': size = FieldSize.Word; tokenizer.Advance(); break;
            }

            uint address = 0;
            do
            {
                uint charValue = 255;
                switch (tokenizer.NextChar)
                {
                    case '0': charValue = 0; break;
                    case '1': charValue = 1; break;
                    case '2': charValue = 2; break;
                    case '3': charValue = 3; break;
                    case '4': charValue = 4; break;
                    case '5': charValue = 5; break;
                    case '6': charValue = 6; break;
                    case '7': charValue = 7; break;
                    case '8': charValue = 8; break;
                    case '9': charValue = 9; break;
                    case 'a':
                    case 'A': charValue = 10; break;
                    case 'b':
                    case 'B': charValue = 11; break;
                    case 'c':
                    case 'C': charValue = 12; break;
                    case 'd':
                    case 'D': charValue = 13; break;
                    case 'e':
                    case 'E': charValue = 14; break;
                    case 'f':
                    case 'F': charValue = 15; break;
                }

                if (charValue == 255)
                    break;

                tokenizer.Advance();
                address <<= 4;
                address += charValue;
            } while (true);

            return new Field { Size = size, Type = fieldType, Value = address };
        }

        private static RequirementOperator ReadOperator(Tokenizer tokenizer)
        {
            switch (tokenizer.NextChar)
            {
                case '=':
                    tokenizer.Advance();
                    return RequirementOperator.Equal;

                case '!':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        return RequirementOperator.NotEqual;
                    }
                    break;

                case '<':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        return RequirementOperator.LessThanOrEqual;
                    }
                    return RequirementOperator.LessThan;

                case '>':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        return RequirementOperator.GreaterThanOrEqual;
                    }
                    return RequirementOperator.GreaterThan;
            }

            return RequirementOperator.None;
        }

        public string SerializeRequirements()
        {
            return SerializeRequirements(_core, _alts);
        }

        public static string SerializeRequirements(Achievement achievement)
        {
            return SerializeRequirements(achievement.CoreRequirements, achievement.AlternateRequirements);
        }

        private static string SerializeRequirements(IEnumerable core, IEnumerable alts)
        {
            var builder = new StringBuilder();

            foreach (Requirement requirement in core)
            {
                SerializeRequirement(requirement, builder);
                builder.Append('_');
            }

            builder.Length--; // remove last _

            foreach (IEnumerable<Requirement> alt in alts)
            {
                builder.Append('S');
                foreach (Requirement requirement in alt)
                {
                    SerializeRequirement(requirement, builder);
                    builder.Append('_');
                }

                builder.Length--; // remove last _
            }

            return builder.ToString();
        }

        private static void SerializeRequirement(Requirement requirement, StringBuilder builder)
        {
            if (requirement.Type == RequirementType.ResetIf)
                builder.Append("R:");
            else if (requirement.Type == RequirementType.PauseIf)
                builder.Append("P:");

            requirement.Left.Serialize(builder);

            switch (requirement.Operator)
            {
                case RequirementOperator.Equal: builder.Append('='); break;
                case RequirementOperator.NotEqual: builder.Append("!="); break;
                case RequirementOperator.LessThan: builder.Append('<'); break;
                case RequirementOperator.LessThanOrEqual: builder.Append("<="); break;
                case RequirementOperator.GreaterThan: builder.Append('>'); break;
                case RequirementOperator.GreaterThanOrEqual: builder.Append(">="); break;
            }

            requirement.Right.Serialize(builder);

            if (requirement.HitCount > 0)
            {
                builder.Append('.');
                builder.Append(requirement.HitCount);
                builder.Append('.');
            }
        }

        internal bool AreRequirementsSame(AchievementBuilder right)
        {
            if (!AreRequirementsSame(_core, right._core))
                return false;

            var enum1 = _alts.GetEnumerator();
            var enum2 = right._alts.GetEnumerator();
            while (enum1.MoveNext())
            {
                if (!enum2.MoveNext())
                    return false;

                if (!AreRequirementsSame(enum1.Current, enum2.Current))
                    return false;
            }

            return !enum2.MoveNext();
        }

        private static bool AreRequirementsSame(IEnumerable<Requirement> left, IEnumerable<Requirement> right)
        {
            var rightRequirements = new List<Requirement>(right);
            var enumerator = left.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int index = -1;
                for (int i = 0; i < rightRequirements.Count; i++)
                {
                    if (rightRequirements[i] == enumerator.Current)
                    {
                        index = i;
                        break;
                    }
                }
                if (index == -1)
                    return false;

                rightRequirements.RemoveAt(index);
                if (rightRequirements.Count == 0)
                    return !enumerator.MoveNext();
            }

            return rightRequirements.Count == 0;
        }

        private static void NormalizeComparisons(List<Requirement> requirements)
        {
            var alwaysTrue = new List<Requirement>();
            var alwaysFalse = new List<Requirement>();

            foreach (var requirement in requirements)
            {
                if (requirement.Right.Type != FieldType.Value)
                    continue;

                if (requirement.Right.Value == 0)
                {
                    switch (requirement.Operator)
                    {
                        case RequirementOperator.LessThan: // n < 0 -> never true
                            alwaysFalse.Add(requirement);
                            continue;

                        case RequirementOperator.LessThanOrEqual: // n <= 0 -> n == 0
                            requirement.Operator = RequirementOperator.Equal;
                            break;

                        case RequirementOperator.GreaterThan: // n > 0 -> n != 0
                            requirement.Operator = RequirementOperator.NotEqual;
                            break;

                        case RequirementOperator.GreaterThanOrEqual: // n >= 0 -> always true
                            alwaysTrue.Add(requirement);
                            continue;
                    }
                }

                uint max;

                switch (requirement.Left.Size)
                {
                    case FieldSize.Bit0:
                    case FieldSize.Bit1:
                    case FieldSize.Bit2:
                    case FieldSize.Bit3:
                    case FieldSize.Bit4:
                    case FieldSize.Bit5:
                    case FieldSize.Bit6:
                    case FieldSize.Bit7:
                        if (requirement.Right.Value == 0 && requirement.Operator == RequirementOperator.NotEqual) // bit != 0 -> bit == 1
                        {
                            requirement.Operator = RequirementOperator.Equal;
                            requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 1 };
                            continue;
                        }
                        if (requirement.Right.Value == 1)
                        {
                            switch (requirement.Operator)
                            {
                                case RequirementOperator.NotEqual: // bit != 1 -> bit == 0
                                case RequirementOperator.LessThan: // bit < 1 -> bit == 0
                                    requirement.Operator = RequirementOperator.Equal;
                                    requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 0 };
                                    continue;
                            }
                        }
                        max = 1;
                        break;

                    case FieldSize.LowNibble:
                    case FieldSize.HighNibble:
                        max = 15;
                        break;

                    case FieldSize.Byte:
                        max = 255;
                        break;

                    case FieldSize.Word:
                        max = 65535;
                        break;

                    default:
                    case FieldSize.DWord:
                        max = uint.MaxValue;
                        break;
                }

                if (requirement.Right.Value > max)
                    requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = max };

                if (requirement.Right.Value == max)
                {
                    switch (requirement.Operator)
                    {
                        case RequirementOperator.GreaterThan: // n > max -> always false
                            alwaysFalse.Add(requirement);
                            break;

                        case RequirementOperator.GreaterThanOrEqual: // n >= max -> n == max
                            requirement.Operator = RequirementOperator.Equal;
                            break;

                        case RequirementOperator.LessThanOrEqual: // n <= max -> always true
                            alwaysTrue.Add(requirement);
                            break;

                        case RequirementOperator.LessThan: // n < max -> n != max
                            requirement.Operator = RequirementOperator.NotEqual;
                            break;
                    }
                }
            }

            if (alwaysFalse.Count > 0)
            {
                requirements.Clear();
            }
            else
            {
                foreach (var requirement in alwaysTrue)
                    requirements.Remove(requirement);
            }
        }

        private static bool MergeRequirements(Requirement left, Requirement right, ConditionalOperation condition, out Requirement merged)
        {
            merged = null;
            if (left.Type != right.Type)
                return false;

            if (left.HitCount != right.HitCount)
                return false;

            if (left.Left != right.Left)
                return false;

            if (left.Operator == right.Operator)
            {
                if (left.Right == right.Right)
                {
                    merged = left;
                    return true;
                }

                return false;
            }

            if (left.Right.Type != FieldType.Value || right.Right.Type != FieldType.Value)
                return false;

            bool useRight = false, useLeft = false;
            RequirementOperator newOperator = RequirementOperator.None;
            switch (left.Operator)
            {
                case RequirementOperator.Equal:
                    switch (right.Operator)
                    {
                        case RequirementOperator.GreaterThan:
                            if (right.Right.Value == left.Right.Value)
                                newOperator = RequirementOperator.GreaterThanOrEqual;
                            else if (right.Right.Value < left.Right.Value)
                                useRight = true;
                            break;

                        case RequirementOperator.GreaterThanOrEqual:
                            useRight = right.Right.Value <= left.Right.Value;
                            break;

                        case RequirementOperator.LessThan:
                            if (right.Right.Value == left.Right.Value)
                                newOperator = RequirementOperator.LessThanOrEqual;
                            else if (right.Right.Value > left.Right.Value)
                                useRight = right.Right.Value > left.Right.Value;
                            break;

                        case RequirementOperator.LessThanOrEqual:
                            useRight = right.Right.Value >= left.Right.Value;
                            break;

                        case RequirementOperator.NotEqual:
                            if (right.Right.Value != left.Right.Value)
                                useRight = true;
                            break;
                    }
                    break;

                case RequirementOperator.GreaterThan:
                    switch (right.Operator)
                    {
                        case RequirementOperator.GreaterThan:
                            useRight = right.Right.Value < left.Right.Value;
                            break;

                        case RequirementOperator.GreaterThanOrEqual:
                            useRight = right.Right.Value <= left.Right.Value;
                            break;

                        case RequirementOperator.Equal:
                            if (right.Right.Value == left.Right.Value)
                                newOperator = RequirementOperator.GreaterThanOrEqual;
                            else if (right.Right.Value > left.Right.Value)
                                useLeft = true;
                            break;
                    }
                    break;

                case RequirementOperator.GreaterThanOrEqual:
                    switch (right.Operator)
                    {
                        case RequirementOperator.GreaterThan:
                            if (right.Right.Value == left.Right.Value)
                                useLeft = true;
                            else
                                useRight = right.Right.Value < left.Right.Value;
                            break;

                        case RequirementOperator.GreaterThanOrEqual:
                            useRight = right.Right.Value <= left.Right.Value;
                            break;
                            
                        case RequirementOperator.Equal:
                            useLeft = right.Right.Value >= left.Right.Value;
                            break;
                    }
                    break;

                case RequirementOperator.LessThan:
                    switch (right.Operator)
                    {
                        case RequirementOperator.LessThan:
                            useRight = right.Right.Value > left.Right.Value;
                            break;

                        case RequirementOperator.LessThanOrEqual:
                            useRight = right.Right.Value >= left.Right.Value;
                            break;

                        case RequirementOperator.Equal:
                            if (right.Right.Value == left.Right.Value)
                                newOperator = RequirementOperator.LessThanOrEqual;
                            else if (right.Right.Value < left.Right.Value)
                                useLeft = true;
                            break;
                    }
                    break;

                case RequirementOperator.LessThanOrEqual:
                    switch (right.Operator)
                    {
                        case RequirementOperator.LessThan:
                            if (right.Right.Value == left.Right.Value)
                                useLeft = true;
                            else
                                useRight = right.Right.Value > left.Right.Value;
                            break;

                        case RequirementOperator.LessThanOrEqual:
                            useRight = right.Right.Value >= left.Right.Value;
                            break;

                        case RequirementOperator.Equal:
                            useLeft = right.Right.Value <= left.Right.Value;
                            break;
                    }
                    break;

                case RequirementOperator.NotEqual:
                    switch (right.Operator)
                    {
                        case RequirementOperator.Equal:
                            if (right.Right.Value != left.Right.Value)
                                useRight = true;
                            break;
                    }
                    break;
            }

            if (condition == ConditionalOperation.Or)
            {
                if (useRight)
                {
                    merged = right;
                    return true;
                }

                if (useLeft)
                {
                    merged = left;
                    return true;
                }

                if (newOperator != RequirementOperator.None)
                {
                    merged = new Requirement { Left = left.Left, Right = left.Right, HitCount = left.HitCount, Operator = newOperator, Type = left.Type };
                    return true;
                }
            }
            else
            {
                if (useRight)
                {
                    merged = left;
                    return true;
                }

                if (useLeft)
                {
                    merged = right;
                    return true;
                }

                if (newOperator != RequirementOperator.None)
                {
                    merged = new Requirement { Left = left.Left, Right = left.Right, HitCount = left.HitCount, Operator = RequirementOperator.Equal, Type = left.Type };
                    return true;
                }
            }

            return false;
        }

        private void MergeDuplicateAlts()
        {
            for (int i = _alts.Count - 1; i > 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (_alts[i].Count != _alts[j].Count)
                        continue;

                    bool[] matches = new bool[_alts[i].Count];
                    Requirement[] merged = new Requirement[_alts[i].Count];
                    for (int k = 0; k < matches.Length; k++)
                    {
                        bool matched = false;
                        for (int l = 0; l < matches.Length; l++)
                        {
                            if (matches[l])
                                continue;

                            if (MergeRequirements(_alts[i][k], _alts[j][l], ConditionalOperation.Or, out merged[k]))
                            {
                                matched = true;
                                matches[l] = true;
                                break;
                            }
                        }

                        if (!matched)
                            break;
                    }

                    if (matches.All(m => m == true))
                    {
                        _alts[j].Clear();
                        _alts[j].AddRange(merged);
                        _alts.RemoveAt(i);
                        break;
                    }
                }
            }

            if (_alts.Count == 1)
            {
                _core.AddRange(_alts[0]);
                _alts.Clear();
            }
        }

        private void PromoteCommonAltsToCore()
        {
            // first pass, ignore PauseIfs
            var requirementsFoundInAll = new List<Requirement>();
            foreach (var requirement in _alts[0])
            {
                bool foundInAll = true;
                for (int i = 1; i < _alts.Count; i++)
                {
                    if (!_alts[i].Any(a => a == requirement))
                    {
                        foundInAll = false;
                        break;
                    }
                }

                if (foundInAll)
                    requirementsFoundInAll.Add(requirement);
            }

            foreach (var requirement in requirementsFoundInAll)
            {
                // ResetIf and PauseIf can only be promoted if all HitCounts are also promoted
                if (requirement.Type == RequirementType.ResetIf || requirement.Type == RequirementType.PauseIf)
                {
                    bool canPromote = false;

                    foreach (var alt in _alts)
                    {
                        for (int i = alt.Count - 1; i >= 0; i--)
                        {
                            if (alt[i].HitCount > 0 && !requirementsFoundInAll.Contains(alt[i]))
                            {
                                canPromote = false;
                                break;
                            }
                        }

                        if (!canPromote)
                            break;
                    }

                    if (!canPromote)
                        continue;
                }

                foreach (var alt in _alts)
                {
                    for (int i = alt.Count - 1; i >= 0; i--)
                    {
                        if (alt[i] == requirement)
                            alt.RemoveAt(i);
                    }
                }

                _core.Add(requirement);
            }
        }

        private void RemoveDuplicates(List<Requirement> requirements)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                for (int j = requirements.Count - 1; j > i; j--)
                {
                    if (requirements[j] == requirements[i])
                        requirements.RemoveAt(j);
                }
            }
        }

        private void RemoveAltsAlreadyInCore(List<Requirement> requirements)
        {
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                if (_core.Any(r => r == requirements[i]))
                {
                    requirements.RemoveAt(i);
                    continue;
                }
            }
        }

        private static void RemoveRedundancies(List<Requirement> requirements)
        {
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                var requirement = requirements[i];
                if (requirement.Right.Type != FieldType.Value)
                    continue;

                for (int j = 0; j < i; j++)
                {
                    Requirement merged;
                    if (MergeRequirements(requirement, requirements[j], ConditionalOperation.And, out merged))
                    {
                        requirements[j] = merged;
                        requirements.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private static bool IsMergable(Requirement requirement)
        {
            if (requirement.Operator != RequirementOperator.Equal)
                return false;
            if (requirement.Left.Type != FieldType.MemoryAddress)
                return false;
            if (requirement.Right.Type != FieldType.Value)
                return false;
            if (requirement.HitCount != 0)
                return false;
            if (requirement.Type != RequirementType.None)
                return false;

            return true;
        }

        private void MergeBits(List<Requirement> requirements)
        {
            var references = new TinyDictionary<uint, int>();
            foreach (var requirement in requirements)
            {
                if (!IsMergable(requirement))
                    continue;

                int flags;
                references.TryGetValue(requirement.Left.Value, out flags);
                switch (requirement.Left.Size)
                {
                    case FieldSize.Bit0:
                        flags |= 0x01;
                        if (requirement.Right.Value != 0)
                            flags |= 0x0100;
                        break;
                    case FieldSize.Bit1:
                        flags |= 0x02;
                        if (requirement.Right.Value != 0)
                            flags |= 0x0200;
                        break;
                    case FieldSize.Bit2:
                        flags |= 0x04;
                        if (requirement.Right.Value != 0)
                            flags |= 0x0400;
                        break;
                    case FieldSize.Bit3:
                        flags |= 0x08;
                        if (requirement.Right.Value != 0)
                            flags |= 0x0800;
                        break;
                    case FieldSize.Bit4:
                        flags |= 0x10;
                        if (requirement.Right.Value != 0)
                            flags |= 0x1000;
                        break;
                    case FieldSize.Bit5:
                        flags |= 0x20;
                        if (requirement.Right.Value != 0)
                            flags |= 0x2000;
                        break;
                    case FieldSize.Bit6:
                        flags |= 0x40;
                        if (requirement.Right.Value != 0)
                            flags |= 0x4000;
                        break;
                    case FieldSize.Bit7:
                        flags |= 0x80;
                        if (requirement.Right.Value != 0)
                            flags |= 0x8000;
                        break;
                    case FieldSize.LowNibble:
                        flags |= 0x0F;
                        flags |= (ushort)(requirement.Right.Value & 0x0F) << 8;
                        break;
                    case FieldSize.HighNibble:
                        flags |= 0xF0;
                        flags |= (ushort)(requirement.Right.Value & 0x0F) << 12;
                        break;
                    case FieldSize.Byte:
                        flags |= 0xFF;
                        flags |= (ushort)(requirement.Right.Value & 0xFF) << 8;
                        break;
                }

                references[requirement.Left.Value] = flags;
            }

            foreach (var kvp in references)
            {
                if ((kvp.Value & 0xFF) == 0xFF)
                {
                    MergeBits(requirements, kvp.Key, FieldSize.Byte, (kvp.Value >> 8) & 0xFF);
                }
                else
                {
                    if ((kvp.Value & 0x0F) == 0x0F)
                        MergeBits(requirements, kvp.Key, FieldSize.LowNibble, (kvp.Value >> 8) & 0x0F);
                    if ((kvp.Value & 0xF0) == 0xF0)
                        MergeBits(requirements, kvp.Key, FieldSize.HighNibble, (kvp.Value >> 12) & 0x0F);
                }
            }
        }

        private static void MergeBits(List<Requirement> requirements, uint address, FieldSize newSize, int newValue)
        {
            bool insert = true;
            int insertAt = 0;
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                if (!IsMergable(requirements[i]))
                    continue;

                var requirement = requirements[i];
                if (requirement.Left.Value != address)
                    continue;

                if (requirement.Left.Size == newSize)
                {
                    if (requirement.Right.Value != newValue)
                        requirement.Right = new Field { Size = newSize, Type = FieldType.Value, Value = (uint)newValue };

                    insert = false;
                    continue;
                }

                bool delete = false;
                switch (newSize)
                {
                    case FieldSize.Byte:
                        switch (requirement.Left.Size)
                        {
                            case FieldSize.Bit0:
                            case FieldSize.Bit1:
                            case FieldSize.Bit2:
                            case FieldSize.Bit3:
                            case FieldSize.Bit4:
                            case FieldSize.Bit5:
                            case FieldSize.Bit6:
                            case FieldSize.Bit7:
                            case FieldSize.LowNibble:
                            case FieldSize.HighNibble:
                                delete = true;
                                break;
                        }
                        break;

                    case FieldSize.LowNibble:
                        switch (requirement.Left.Size)
                        {
                            case FieldSize.Bit0:
                            case FieldSize.Bit1:
                            case FieldSize.Bit2:
                            case FieldSize.Bit3:
                                delete = true;
                                break;
                        }
                        break;

                    case FieldSize.HighNibble:
                        switch (requirement.Left.Size)
                        {
                            case FieldSize.Bit4:
                            case FieldSize.Bit5:
                            case FieldSize.Bit6:
                            case FieldSize.Bit7:
                                delete = true;
                                break;
                        }
                        break;
                }

                if (delete)
                {
                    requirements.RemoveAt(i);
                    insertAt = i;
                    continue;
                }
            }

            if (insert)
            {
                var requirement = new Requirement();
                requirement.Left = new Field { Size = newSize, Type = FieldType.MemoryAddress, Value = address };
                requirement.Operator = RequirementOperator.Equal;
                requirement.Right = new Field { Size = newSize, Type = FieldType.Value, Value = (uint)newValue };
                requirements.Insert(insertAt, requirement);
            }
        }

        public string Optimize()
        {
            // normalize BitX() methods to compare against 1
            NormalizeComparisons(_core);
            if (_core.Count == 0)
                return "No core requirements";

            for (int i = _alts.Count - 1; i >= 0; i--)
            {
                NormalizeComparisons(_alts[i]);
                if (_alts[i].Count == 0)
                    _alts.RemoveAt(i);
            }
            if (_alts.Count == 1)
            {
                _core.AddRange(_alts[0]);
                _alts.Clear();
            }

            // remove duplicates within a set of requirements
            RemoveDuplicates(_core);
            foreach (var alt in _alts)
            {
                RemoveDuplicates(alt);
                RemoveAltsAlreadyInCore(alt);
            }

            // remove redundancies (i > 3 && i > 5) => (i > 5)
            RemoveRedundancies(_core);
            foreach (var alt in _alts)
                RemoveRedundancies(alt);

            // bit1(x) && bit2(x) && bit3(x) && bit4(x) => low4(x)
            MergeBits(_core);
            foreach (var alt in _alts)
                MergeBits(alt);

            // merge duplicate alts
            MergeDuplicateAlts();

            // identify any item common to all alts and promote it to core
            if (_alts.Count > 1)
                PromoteCommonAltsToCore();

            bool hasHitCount = false;
            bool hasReset = false;
            bool hasPause = false;
            foreach (var requirement in _core)
            {
                if (requirement.HitCount > 0)
                    hasHitCount = true;
                if (requirement.Type == RequirementType.ResetIf)
                    hasReset = true;
                if (requirement.Type == RequirementType.PauseIf)
                    hasPause = true;
            }

            if (!hasHitCount)
            {
                foreach (var alt in _alts)
                {
                    foreach (var requirement in alt)
                    {
                        if (requirement.HitCount > 0)
                            hasHitCount = true;
                    }
                }

                if (!hasHitCount)
                {
                    if (hasReset && Id == 0)
                        return "Reset condition without HitCount";
                }
            }

            return null;
        }
    }
}
