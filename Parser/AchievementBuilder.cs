using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RATools.Data;
using Jamiras.Components;

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

                requirement.Left = ReadField(tokenizer);
                requirement.Operator = ReadOperator(tokenizer);
                requirement.Right = ReadField(tokenizer);
                Current.Add(requirement);

                if (tokenizer.NextChar == '.')
                {
                    tokenizer.Advance(); // first period
                    requirement.HitCount = (ushort)ReadNumber(tokenizer);
                    tokenizer.Advance(); // second period
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

        private uint ReadNumber(Tokenizer tokenizer)
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

        private Field ReadField(Tokenizer tokenizer)
        {
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

            return new Field { Size = size, Type = FieldType.MemoryAddress, Value = address };
        }

        private RequirementOperator ReadOperator(Tokenizer tokenizer)
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

        private void NormalizeComparisons(List<Requirement> requirements)
        {
            var alwaysTrue = new List<Requirement>();

            foreach (var requirement in requirements)
            {
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
                        if (requirement.Right.Type == FieldType.Value)
                        {
                            if (requirement.Right.Value == 0)
                            {
                                switch (requirement.Operator)
                                {
                                    case RequirementOperator.NotEqual: // bit != 0 -> bit = 1
                                    case RequirementOperator.GreaterThan: // bit > 0 -> bit = 1
                                        requirement.Operator = RequirementOperator.Equal;
                                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 1 };
                                        break;

                                    case RequirementOperator.GreaterThanOrEqual: // bit >= 0 -> always true
                                        alwaysTrue.Add(requirement);
                                        break;
                                }
                            }
                            else // value is non-zero
                            {
                                if (requirement.Right.Value != 1)
                                    requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = 1 };

                                switch (requirement.Operator)
                                {
                                    case RequirementOperator.NotEqual: // bit != 1 -> bit = 0
                                    case RequirementOperator.LessThan: // bit < 1 -> bit = 0
                                        requirement.Operator = RequirementOperator.Equal;
                                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 0 };
                                        break;

                                    case RequirementOperator.LessThanOrEqual: // bit <= 1 -> always true
                                        alwaysTrue.Add(requirement);
                                        break;
                                }
                            }
                        }

                        break;
                }
            }

            foreach (var requirement in alwaysTrue)
                requirements.Remove(requirement);
        }

        private void PromoteCommonAltsToCore()
        {
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

        private void RemoveRedundancies(List<Requirement> requirements)
        {
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                if (_core.Any(r => r == requirements[i]))
                    requirements.RemoveAt(i);
            }
        }

        public void Optimize()
        {
            // normalize BitX() methods to compare against 1
            NormalizeComparisons(_core);
            foreach (var alt in _alts)
                NormalizeComparisons(alt);

            // remove duplicates
            RemoveDuplicates(_core);
            foreach (var alt in _alts)
            {
                RemoveDuplicates(alt);
                RemoveRedundancies(alt);
            }

            // identify any item common to all alts and promote it to core
            if (_alts.Count > 1)
                PromoteCommonAltsToCore();
        }
    }
}
