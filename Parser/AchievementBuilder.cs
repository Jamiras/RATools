using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    [DebuggerDisplay("{Title} ({Points}) Core:{_core.Count} Alts:{_alts.Count}")]
    public class AchievementBuilder
    {
        public AchievementBuilder()
        {
            Title = String.Empty;
            Description = String.Empty;
            BadgeName = String.Empty;

            _core = new List<Requirement>();
            _alts = new List<ICollection<Requirement>>();
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
        }

        /// <summary>
        /// Gets or sets the achievement title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the achievement description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the number of points that the achievement is worth.
        /// </summary>
        public int Points { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the achievement.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the badge for the achievement.
        /// </summary>
        public string BadgeName { get; set; }

        /// <summary>
        /// Gets the core requirements collection.
        /// </summary>
        public ICollection<Requirement> CoreRequirements
        {
            get { return _core; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Requirement> _core;

        /// <summary>
        /// Gets the alt requirement group collections.
        /// </summary>
        public ICollection<ICollection<Requirement>> AlternateRequirements
        {
            get { return _alts; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<ICollection<Requirement>> _alts;

        /// <summary>
        /// Constructs an <see cref="Achievement"/> from the current state of the builder.
        /// </summary>
        public Achievement ToAchievement()
        {
            var achievement = new Achievement { Title = Title, Description = Description, Points = Points, CoreRequirements = _core.ToArray(), Id = Id, BadgeName = BadgeName };
            var alts = new Requirement[_alts.Count][];
            for (int i = 0; i < _alts.Count; i++)
                alts[i] = _alts[i].ToArray();
            achievement.AlternateRequirements = alts;
            return achievement;
        }

        /// <summary>
        /// Populates the core and alt requirements from a serialized requirement string.
        /// </summary>
        public void ParseRequirements(Tokenizer tokenizer)
        {
            var current = _core;
            do
            {
                var requirement = new Requirement();

                if (tokenizer.Match("R:"))
                    requirement.Type = RequirementType.ResetIf;
                else if (tokenizer.Match("P:"))
                    requirement.Type = RequirementType.PauseIf;
                else if (tokenizer.Match("A:"))
                    requirement.Type = RequirementType.AddSource;
                else if (tokenizer.Match("B:"))
                    requirement.Type = RequirementType.SubSource;
                else if (tokenizer.Match("C:"))
                    requirement.Type = RequirementType.AddHits;

                requirement.Left = Field.Deserialize(tokenizer);

                requirement.Operator = ReadOperator(tokenizer);
                requirement.Right = Field.Deserialize(tokenizer);

                switch (requirement.Type)
                {
                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                        requirement.Operator = RequirementOperator.None;
                        requirement.Right = new Field();
                        break;

                    default:
                        if (requirement.Right.Size == FieldSize.None)
                            requirement.Right = new Field { Type = requirement.Right.Type, Size = requirement.Left.Size, Value = requirement.Right.Value };
                        break;
                }

                current.Add(requirement);

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
                        if (ReferenceEquals(current, _core) || current.Count != 0)
                        {
                            current = new List<Requirement>();
                            _alts.Add(current);
                        }
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

        /// <summary>
        /// Creates a serialized requirements string from the core and alt groups.
        /// </summary>
        public string SerializeRequirements()
        {
            return SerializeRequirements(_core, _alts);
        }

        /// <summary>
        /// Creates a serialized requirements string from the core and alt groups of a provided <see cref="Achievement"/>.
        /// </summary>
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

            if (builder.Length > 0)
                builder.Length--; // remove last _

            foreach (IEnumerable<Requirement> alt in alts)
            {
                if (builder.Length > 0)
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
            switch (requirement.Type)
            {
                case RequirementType.ResetIf: builder.Append("R:"); break;
                case RequirementType.PauseIf: builder.Append("P:"); break;
                case RequirementType.AddSource: builder.Append("A:"); break;
                case RequirementType.SubSource: builder.Append("B:"); break;
                case RequirementType.AddHits: builder.Append("C:"); break;
            }

            requirement.Left.Serialize(builder);

            switch (requirement.Type)
            {
                case RequirementType.AddSource:
                case RequirementType.SubSource:
                    builder.Append("=0");
                    break;

                default:
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
                    break;
            }


            if (requirement.HitCount > 0)
            {
                builder.Append('.');
                builder.Append(requirement.HitCount);
                builder.Append('.');
            }
        }

        static void AppendDebugStringGroup(StringBuilder builder, ICollection<Requirement> group)
        {
            bool needsAmpersand = false;

            var enumerator = group.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var requirement = enumerator.Current;

                var addSources = new StringBuilder();
                var subSources = new StringBuilder();
                var addHits = new StringBuilder();
                bool isCombining = true;
                do
                {
                    switch (requirement.Type)
                    {
                        case RequirementType.AddSource:
                            requirement.Left.AppendString(addSources, NumberFormat.Decimal);
                            addSources.Append(" + ");
                            break;

                        case RequirementType.SubSource:
                            subSources.Append(" - ");
                            requirement.Left.AppendString(subSources, NumberFormat.Decimal);
                            break;

                        case RequirementType.AddHits:
                            requirement.AppendString(addHits, NumberFormat.Decimal);
                            addHits.Append(" || ");
                            break;

                        default:
                            isCombining = false;
                            break;
                    }

                    if (!isCombining)
                        break;

                    if (!enumerator.MoveNext())
                        return;

                    requirement = enumerator.Current;
                } while (true);

                var definition = new StringBuilder();
                requirement.AppendString(definition, NumberFormat.Decimal,
                    addSources.Length > 0 ? addSources.ToString() : null,
                    subSources.Length > 0 ? subSources.ToString() : null,
                    addHits.Length > 0 ? addHits.ToString() : null);

                if (needsAmpersand)
                    builder.Append(" && ");
                else
                    needsAmpersand = true;

                builder.Append(definition.ToString());
            }
        }

        /// <summary>
        /// Gets the requirements formatted as a human-readable string.
        /// </summary>
        internal string RequirementsDebugString
        {
            get
            {
                var builder = new StringBuilder();
                AppendDebugStringGroup(builder, CoreRequirements);

                if (AlternateRequirements.Count > 0)
                {
                    if (CoreRequirements.Count > 0)
                        builder.Append(" && (");

                    foreach (var altGroup in AlternateRequirements)
                    {
                        if (altGroup.Count > 1)
                            builder.Append('(');

                        AppendDebugStringGroup(builder, altGroup);

                        if (altGroup.Count > 1)
                            builder.Append(')');

                        builder.Append(" || ");
                    }

                    builder.Length -= 4;

                    if (CoreRequirements.Count > 0)
                        builder.Append(')');
                }

                return builder.ToString();
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

        // ==== Optimize helpers ====

        private static void NormalizeComparisons(ICollection<Requirement> requirements)
        {
            var alwaysTrue = new List<Requirement>();
            var alwaysFalse = new List<Requirement>();

            bool isAddSource = false;
            foreach (var requirement in requirements)
            {
                if (requirement.Type == RequirementType.AddSource || requirement.Type == RequirementType.SubSource)
                {
                    isAddSource = true;
                    continue;
                }

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
                else if (requirement.Right.Value == 1 && requirement.Operator == RequirementOperator.LessThan)
                {
                    // n < 1 -> n == 0
                    requirement.Operator = RequirementOperator.Equal;
                    requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 0 };
                    continue;
                }

                uint max;

                if (isAddSource)
                {
                    max = uint.MaxValue;
                    isAddSource = false;
                }
                else
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
                            max = 1;

                            if (requirement.Right.Value == 0)
                            {
                                switch (requirement.Operator)
                                {
                                    case RequirementOperator.NotEqual: // bit != 0 -> bit == 1
                                    case RequirementOperator.GreaterThan: // bit > 0 -> bit == 1
                                        requirement.Operator = RequirementOperator.Equal;
                                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 1 };
                                        continue;
                                }
                            }
                            else
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
                }

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

        private static bool HasHitCount(IEnumerable<Requirement> requirements)
        {
            foreach (var requirement in requirements)
            {
                if (requirement.HitCount > 0)
                    return true;
            }

            return false;
        }

        private bool HasHitCount()
        {
            if (HasHitCount(_core))
                return true;

            foreach (var alt in _alts)
            {
                if (HasHitCount(alt))
                    return true;
            }

            return false;
        }

        private void NormalizeNonHitCountResetAndPauseIfs()
        {
            // if this is a dumped achievement, don't convert these, it just makes the diff hard to read.
            if (Id != 0)
                return;

            // a ResetIf condition in an achievement without any HitCount conditions is the same as a non-ResetIf 
            // condition for the opposite comparison (i.e. ResetIf val = 0 is the same as val != 0). Some developers 
            // use ResetIf conditions to keep the HitCount counter at 0 until the achievement is activated. This is 
            // a bad practice, as it makes the achievement harder to read, and it normally adds an additional 
            // condition to be evaluated every frame.

            // a PauseIf condition in an achievement without any HitCount conditions prevents the achievement from
            // triggering while the PauseIf condition is true. Conversely, the achievement can only trigger if the 
            // condition is false, so invert the logic on the condition and make it a requirement.

            // if no hit counts are found, then invert any PauseIfs or ResetIfs.
            if (!HasHitCount())
            {
                NormalizeNonHitCountResetAndPauseIfs(_core);
                foreach (var alt in _alts)
                    NormalizeNonHitCountResetAndPauseIfs(alt);
            }
        }

        private static void NormalizeNonHitCountResetAndPauseIfs(ICollection<Requirement> requirements)
        {
            foreach (var requirement in requirements)
            {
                if (requirement.Type == RequirementType.PauseIf || requirement.Type == RequirementType.ResetIf)
                {
                    requirement.Type = RequirementType.None;
                    requirement.Operator = Requirement.GetOpposingOperator(requirement.Operator);
                }
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
            }

            if (left.Right.Type != FieldType.Value || right.Right.Type != FieldType.Value)
                return false;

            bool useRight = false, useLeft = false, conflicting = false;
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

                        case RequirementOperator.Equal:
                            if (right.Right.Value == left.Right.Value)
                                useRight = true;
                            else
                                conflicting = (condition == ConditionalOperation.And);
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

                        case RequirementOperator.LessThan:
                            if (right.Right.Value == left.Right.Value && condition == ConditionalOperation.Or)
                                newOperator = RequirementOperator.NotEqual;
                            else if (right.Right.Value <= left.Right.Value)
                                conflicting = (condition == ConditionalOperation.And);
                            break;

                        case RequirementOperator.LessThanOrEqual:
                            if (right.Right.Value < left.Right.Value)
                                conflicting = (condition == ConditionalOperation.And);
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

                        case RequirementOperator.LessThan:
                            if (right.Right.Value <= left.Right.Value)
                                conflicting = (condition == ConditionalOperation.And);
                            break;

                        case RequirementOperator.LessThanOrEqual:
                            if (right.Right.Value == left.Right.Value && condition == ConditionalOperation.And)
                                newOperator = RequirementOperator.Equal;
                            else if (right.Right.Value < left.Right.Value)
                                conflicting = (condition == ConditionalOperation.And);
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

                        case RequirementOperator.GreaterThan:
                            if (right.Right.Value == left.Right.Value && condition == ConditionalOperation.Or)
                                newOperator = RequirementOperator.NotEqual;
                            else if (right.Right.Value >= left.Right.Value)
                                conflicting = (condition == ConditionalOperation.And);
                            break;

                        case RequirementOperator.GreaterThanOrEqual:
                            if (right.Right.Value > left.Right.Value)
                                conflicting = (condition == ConditionalOperation.And);
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

                        case RequirementOperator.GreaterThan:
                            if (right.Right.Value >= left.Right.Value)
                                conflicting = (condition == ConditionalOperation.And);
                            break;

                        case RequirementOperator.GreaterThanOrEqual:
                            if (right.Right.Value == left.Right.Value && condition == ConditionalOperation.And)
                                newOperator = RequirementOperator.Equal;
                            else if (right.Right.Value > left.Right.Value)
                                conflicting = (condition == ConditionalOperation.And);
                            break;
                    }
                    break;

                case RequirementOperator.NotEqual:
                    switch (right.Operator)
                    {
                        case RequirementOperator.Equal:
                            if (right.Right.Value != left.Right.Value)
                                useLeft = true;
                            break;
                    }
                    break;
            }

            if (conflicting)
            {
                if (left.HitCount > 0 || right.HitCount > 0)
                    return false;
                if (left.Type == RequirementType.PauseIf || right.Type == RequirementType.PauseIf)
                    return false;
                if (left.Type == RequirementType.ResetIf || right.Type == RequirementType.ResetIf)
                    return false;

                merged = null;
                return true;
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

        static bool Evaluate(Requirement requirement)
        {
            switch (requirement.Operator)
            {
                case RequirementOperator.Equal:
                    return (requirement.Left.Value == requirement.Right.Value);
                case RequirementOperator.NotEqual:
                    return (requirement.Left.Value != requirement.Right.Value);
                case RequirementOperator.LessThan:
                    return (requirement.Left.Value <= requirement.Right.Value);
                case RequirementOperator.LessThanOrEqual:
                    return (requirement.Left.Value < requirement.Right.Value);
                case RequirementOperator.GreaterThan:
                    return (requirement.Left.Value > requirement.Right.Value);
                case RequirementOperator.GreaterThanOrEqual:
                    return (requirement.Left.Value >= requirement.Right.Value);
                default:
                    return false;
            }
        }

        static bool IsTrue(Requirement requirement)
        {
            if (requirement.Left.Type == FieldType.Value && requirement.Right.Type == FieldType.Value)
                return Evaluate(requirement);

            return false;
        }

        static bool IsFalse(Requirement requirement)
        {
            if (requirement.Left.Type == FieldType.Value && requirement.Right.Type == FieldType.Value)
                return !Evaluate(requirement);

            return false;
        }

        private void MergeDuplicateAlts()
        {
            for (int i = _alts.Count - 1; i > 0; i--)
            {
                var altsI = (IList<Requirement>)_alts[i];

                for (int j = i - 1; j >= 0; j--)
                {
                    var altsJ = (IList<Requirement>)_alts[j];

                    if (altsI.Count != altsJ.Count)
                        continue;

                    bool[] matches = new bool[altsI.Count];
                    Requirement[] merged = new Requirement[altsI.Count];
                    for (int k = 0; k < matches.Length; k++)
                    {
                        bool matched = false;
                        for (int l = 0; l < matches.Length; l++)
                        {
                            if (matches[l])
                                continue;

                            if (MergeRequirements(altsI[k], altsJ[l], ConditionalOperation.Or, out merged[k]))
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
                        altsJ.Clear();
                        foreach (var requirement in merged)
                            altsJ.Add(requirement);
                        _alts.RemoveAt(i);
                        break;
                    }
                }
            }

            if (_alts.Count > 2)
            {
                bool hasAlwaysTrue = false;

                // if a trigger contains an always_false alt group, remove it unless it would promote another alt group to core. it's
                // typically used for two cases: building an alt group list or keeping a PauseIf out of core
                for (int j = _alts.Count - 1; j >= 0; j--)
                {
                    if (_alts[j].Count == 1)
                    {
                        if (IsFalse(_alts[j].First()))
                        {
                            _alts.RemoveAt(j);
                            if (_alts.Count == 2)
                                break;
                        }
                        else if (IsTrue(_alts[j].First()))
                        {
                            hasAlwaysTrue = true;
                        }
                    }
                }

                // if a trigger contains an always_true alt group, remove any other alt groups that don't have PauseIf or ResetIf conditions as they are unimportant
                if (hasAlwaysTrue)
                {
                    for (int j = _alts.Count - 1; j >= 0; j--)
                    {
                        if (_alts[j].Count == 1 && IsTrue(_alts[j].First()))
                            continue;

                        bool hasPauseIf = _alts[j].Any(r => r.Type == RequirementType.PauseIf);
                        bool hasResetIf = _alts[j].Any(r => r.Type == RequirementType.ResetIf);
                        if (!hasPauseIf && !hasResetIf)
                            _alts.RemoveAt(j);
                    }

                    if (_alts.Count == 1)
                    {
                        // only AlwaysTrue group left, get rid of it
                        _alts.Clear();
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
            // identify requirements present in all alt groups.
            bool combiningRequirement = false;
            var requirementsFoundInAll = new List<Requirement>();
            foreach (var requirement in _alts[0])
            {
                switch (requirement.Type)
                {
                    case RequirementType.AddHits:
                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                        combiningRequirement = true;
                        continue;

                    default:
                        if (combiningRequirement)
                        {
                            combiningRequirement = false;
                            continue;
                        }
                        break;
                }

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
                // PauseIf only affects the alt group that it's in, so it can only be promoted if all 
                // the HitCounts in the alt group are also promoted
                if (requirement.Type == RequirementType.PauseIf)
                {
                    bool canPromote = false;

                    foreach (IList<Requirement> alt in _alts)
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

                // ResetIf in an alt group may be disabled by a PauseIf, don't promote if any PauseIfs 
                // are not promoted
                if (requirement.Type == RequirementType.ResetIf)
                {
                    bool canPromote = true;

                    foreach (IList<Requirement> alt in _alts)
                    {
                        for (int i = alt.Count - 1; i >= 0; i--)
                        {
                            if (alt[i].Type == RequirementType.PauseIf && !requirementsFoundInAll.Contains(alt[i]))
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

                // remove the requirement from each alt group
                foreach (IList<Requirement> alt in _alts)
                {
                    for (int i = alt.Count - 1; i >= 0; i--)
                    {
                        if (alt[i] == requirement)
                            alt.RemoveAt(i);
                    }
                }

                // and put it in the core group
                _core.Add(requirement);
            }
        }

        private void RemoveDuplicates(IList<Requirement> requirements)
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

        private void RemoveAltsAlreadyInCore(IList<Requirement> requirements)
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

        private static bool IsMultiClause(RequirementType type)
        {
            switch (type)
            {
                case RequirementType.AddSource:
                case RequirementType.SubSource:
                case RequirementType.AddHits:
                    return true;

                default:
                    return false;
            }
        }

        private void RemoveRedundancies(IList<Requirement> requirements)
        {
            var multiClauseConditions = new List<int>();
            for (int i = 0; i < requirements.Count; i++)
            {
                if (IsMultiClause(requirements[i].Type))
                {
                    multiClauseConditions.Add(i);
                    if (!multiClauseConditions.Contains(i + 1))
                        multiClauseConditions.Add(i + 1);
                }
            }

            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                if (multiClauseConditions.Contains(i))
                    continue;

                var requirement = requirements[i];

                // if one requirement is "X == N" and another is "ResetIf X != N", they can be merged.
                if (requirement.HitCount == 0 && (requirement.Type == RequirementType.ResetIf || requirement.Type == RequirementType.None))
                {
                    bool merged = false;
                    for (int j = 0; j < i; j++)
                    {
                        if (multiClauseConditions.Contains(j))
                            continue;

                        Requirement compareRequirement = requirements[j];
                        if (requirement.Type == compareRequirement.Type || compareRequirement.HitCount != 0)
                            continue;
                        if (compareRequirement.Type != RequirementType.ResetIf && compareRequirement.Type != RequirementType.None)
                            continue;
                        if (requirement.Left != compareRequirement.Left || requirement.Right != compareRequirement.Right)
                            continue;

                        var opposingOperator = Requirement.GetOpposingOperator(requirement.Operator);
                        if (compareRequirement.Operator == opposingOperator)
                        {
                            // if a HitCount exists, keep the ResetIf, otherwise keep the non-ResetIf
                            bool hasHitCount = HasHitCount();
                            bool isResetIf = (requirement.Type == RequirementType.ResetIf);
                            if (hasHitCount == isResetIf)
                                requirements[j] = requirement;

                            requirements.RemoveAt(i);
                            merged = true;
                            break;
                        }
                    }

                    if (merged)
                        continue;
                }

                // merge overlapping comparisons (a > 3 && a > 4 => a > 4)
                if (requirement.Right.Type == FieldType.Value)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (multiClauseConditions.Contains(j))
                            continue;

                        Requirement merged;
                        if (MergeRequirements(requirement, requirements[j], ConditionalOperation.And, out merged))
                        {
                            if (merged == null)
                            {
                                // conflicting requirements, replace the entire requirement set with an always_false()
                                requirements.Clear();
                                requirements.Add(new Requirement
                                {
                                    Left = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 0 },
                                    Operator = RequirementOperator.Equal,
                                    Right = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 1 },
                                });
                                return;
                            }

                            requirements[j] = merged;
                            requirements.RemoveAt(i);
                            break;
                        }
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

        private void MergeBits(IList<Requirement> requirements)
        {
            var mergableRequirements = new List<Requirement>();

            bool inMultiClause = false;
            var references = new TinyDictionary<uint, int>();
            foreach (var requirement in requirements)
            {
                if (IsMultiClause(requirement.Type))
                {
                    inMultiClause = true;
                    continue;
                }
                else if (inMultiClause)
                {
                    inMultiClause = false;
                    continue;
                }

                if (IsMergable(requirement))
                    mergableRequirements.Add(requirement);
            }

            foreach (var requirement in mergableRequirements)
            {
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
                    MergeBits(requirements, mergableRequirements, kvp.Key, FieldSize.Byte, (kvp.Value >> 8) & 0xFF);
                }
                else
                {
                    if ((kvp.Value & 0x0F) == 0x0F)
                        MergeBits(requirements, mergableRequirements, kvp.Key, FieldSize.LowNibble, (kvp.Value >> 8) & 0x0F);
                    if ((kvp.Value & 0xF0) == 0xF0)
                        MergeBits(requirements, mergableRequirements, kvp.Key, FieldSize.HighNibble, (kvp.Value >> 12) & 0x0F);
                }
            }
        }

        private static void MergeBits(IList<Requirement> requirements, ICollection<Requirement> mergableRequirements, uint address, FieldSize newSize, int newValue)
        {
            bool insert = true;
            int insertAt = 0;
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                var requirement = requirements[i];
                if (!mergableRequirements.Contains(requirement))
                    continue;

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
            {
                if (_alts.Count > 0)
                    return "Ambiguous logic clauses. Please put parentheses around all of the alt group clauses.";

                return "No valid requirements found.";
            }

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

            // convert ResetIfs and PauseIfs without HitCounts to standard requirements
            NormalizeNonHitCountResetAndPauseIfs();

            // remove duplicates within a set of requirements
            RemoveDuplicates(_core);
            foreach (IList<Requirement> alt in _alts)
            {
                RemoveDuplicates(alt);
                RemoveAltsAlreadyInCore(alt);
            }

            // remove redundancies (i > 3 && i > 5) => (i > 5)
            RemoveRedundancies(_core);
            foreach (IList<Requirement> alt in _alts)
                RemoveRedundancies(alt);

            // bit1(x) && bit2(x) && bit3(x) && bit4(x) => low4(x)
            MergeBits(_core);
            foreach (IList<Requirement> alt in _alts)
                MergeBits(alt);

            // merge duplicate alts
            MergeDuplicateAlts();

            // identify any item common to all alts and promote it to core
            if (_alts.Count > 1)
                PromoteCommonAltsToCore();

            // success!
            return null;
        }
    }
}
