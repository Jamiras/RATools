using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
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
            var core = _core.ToArray();

            var achievement = new Achievement { Title = Title, Description = Description, Points = Points, CoreRequirements = core, Id = Id, BadgeName = BadgeName };
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
                if (tokenizer.NextChar != 'S')
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
                    else if (tokenizer.Match("D:"))
                        requirement.Type = RequirementType.SubHits;
                    else if (tokenizer.Match("N:"))
                        requirement.Type = RequirementType.AndNext;
                    else if (tokenizer.Match("O:"))
                        requirement.Type = RequirementType.OrNext;
                    else if (tokenizer.Match("I:"))
                        requirement.Type = RequirementType.AddAddress;
                    else if (tokenizer.Match("M:"))
                        requirement.Type = RequirementType.Measured;
                    else if (tokenizer.Match("G:"))
                        requirement.Type = RequirementType.MeasuredPercent;
                    else if (tokenizer.Match("Q:"))
                        requirement.Type = RequirementType.MeasuredIf;
                    else if (tokenizer.Match("Z:"))
                        requirement.Type = RequirementType.ResetNextIf;
                    else if (tokenizer.Match("T:"))
                        requirement.Type = RequirementType.Trigger;

                    requirement.Left = Field.Deserialize(tokenizer);

                    requirement.Operator = ReadOperator(tokenizer);
                    if (requirement.Operator != RequirementOperator.None)
                        requirement.Right = Field.Deserialize(tokenizer);

                    if (requirement.IsScalable && requirement.IsComparison)
                    {
                        requirement.Operator = RequirementOperator.None;
                        requirement.Right = new Field();
                    }

                    if (tokenizer.NextChar == '.')
                    {
                        tokenizer.Advance(); // first period
                        requirement.HitCount = ReadNumber(tokenizer);
                        tokenizer.Advance(); // second period
                    }
                    else if (tokenizer.NextChar == '(') // old format
                    {
                        tokenizer.Advance(); // '('
                        requirement.HitCount = ReadNumber(tokenizer);
                        tokenizer.Advance(); // ')'
                    }

                    current.Add(requirement);
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

                case '*':
                    tokenizer.Advance();
                    return RequirementOperator.Multiply;

                case '/':
                    tokenizer.Advance();
                    return RequirementOperator.Divide;

                case '&':
                    tokenizer.Advance();
                    return RequirementOperator.LogicalAnd;
            }

            return RequirementOperator.None;
        }

        public static string GetMinimumVersion(Achievement achievement)
        {
            int minimumVersion = MinimumVersion(achievement.CoreRequirements);
            foreach (var group in achievement.AlternateRequirements)
            {
                int altMinimumVersion = MinimumVersion(group);
                if (altMinimumVersion > minimumVersion)
                    minimumVersion = altMinimumVersion;
            }

            return String.Format("0.{0}", minimumVersion);
        }

        private static int MinimumVersion(IEnumerable<Requirement> requirements)
        {
            int minVer = 0;

            foreach (var requirement in requirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.AndNext:
                        // 0.76 21 Jun 2019
                        if (minVer < 76)
                            minVer = 76;
                        break;

                    case RequirementType.AddAddress:
                    case RequirementType.Measured:
                        // 0.77 30 Nov 2019
                        if (minVer < 77)
                            minVer = 77;
                        break;

                    case RequirementType.MeasuredIf:
                    case RequirementType.OrNext:
                        // 0.78 18 May 2020
                        if (minVer < 78)
                            minVer = 78;
                        break;

                    case RequirementType.ResetNextIf:
                    case RequirementType.Trigger:
                    case RequirementType.SubHits:
                        // 0.79 22 May 2021
                        if (minVer < 79)
                            minVer = 79;
                        break;

                    case RequirementType.MeasuredPercent:
                        // 0.80 TBD
                        if (minVer < 80)
                            minVer = 80;
                        break;

                    default:
                        break;
                }

                switch (requirement.Operator)
                {
                    case RequirementOperator.Multiply:
                    case RequirementOperator.Divide:
                    case RequirementOperator.LogicalAnd:
                        // 0.78 18 May 2020
                        if (minVer < 78)
                            minVer = 78;
                        break;

                    default:
                        break;
                }

                foreach (var type in new FieldType[] { requirement.Left.Type, requirement.Right.Type })
                {
                    switch (type)
                    {
                        case FieldType.PriorValue:
                            // 0.76 21 Jun 2019
                            if (minVer < 76)
                                minVer = 76;
                            break;

                        default:
                            break;
                    }
                }

                foreach (var size in new FieldSize[] { requirement.Left.Size, requirement.Right.Size })
                {
                    switch (size)
                    {
                        case FieldSize.TByte:
                            // 0.77 30 Nov 2019
                            if (minVer < 77)
                                minVer = 77;
                            break;

                        case FieldSize.BitCount:
                            // 0.78 18 May 2020
                            if (minVer < 78)
                                minVer = 78;
                            break;

                        case FieldSize.BigEndianWord:
                        case FieldSize.BigEndianTByte:
                        case FieldSize.BigEndianDWord:
                        case FieldSize.Float:
                        case FieldSize.MBF32:
                            // 0.80 TBD
                            if (minVer < 80)
                                minVer = 80;
                            break;

                        default:
                            break;
                    }
                }
            }

            return minVer;
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

        internal static string SerializeRequirements(IEnumerable<Requirement> core, IEnumerable<IEnumerable<Requirement>> alts)
        {
            var builder = new StringBuilder();

            // if no new features are found, prefer the legacy format for greatest compatibility with older versions of RetroArch
            int minimumVersion = MinimumVersion(core);
            foreach (var group in alts)
            {
                int altMinimumVersion = MinimumVersion(group);
                if (altMinimumVersion > minimumVersion)
                    minimumVersion = altMinimumVersion;
            }

            foreach (Requirement requirement in core)
            {
                SerializeRequirement(requirement, builder, minimumVersion);
                builder.Append('_');
            }

            if (builder.Length > 0)
            {
                builder.Length--; // remove last _
            }
            else if (alts.Any())
            {
                // if core is empty and any alts exist, add an always_true condition to the core for compatibility
                // with legacy RetroArch parsing
                builder.Append("1=1");
            }

            foreach (IEnumerable<Requirement> alt in alts)
            {
                builder.Append('S');

                foreach (Requirement requirement in alt)
                {
                    SerializeRequirement(requirement, builder, minimumVersion);
                    builder.Append('_');
                }

                builder.Length--; // remove last _
            }

            return builder.ToString();
        }

        private static void SerializeRequirement(Requirement requirement, StringBuilder builder, int minimumVersion)
        {
            switch (requirement.Type)
            {
                case RequirementType.ResetIf: builder.Append("R:"); break;
                case RequirementType.PauseIf: builder.Append("P:"); break;
                case RequirementType.AddSource: builder.Append("A:"); break;
                case RequirementType.SubSource: builder.Append("B:"); break;
                case RequirementType.AddHits: builder.Append("C:"); break;
                case RequirementType.SubHits: builder.Append("D:"); break;
                case RequirementType.AndNext: builder.Append("N:"); break;
                case RequirementType.OrNext: builder.Append("O:"); break;
                case RequirementType.Measured: builder.Append("M:"); break;
                case RequirementType.MeasuredPercent: builder.Append("G:"); break;
                case RequirementType.MeasuredIf: builder.Append("Q:"); break;
                case RequirementType.AddAddress: builder.Append("I:"); break;
                case RequirementType.ResetNextIf: builder.Append("Z:"); break;
                case RequirementType.Trigger: builder.Append("T:"); break;
            }

            requirement.Left.Serialize(builder);

            switch (requirement.Type)
            {
                case RequirementType.AddSource:
                case RequirementType.SubSource:
                case RequirementType.AddAddress:
                    switch (requirement.Operator)
                    {
                        case RequirementOperator.Multiply: builder.Append('*'); break;
                        case RequirementOperator.Divide: builder.Append('/'); break;
                        case RequirementOperator.LogicalAnd: builder.Append('&'); break;
                        default:
                            if (minimumVersion < 77)
                                builder.Append("=0");
                            return;
                    }

                    requirement.Right.Serialize(builder);
                    return;

                default:
                    switch (requirement.Operator)
                    {
                        case RequirementOperator.Equal: builder.Append('='); break;
                        case RequirementOperator.NotEqual: builder.Append("!="); break;
                        case RequirementOperator.LessThan: builder.Append('<'); break;
                        case RequirementOperator.LessThanOrEqual: builder.Append("<="); break;
                        case RequirementOperator.GreaterThan: builder.Append('>'); break;
                        case RequirementOperator.GreaterThanOrEqual: builder.Append(">="); break;
                        case RequirementOperator.Multiply: builder.Append('*'); break;
                        case RequirementOperator.Divide: builder.Append('/'); break;
                        case RequirementOperator.LogicalAnd: builder.Append('&'); break;
                        case RequirementOperator.None: return;
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

        public static void AppendStringGroup(StringBuilder builder, IEnumerable<Requirement> requirements,
                                             NumberFormat numberFormat, int wrapWidth = Int32.MaxValue, int indent = 14)
        {
            var groups = RequirementEx.Combine(requirements);

            RequirementEx measured = null;
            RequirementEx measuredIf = null;
            foreach (var group in groups)
            {
                switch (group.Requirements.Last().Type)
                {
                    case RequirementType.Measured:
                    case RequirementType.MeasuredPercent:
                        measured = group;
                        break;

                    case RequirementType.MeasuredIf:
                        measuredIf = group;
                        break;

                    default:
                        break;
                }
            }

            string measuredIfString = null;
            if (measuredIf != null && measured != null)
            {
                // if both a Measured and MeasuredIf exist, merge the MeasuredIf into the Measured group so
                // it can be converted to a 'when' parameter of the measured() call
                groups.Remove(measuredIf);

                var measuredIfBuilder = new StringBuilder();
                measuredIf.AppendString(measuredIfBuilder, numberFormat);

                // remove "measured_if(" and ")" - they're not needed when used as a when clause
                measuredIfBuilder.Length--;
                measuredIfBuilder.Remove(0, 12);

                measuredIfString = measuredIfBuilder.ToString();
            }

            int width = wrapWidth - indent;
            bool needsAmpersand = false;
            foreach (var group in groups)
            {
                if (needsAmpersand)
                {
                    builder.Append(" && ");
                    width -= 4;
                }
                else
                {
                    needsAmpersand = true;
                }

                group.AppendString(builder, numberFormat, ref width, wrapWidth, indent, measuredIfString);
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
                AppendStringGroup(builder, CoreRequirements, NumberFormat.Decimal, Int32.MaxValue);

                if (AlternateRequirements.Count > 0)
                {
                    if (CoreRequirements.Count > 0)
                        builder.Append(" && (");

                    foreach (var altGroup in AlternateRequirements)
                    {
                        if (altGroup.Count > 1)
                            builder.Append('(');

                        AppendStringGroup(builder, altGroup, NumberFormat.Decimal, Int32.MaxValue);

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

        internal static Field CreateFieldFromExpression(ExpressionBase expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.IntegerConstant:
                    return new Field
                    {
                        Size = FieldSize.DWord,
                        Type = FieldType.Value,
                        Value = (uint)((IntegerConstantExpression)expression).Value
                    };

                case ExpressionType.FloatConstant:
                    return new Field
                    {
                        Size = FieldSize.Float,
                        Type = FieldType.Float,
                        Float = ((FloatConstantExpression)expression).Value
                    };

                default:
                    return new Field();
            }
        }

        // ==== Optimize helpers ====

        private static bool? NormalizeComparisonMax(Requirement requirement, uint max)
        {
            if (requirement.Right.Value >= max)
            {
                switch (requirement.Operator)
                {
                    case RequirementOperator.GreaterThan: // n > max -> always false
                        return false;

                    case RequirementOperator.GreaterThanOrEqual: // n >= max -> n == max
                        if (requirement.Right.Value > max)
                            return false;

                        requirement.Operator = RequirementOperator.Equal;
                        break;

                    case RequirementOperator.LessThanOrEqual: // n <= max -> always true
                        return true;

                    case RequirementOperator.LessThan: // n < max -> n != max
                        if (requirement.Right.Value > max)
                            return true;

                        requirement.Operator = RequirementOperator.NotEqual;
                        break;

                    case RequirementOperator.Equal:
                        if (requirement.Right.Value > max)
                            return false;
                        break;

                    case RequirementOperator.NotEqual:
                        if (requirement.Right.Value > max)
                            return true;
                        break;
                }
            }

            return null;
        }

        private static bool? NormalizeLimits(Requirement requirement)
        {
            if (requirement.Right.Type != FieldType.Value)
            {
                // if the comparison is between two memory addresses, we cannot determine the relationship of the values
                if (requirement.Left.Type != FieldType.Value)
                    return null;

                // normalize the comparison so the value is on the right.
                var value = requirement.Left;
                requirement.Left = requirement.Right;
                requirement.Right = value;
                requirement.Operator = Requirement.GetReversedRequirementOperator(requirement.Operator);
            }

            if (requirement.Right.Value == 0)
            {
                switch (requirement.Operator)
                {
                    case RequirementOperator.LessThan: // n < 0 -> never true
                        return false;

                    case RequirementOperator.LessThanOrEqual: // n <= 0 -> n == 0
                        requirement.Operator = RequirementOperator.Equal;
                        break;

                    case RequirementOperator.GreaterThan: // n > 0 -> n != 0
                        requirement.Operator = RequirementOperator.NotEqual;
                        break;

                    case RequirementOperator.GreaterThanOrEqual: // n >= 0 -> always true
                        return true;
                }
            }
            else if (requirement.Right.Value == 1 && requirement.Operator == RequirementOperator.LessThan)
            {
                // n < 1 -> n == 0
                requirement.Operator = RequirementOperator.Equal;
                requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 0 };
                return null;
            }

            uint max = Field.GetMaxValue(requirement.Left.Size);
            if (max == 1)
            {
                if (requirement.Right.Value == 0)
                {
                    switch (requirement.Operator)
                    {
                        case RequirementOperator.NotEqual: // bit != 0 -> bit == 1
                        case RequirementOperator.GreaterThan: // bit > 0 -> bit == 1
                            requirement.Operator = RequirementOperator.Equal;
                            requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 1 };
                            return null;
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
                            return null;
                    }
                }
            }

            var result = NormalizeComparisonMax(requirement, max);
            if (result != null)
                return result;

            if (requirement.Left.Size == FieldSize.BitCount &&
                requirement.Operator == RequirementOperator.Equal &&
                requirement.Right.Type == FieldType.Value)
            {
                if (requirement.Right.Value == 0)
                {
                    // bitcount == 0 is the same as byte == 0
                    requirement.Left = new Field { Size = FieldSize.Byte, Type = requirement.Left.Type, Value = requirement.Left.Value };
                }
                else if (requirement.Right.Value == 8)
                {
                    // bitcount == 8 is the same as byte == 255
                    requirement.Left = new Field { Size = FieldSize.Byte, Type = requirement.Left.Type, Value = requirement.Left.Value };
                    requirement.Right = new Field { Type = FieldType.Value, Value = 255 };
                }
            }

            return null;
        }

        private static uint DecToHex(uint dec)
        {
            uint hex = (dec % 10);
            dec /= 10;
            hex |= (dec % 10) << 4;
            dec /= 10;
            hex |= (dec % 10) << 8;
            dec /= 10;
            hex |= (dec % 10) << 12;
            dec /= 10;
            hex |= (dec % 10) << 16;
            dec /= 10;
            hex |= (dec % 10) << 20;
            dec /= 10;
            hex |= (dec % 10) << 24;
            dec /= 10;
            hex |= (dec % 10) << 28;
            return hex;
        }

        private static bool? NormalizeBCD(Requirement requirement)
        {
            if (requirement.Left.Type == FieldType.BinaryCodedDecimal)
            {
                switch (requirement.Right.Type)
                {
                    case FieldType.BinaryCodedDecimal:
                        /* both sides are being BCD decoded, can skip that step */
                        requirement.Left = new Field { Size = requirement.Left.Size, Type = FieldType.MemoryAddress, Value = requirement.Left.Value };
                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.MemoryAddress, Value = requirement.Right.Value };
                        break;

                    case FieldType.Value:
                        /* prevent overflow calling DecToHex - limits for smaller sizes will be enforced later
                         * because DecToHex will return a value larger than the memory accessor can generate */
                        var result = NormalizeComparisonMax(requirement, 99999999);
                        if (result != null)
                            return result;

                        /* BCD comparison to constant - convert constant to avoid decoding overhead */
                        requirement.Left = new Field { Size = requirement.Left.Size, Type = FieldType.MemoryAddress, Value = requirement.Left.Value };
                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = DecToHex(requirement.Right.Value) };
                        break;

                    default:
                        /* cannot normalize BCD comparison */
                        break;
                }

                /* BCD decode of anything smaller than 4 bits has no effect */
                if (requirement.Left.Type == FieldType.BinaryCodedDecimal && Field.GetMaxValue(requirement.Left.Size) < 16)
                    requirement.Left = new Field { Size = requirement.Left.Size, Type = FieldType.MemoryAddress, Value = requirement.Left.Value };
            }

            /* BCD decode of anything smaller than 4 bits has no effect */
            if (requirement.Right.Type == FieldType.BinaryCodedDecimal && Field.GetMaxValue(requirement.Right.Size) < 16)
                requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.MemoryAddress, Value = requirement.Right.Value };

            return null;
        }

        private static void NormalizeComparisons(IList<RequirementEx> requirements)
        {
            var alwaysTrue = new List<RequirementEx>();
            var alwaysFalse = new List<RequirementEx>();

            foreach (var requirementEx in requirements)
            {
                // if there's modifier conditions, assume it can't be always_true() or always_false()
                if (requirementEx.Requirements.Count > 1)
                    continue;

                var requirement = requirementEx.Requirements[0];

                // see if the requirement can be simplified down to an always_true() or always_false().
                var result = NormalizeBCD(requirement);
                if (result == null)
                    result = requirement.Evaluate();
                if (result == null)
                    result = NormalizeLimits(requirement);

                // any condition with a HitCount greater than 1 cannot be always_true(), so create a copy
                // without the HitCount and double-check.
                if (result == null && requirement.HitCount > 1)
                {
                    var normalizedRequirement = new Requirement
                    {
                        Left = requirement.Left,
                        Operator = requirement.Operator,
                        Right = requirement.Right,
                    };

                    result = normalizedRequirement.Evaluate();
                }

                if (result == true)
                {
                    // replace with simplified logic (keeping any HitCounts or flags)
                    var alwaysTrueRequirement = AlwaysTrueFunction.CreateAlwaysTrueRequirement();
                    requirement.Left = alwaysTrueRequirement.Left;
                    requirement.Operator = alwaysTrueRequirement.Operator;
                    requirement.Right = alwaysTrueRequirement.Right;

                    if (requirement.HitCount <= 1)
                    {
                        if (requirement.Type == RequirementType.PauseIf)
                        {
                            // a PauseIf for a condition that is always true will permanently disable the group.
                            // replace the entire group with an always_false() clause
                            requirementEx.Requirements[0] = AlwaysFalseFunction.CreateAlwaysFalseRequirement();
                            requirements.Clear();
                            requirements.Add(requirementEx);
                            return;
                        }
                        else if (requirement.Type == RequirementType.ResetIf)
                        {
                            // a ResetIf for a condition that is always true will invalidate the trigger (not just the group).
                            // since we can't fully invalidate the trigger from here, replace the entire group with a ResetIf(always_true())
                            requirementEx.Requirements[0] = alwaysTrueRequirement; // this discards HitCounts
                            requirementEx.Requirements[0].Type = RequirementType.ResetIf;
                            requirements.Clear();
                            requirements.Add(requirementEx);
                            return;
                        }

                        alwaysTrue.Add(requirementEx);
                    }
                }
                else if (result == false)
                {
                    // replace with simplified logic (keeping any HitCounts or flags)
                    var alwaysFalseRequirement = AlwaysFalseFunction.CreateAlwaysFalseRequirement();
                    requirement.Left = alwaysFalseRequirement.Left;
                    requirement.Operator = alwaysFalseRequirement.Operator;
                    requirement.Right = alwaysFalseRequirement.Right;

                    // a PauseIf for a condition that can never be true can be eliminated - replace with always_true
                    // a ResetIf for a condition that can never be true can be eliminated - replace with always_true
                    if (requirement.Type == RequirementType.ResetIf || requirement.Type == RequirementType.PauseIf)
                        alwaysTrue.Add(requirementEx);
                    else
                        alwaysFalse.Add(requirementEx);
                }
            }

            if (alwaysFalse.Count > 0)
            {
                // at least one requirement can never be true. replace the all non-PauseIf non-ResetIf conditions 
                // with a single always_false()
                for (int i = requirements.Count - 1; i >= 0; i--)
                {
                    var requirement = requirements[i];
                    switch (requirement.Requirements.Last().Type)
                    {
                        case RequirementType.PauseIf:
                        case RequirementType.ResetIf:
                            if (alwaysFalse.Contains(requirement))
                                requirements.RemoveAt(i);
                            break;

                        default:
                            requirements.RemoveAt(i);
                            break;
                    }
                }

                var requirementEx = new RequirementEx();
                requirementEx.Requirements.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
                requirements.Add(requirementEx);
            }
            else if (alwaysTrue.Count > 0)
            {
                foreach (var requirementEx in alwaysTrue)
                    requirements.Remove(requirementEx);

                if (requirements.Count == 0)
                {
                    var requirementEx = new RequirementEx();
                    requirementEx.Requirements.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                    requirements.Add(requirementEx);
                }
            }
        }

        private static bool HasHitCount(List<List<RequirementEx>> groups)
        {
            foreach (var group in groups)
            {
                foreach (var requirement in group)
                {
                    if (requirement.HasHitCount)
                        return true;
                }
            }

            return false;
        }

        private void NormalizeNonHitCountResetAndPauseIfs(List<List<RequirementEx>> groups, bool hasHitCount)
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

            if (hasHitCount)
                return;

            // if no hit counts are found, then invert any PauseIfs or ResetIfs.
            foreach (var group in groups)
            {
                // PauseIf may be protecting a Measured, if found don't invert PauseIf
                bool hasMeasured = group.Any(g => g.Requirements.Any(r => r.IsMeasured));

                foreach (var requirementEx in group)
                {
                    var requirement = requirementEx.Requirements.Last();
                    if ((requirement.Type == RequirementType.PauseIf && !hasMeasured) ||
                        requirement.Type == RequirementType.ResetIf)
                    {
                        bool hasAndNext = requirementEx.Requirements.Any(r => r.Type == RequirementType.AndNext);
                        bool hasOrNext = requirementEx.Requirements.Any(r => r.Type == RequirementType.OrNext);
                        if (hasAndNext && hasOrNext)
                        {
                            // if both AndNext and OrNext is present, we can't just invert the logic as the order of operations may be affected
                        }
                        else
                        {
                            if (hasAndNext)
                            {
                                // convert an AndNext chain into an OrNext chain
                                foreach (var r in requirementEx.Requirements)
                                {
                                    if (r.Type == RequirementType.AndNext)
                                    {
                                        r.Type = RequirementType.OrNext;
                                        r.Operator = Requirement.GetOpposingOperator(r.Operator);
                                    }
                                }
                            }
                            else if (hasOrNext)
                            {
                                // convert an OrNext chain into an AndNext chain
                                foreach (var r in requirementEx.Requirements)
                                {
                                    if (r.Type == RequirementType.OrNext)
                                    {
                                        r.Type = RequirementType.AndNext;
                                        r.Operator = Requirement.GetOpposingOperator(r.Operator);
                                    }
                                }
                            }

                            requirement.Type = RequirementType.None;
                            requirement.Operator = Requirement.GetOpposingOperator(requirement.Operator);
                        }
                    }
                }
            }
        }

        private static bool MergeRequirements(Requirement left, Requirement right, ConditionalOperation condition, out Requirement merged)
        {
            merged = null;

            if (left.Type != right.Type)
                return false;

            if (left.Left != right.Left)
                return false;

            if (left.Operator == RequirementOperator.None)
            {
                // AddSource, SubSource, and AddAddress don't have a right side or hit counts
                merged = left;
                return true;
            }

            if (left.Operator == right.Operator && left.HitCount == right.HitCount && left.Right == right.Right)
            {
                // 100% match, use it
                merged = left;
                return true;
            }

            // create a copy that we can modify
            merged = new Requirement
            {
                Type = left.Type,
                Left = left.Left,
                Operator = left.Operator,
                Right = left.Right,
                HitCount = left.HitCount
            };

            // merge hit counts
            if (left.HitCount != right.HitCount)
            {
                // cannot merge hit counts if one of them is infinite
                if (left.HitCount == 0 || right.HitCount == 0)
                    return false;

                merged.HitCount = Math.Max(left.HitCount, right.HitCount);

                if (left.Operator == right.Operator && left.Right == right.Right)
                    return true;
            }

            // if either right is not a value field, we can't merge
            if (left.Right.Type != FieldType.Value || right.Right.Type != FieldType.Value)
                return false;

            // both rights are value fields, see if there's overlap in the logic
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

            // check for conflict
            if (conflicting)
            {
                if (left.HitCount > 0 || right.HitCount > 0)
                    return false;
                if (left.Type == RequirementType.PauseIf)
                    return false;
                if (left.Type == RequirementType.ResetIf)
                    return false;

                // conditions conflict with each other, trigger is impossible
                merged = null;
                return true;
            }

            // when processing never() [ResetIf], invert the conditional operation as we expect the
            // conditions to be false when the achievement triggers
            if (left.Type == RequirementType.ResetIf)
            {
                switch (condition)
                {
                    case ConditionalOperation.And:
                        // "never(A) && never(B)" => "never(A || B)"
                        condition = ConditionalOperation.Or;
                        break;
                    case ConditionalOperation.Or:
                        // "never(A) || never(B)" => "never(A && B)"
                        condition = ConditionalOperation.And;
                        break;
                }
            }

            if (condition == ConditionalOperation.Or)
            {
                // "A || B" => keep the less restrictive condition (intersection)
                if (useRight)
                {
                    merged.Operator = right.Operator;
                    merged.Right = right.Right;
                    return true;
                }

                if (useLeft)
                    return true;

                if (newOperator != RequirementOperator.None)
                {
                    merged.Operator = newOperator;
                    return true;
                }
            }
            else
            {
                // "A && B" => keep the more restrictive condition (union)
                if (useRight)
                    return true;

                if (useLeft)
                {
                    merged.Operator = right.Operator;
                    merged.Right = right.Right;
                    return true;
                }

                if (newOperator != RequirementOperator.None)
                {
                    merged.Operator = newOperator;
                    return true;
                }
            }

            return false;
        }

        private static bool MergeRequirements(RequirementEx first, RequirementEx second, ConditionalOperation condition, out RequirementEx merged)
        {
            if (first.Requirements.Count != second.Requirements.Count)
            {
                merged = null;
                return false;
            }

            merged = new RequirementEx();
            for (int i = 0; i < first.Requirements.Count; i++)
            {
                var left = first.Requirements[i];
                var right = second.Requirements[i];

                Requirement mergedRequirement;
                if (!MergeRequirements(left, right, condition, out mergedRequirement))
                {
                    merged = null;
                    return false;
                }

                if (mergedRequirement == null)
                {
                    // conflicting requirements, replace the entire requirement set with an always_false()
                    merged.Requirements.Clear();
                    merged.Requirements.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
                    return true;
                }

                merged.Requirements.Add(mergedRequirement);
            }

            return true;
        }

        private static void MergeDuplicateAlts(List<List<RequirementEx>> groups)
        {
            // if two alt groups are exactly identical, or can otherwise be represented by merging their
            // logic, eliminate the redundant group.
            for (int i = groups.Count - 1; i > 1; i--)
            {
                var altsI = groups[i];

                for (int j = i - 1; j >= 1; j--)
                {
                    var altsJ = groups[j];

                    if (altsI.Count != altsJ.Count)
                        continue;

                    bool[] matches = new bool[altsI.Count];
                    RequirementEx[] merged = new RequirementEx[altsI.Count];
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
                        groups.RemoveAt(i);
                        break;
                    }
                }
            }

            // if at least two alt groups still exist, check for always_true and always_false placeholders
            if (groups.Count > 2)
            {
                bool hasAlwaysTrue = false;

                for (int j = groups.Count - 1; j >= 1; j--)
                {
                    if (groups[j].Count == 1 && groups[j][0].Requirements.Count == 1)
                    {
                        var result = groups[j][0].Requirements[0].Evaluate();
                        if (result == false)
                        {
                            // an always_false alt group is used for two cases:
                            // 1) building an alt group list (safe to remove)
                            // 2) keeping a PauseIf out of core (safe to remove if at least two other alt groups still exist)
                            if (groups.Count > 3)
                                groups.RemoveAt(j);
                        }
                        else if (result == true)
                        {
                            // an always_true alt group supercedes all other alt groups.
                            // if we see one, keep track of that and we'll process it later.
                            hasAlwaysTrue = true;
                        }
                    }
                }

                // if a trigger contains an always_true alt group, remove any other alt groups that don't have PauseIf or ResetIf conditions as they are unimportant
                if (hasAlwaysTrue)
                {
                    for (int j = groups.Count - 1; j >= 1; j--)
                    {
                        if (groups[j].Count == 1 && groups[j][0].Requirements.Count == 1 && groups[j][0].Requirements[0].Evaluate() == true)
                            continue;

                        bool hasPauseIf = false;
                        bool hasResetIf = false;
                        foreach (var requirementEx in groups[j])
                        {
                            switch (requirementEx.Requirements.Last().Type)
                            {
                                case RequirementType.PauseIf:
                                    hasPauseIf = true;
                                    break;
                                case RequirementType.ResetIf:
                                    hasResetIf = true;
                                    break;
                            }
                        }

                        if (!hasPauseIf && !hasResetIf)
                            groups.RemoveAt(j);
                    }

                    // if only the always_true group is left, get rid of it
                    if (groups.Count == 2)
                    {
                        groups.RemoveAt(1);

                        // if the core group is empty, add an explicit always_true
                        if (groups[0].Count == 0)
                        {
                            var requirementEx = new RequirementEx();
                            requirementEx.Requirements.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                            groups[0].Add(requirementEx);
                        }
                    }
                }
            }

            // if only one alt group is left, merge it into the core group
            if (groups.Count == 2)
            {
                groups[0].AddRange(groups[1]);
                groups.RemoveAt(1);
            }
        }

        private static void PromoteCommonAltsToCore(List<List<RequirementEx>> groups)
        {
            if (groups.Count < 2) // no alts
                return;

            if (groups.Count == 2) // only one alt group, merge to core
            {
                groups[0].AddRange(groups[1]);
                groups.RemoveAt(1);
                return;
            }

            // identify requirements present in all alt groups.
            var requirementsFoundInAll = new List<RequirementEx>();
            for (int i = 0; i < groups[1].Count; i++)
            {
                var requirementI = groups[1][i];
                bool foundInAll = true;
                for (int j = 2; j < groups.Count; j++)
                {
                    bool foundInGroup = false;
                    foreach (var requirementJ in groups[j])
                    {
                        if (requirementJ == requirementI)
                        {
                            foundInGroup = true;
                            break;
                        }
                    }

                    if (!foundInGroup)
                    {
                        foundInAll = false;
                        break;
                    }
                }

                if (foundInAll)
                    requirementsFoundInAll.Add(requirementI);
            }

            // PauseIf only affects the alt group that it's in, so it can only be promoted if the entire alt group is promoted
            if (requirementsFoundInAll.Any(r=> r.Requirements.Last().Type == RequirementType.PauseIf))
            {
                bool canPromote = true;
                for (int i = 1; i < groups.Count; i++)
                {
                    if (groups[i].Count != requirementsFoundInAll.Count)
                    {
                        canPromote = false;
                        break;
                    }
                }

                if (!canPromote)
                    requirementsFoundInAll.RemoveAll(r => r.Requirements.Last().Type == RequirementType.PauseIf);
            }

            // Measured and MeasuredIf cannot be separated. If any Measured or MeasuredIf items
            // remain in one of the alt groups (or exist in the core), don't promote any of the others.
            if (requirementsFoundInAll.Any(r => r.Requirements.Last().IsMeasured ||
                                                r.Requirements.Last().Type == RequirementType.MeasuredIf))
            {
                foreach (var group in groups)
                {
                    bool allPromoted = true;
                    foreach (var requirementEx in group)
                    {
                        var lastRequirement = requirementEx.Requirements.Last();
                        if (lastRequirement.IsMeasured || lastRequirement.Type == RequirementType.MeasuredIf)
                        {
                            if (!requirementsFoundInAll.Contains(requirementEx))
                            {
                                allPromoted = false;
                                break;
                            }
                        }
                    }

                    if (!allPromoted)
                    {
                        requirementsFoundInAll.RemoveAll(r => r.Requirements.Last().IsMeasured || r.Requirements.Last().Type == RequirementType.MeasuredIf);
                        break;
                    }
                }
            }

            foreach (var requirement in requirementsFoundInAll)
            {
                // ResetIf or HitCount in an alt group may be disabled by a PauseIf, don't promote if
                // any PauseIfs are not promoted
                if (requirement.Requirements.Last().Type == RequirementType.ResetIf || 
                    requirement.Requirements.Last().HitCount > 0)
                {
                    bool canPromote = true;

                    for (int i = 1; i < groups.Count; i++)
                    {
                        foreach (var requirementJ in groups[i])
                        {
                            if (requirementJ.Requirements.Last().Type == RequirementType.PauseIf && !requirementsFoundInAll.Contains(requirementJ))
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
                for (int i = 1; i < groups.Count; i++)
                {
                    for (int j = groups[i].Count - 1; j >= 0; j--)
                    {
                        if (groups[i][j] == requirement)
                        {
                            groups[i].RemoveAt(j);
                            break;
                        }
                    }
                }

                // put one copy of the repeated requirement it in the core group
                groups[0].Add(requirement);
            }

            // if any group has been completely moved to the core group, it's a subset of
            // all other groups, and any trivial logic can be removed from the other groups
            bool removeTrivialLogic = false;
            for (int i = 1; i < groups.Count; ++i)
            {
                if (groups[i].Count == 0)
                {
                    removeTrivialLogic = true;
                    break;
                }
            }

            if (removeTrivialLogic)
            {
                for (int i = groups.Count - 1; i > 0; --i)
                {
                    for (int j = groups[i].Count - 1; j >= 0; --j)
                    {
                        var finalCondition = groups[i][j].Requirements.Last();
                        if (finalCondition.Type == RequirementType.None && finalCondition.HitCount == 0)
                            groups[i].RemoveAt(j);
                    }

                    if (groups[i].Count == 0)
                        groups.RemoveAt(i);
                }
            }
        }

        private static void RemoveAlwaysFalseAlts(List<List<RequirementEx>> groups)
        {
            bool alwaysFalse = false;

            Predicate<List<RequirementEx>> isAlwaysFalse = group =>
               (group.Count == 1 && group[0].Requirements.Count == 1 && group[0].Requirements[0].Evaluate() == false);

            if (isAlwaysFalse(groups[0]))
            {
                // core is always_false; the entire trigger is always_false
                alwaysFalse = true;
            }
            else if (groups.Count > 1)
            {
                for (int i = groups.Count - 1; i > 0; i--)
                {
                    if (isAlwaysFalse(groups[i]))
                        groups.RemoveAt(i);
                }

                // only always_false alt groups were found, the entire trigger is always_false
                if (groups.Count == 1)
                    alwaysFalse = true;
            }

            if (alwaysFalse)
            {
                groups.Clear();
                groups.Add(new List<RequirementEx>());
                groups[0].Add(new RequirementEx());
                groups[0][0].Requirements.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
            }
        }

        private static void DenormalizeOrNexts(List<List<RequirementEx>> groups)
        {
            RequirementEx orNextGroup = null;
            if (groups.Count == 1)
            {
                foreach (var requirementEx in groups[0])
                {
                    if (requirementEx.Requirements.Any(r => r.Type == RequirementType.OrNext))
                    {
                        if (orNextGroup != null)
                            return;

                        // if there's a hit target, we can't split it up
                        if (requirementEx.Requirements.Last().HitCount == 0)
                            orNextGroup = requirementEx;
                    }
                }
            }

            // found a single OrNext and no alt groups. split it up
            if (orNextGroup != null)
            {
                groups[0].Remove(orNextGroup);

                var alt = new RequirementEx();
                var altGroup = new List<RequirementEx>();
                altGroup.Add(alt);
                groups.Add(altGroup);

                foreach (var requirement in orNextGroup.Requirements)
                {
                    alt.Requirements.Add(requirement);

                    if (requirement.Type == RequirementType.OrNext)
                    {
                        requirement.Type = orNextGroup.Requirements.Last().Type;

                        alt = new RequirementEx();
                        altGroup = new List<RequirementEx>();
                        altGroup.Add(alt);
                        groups.Add(altGroup);
                    }
                }
            }
        }

        private static void RemoveDuplicates(IList<RequirementEx> group, IList<RequirementEx> coreGroup)
        {
            for (int i = 0; i < group.Count; i++)
            {
                for (int j = group.Count - 1; j > i; j--)
                {
                    if (group[j] == group[i])
                        group.RemoveAt(j);
                }
            }

            if (coreGroup != null)
            {
                for (int i = 0; i < coreGroup.Count; i++)
                {
                    for (int j = group.Count - 1; j >= 0; j--)
                    {
                        if (group[j] == coreGroup[i])
                        {
                            bool remove = true;

                            if (group[j].Requirements.Count == 1 && group[j].Requirements[0].Evaluate() == true)
                            {
                                // always_true has special meaning and shouldn't be eliminated if present in the core group
                                remove = false;
                            }
                            else if (group[j].Requirements.Last().Type == RequirementType.PauseIf)
                            {
                                // if the PauseIf wasn't eliminated by NormalizeNonHitCountResetAndPauseIfs, it's protecting something. Keep it.
                                remove = false;
                            }

                            if (remove)
                                group.RemoveAt(j);
                        }
                    }
                }
            }
        }

        private static void RemoveRedundancies(IList<RequirementEx> group, bool hasHitCount)
        {
            for (int i = group.Count - 1; i >= 0; i--)
            {
                if (group[i].Requirements.Count > 1)
                    continue;

                var requirement = group[i].Requirements[0];

                // if one requirement is "X == N" and another is "ResetIf X != N", they can be merged.
                if (requirement.HitCount == 0 && (requirement.Type == RequirementType.ResetIf || requirement.Type == RequirementType.None))
                {
                    bool merged = false;
                    for (int j = 0; j < i; j++)
                    {
                        if (group[j].Requirements.Count > 1)
                            continue;

                        Requirement compareRequirement = group[j].Requirements[0];
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
                            bool isResetIf = (requirement.Type == RequirementType.ResetIf);
                            if (hasHitCount == isResetIf)
                                group[j].Requirements[0] = requirement;

                            group.RemoveAt(i);
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
                        if (group[j].Requirements.Count > 1)
                            continue;

                        RequirementEx merged;
                        if (MergeRequirements(group[i], group[j], ConditionalOperation.And, out merged))
                        {
                            group[j] = merged;
                            group.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private static void MergeAddSourceConstants(IList<RequirementEx> group)
        {
            foreach (var requirementEx in group)
            {
                uint addSourceConstantTotal = 0;
                var addSourceConstants = new List<Requirement>();
                var toRemove = new List<Requirement>();
                bool isAddAddress = false;
                foreach (var requirement in requirementEx.Requirements)
                {
                    switch (requirement.Type)
                    {
                        case RequirementType.AddSource:
                            if (!isAddAddress && requirement.Left.Type == FieldType.Value)
                            {
                                addSourceConstantTotal += requirement.Left.Value;
                                addSourceConstants.Add(requirement);
                            }
                            break;

                        case RequirementType.SubSource:
                            if (!isAddAddress && requirement.Left.Type == FieldType.Value)
                            {
                                addSourceConstantTotal -= requirement.Left.Value;
                                addSourceConstants.Add(requirement);
                            }
                            break;

                        case RequirementType.AddAddress:
                            isAddAddress = true;
                            continue;

                        default:
                            // anything other than AddAddress is lower priority than AddSource or SubSource
                            // so process the merge now and reset the counters
                            if (addSourceConstants.Count > 0)
                            {
                                MergeAddSourceConstants(requirement, toRemove, addSourceConstants, addSourceConstantTotal);

                                addSourceConstants.Clear();
                                addSourceConstantTotal = 0;
                            }
                            break;
                    }

                    isAddAddress = false;
                }

                if (!isAddAddress && addSourceConstants.Count > 0)
                    MergeAddSourceConstants(requirementEx.Requirements.Last(), toRemove, addSourceConstants, addSourceConstantTotal);

                foreach (var requirement in toRemove)
                    requirementEx.Requirements.Remove(requirement);
            }
        }

        private static void MergeAddSourceConstants(Requirement requirement, List<Requirement> toRemove,
            List<Requirement> addSourceConstants, uint addSourceConstantTotal)
        {
            if (requirement.Left.Type == FieldType.Value)
            {
                requirement.Left = new Field
                {
                    Type = FieldType.Value,
                    Size = requirement.Left.Size,
                    Value = requirement.Left.Value + addSourceConstantTotal
                };

                toRemove.AddRange(addSourceConstants);
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

        private static void MergeBits(IList<RequirementEx> group)
        {
            var mergableRequirements = new List<RequirementEx>();

            var references = new TinyDictionary<uint, int>();
            foreach (var requirementEx in group)
            {
                if (requirementEx.Requirements.Count == 1 && IsMergable(requirementEx.Requirements[0]))
                    mergableRequirements.Add(requirementEx);
            }

            foreach (var requirementEx in mergableRequirements)
            {
                var requirement = requirementEx.Requirements[0];

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
                    MergeBits(group, mergableRequirements, kvp.Key, FieldSize.Byte, (kvp.Value >> 8) & 0xFF);
                }
                else
                {
                    if ((kvp.Value & 0x0F) == 0x0F)
                        MergeBits(group, mergableRequirements, kvp.Key, FieldSize.LowNibble, (kvp.Value >> 8) & 0x0F);
                    if ((kvp.Value & 0xF0) == 0xF0)
                        MergeBits(group, mergableRequirements, kvp.Key, FieldSize.HighNibble, (kvp.Value >> 12) & 0x0F);
                }
            }
        }

        private static void MergeBits(IList<RequirementEx> group, ICollection<RequirementEx> mergableRequirements, uint address, FieldSize newSize, int newValue)
        {
            bool insert = true;
            int insertAt = 0;
            for (int i = group.Count - 1; i >= 0; i--)
            {
                if (!mergableRequirements.Contains(group[i]))
                    continue;

                var requirement = group[i].Requirements[0];
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
                    group.RemoveAt(i);
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
                var requirementEx = new RequirementEx();
                requirementEx.Requirements.Add(requirement);
                group.Insert(insertAt, requirementEx);
            }
        }

        private static void Unprocess(ICollection<Requirement> collection, List<RequirementEx> group)
        {
            collection.Clear();
            foreach (var requirementEx in group)
            {
                foreach (var requirement in requirementEx.Requirements)
                    collection.Add(requirement);
            }
        }


        private static string CheckForMultipleMeasuredTargets(IEnumerable<Requirement> requirements, ref uint measuredTarget)
        {
            foreach (var requirement in requirements)
            {
                if (requirement.IsMeasured)
                {
                    uint conditionTarget = requirement.HitCount;
                    if (conditionTarget == 0)
                        conditionTarget = requirement.Right.Value;

                    if (measuredTarget == 0)
                        measuredTarget = conditionTarget;
                    else if (measuredTarget != conditionTarget)
                        return "Multiple measured() conditions must have the same target.";
                }
            }

            return null;
        }

        private static void NormalizeResetNextIfs(List<List<RequirementEx>> groups)
        {
            RequirementEx resetNextIf = null;
            var resetNextIfClauses = new List<RequirementEx>();
            var resetNextIsForPause = false;
            foreach (var group in groups)
            {
                foreach (var requirementEx in group)
                {
                    var lastRequirement = requirementEx.Requirements.Last();
                    if (lastRequirement.HitCount > 0)
                    {
                        bool hasResetNextIf = false;

                        foreach (var requirement in requirementEx.Requirements)
                        {
                            if (requirement.Type == RequirementType.ResetNextIf)
                            {
                                hasResetNextIf = true;

                                var subclause = new RequirementEx();
                                foreach (var requirement2 in requirementEx.Requirements)
                                {
                                    subclause.Requirements.Add(requirement2);
                                    if (requirement2.Type == RequirementType.ResetNextIf)
                                        break;
                                }

                                if (resetNextIf == null)
                                {
                                    resetNextIf = subclause;
                                }
                                else if (resetNextIf != subclause)
                                {
                                    // can only extract ResetNextIf if it's identical
                                    return;
                                }
                            }
                        }

                        // hit target on non-ResetNextIf clause, can't extract the ResetNextIf (if we even find one)
                        if (!hasResetNextIf)
                            return;

                        resetNextIfClauses.Add(requirementEx);

                        resetNextIsForPause |= (lastRequirement.Type == RequirementType.PauseIf);
                    }
                }
            }

            // did not find a ResetNextIf
            if (resetNextIf == null)
                return;

            // remove the common clause from each complex clause
            foreach (var requirementEx in resetNextIfClauses)
            {
                requirementEx.Requirements.RemoveRange(0, resetNextIf.Requirements.Count);
                do
                {
                    int index = requirementEx.Requirements.FindIndex(r => r.Type == RequirementType.ResetNextIf);
                    if (index == -1)
                        break;

                    requirementEx.Requirements.RemoveRange(index - resetNextIf.Requirements.Count + 1, resetNextIf.Requirements.Count);
                } while (true);
            }

            // change the ResetNextIf to a ResetIf
            resetNextIf.Requirements.Last().Type = RequirementType.ResetIf;

            // if the reset is for a pause, it has to be moved to a separate group or it can't be evaluated
            if (resetNextIsForPause)
            {
                var newGroup = new List<RequirementEx>();
                newGroup.Add(resetNextIf);
                groups.Add(newGroup);

                if (groups.Count > 2)
                {
                    // if alt groups already exist, add the an always false condition to prevent the new alt group
                    // from ever being true. otherwise, just add it as is and it'll always be true.
                    var newClause = new RequirementEx();
                    newClause.Requirements.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
                    newGroup.Add(newClause);
                }
                else
                {
                    // if alt groups don't exist, let the new group always be true, but add an always false
                    // second group to prevent it from being collapsed back into core. the always false group
                    // will be optimized out later.
                    var newClause = new RequirementEx();
                    newClause.Requirements.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());

                    newGroup = new List<RequirementEx>();
                    newGroup.Add(newClause);
                    groups.Add(newGroup);
                }
            }
            else
            {
                // reset is not for a pause, insert it where the ResetNextIf was
                foreach (var group in groups)
                {
                    for (int i = 0; i < group.Count; ++i)
                    {
                        if (resetNextIfClauses.Contains(group[i]))
                        {
                            group.Insert(i, resetNextIf);
                            break;
                        }
                    }
                }
            }
        }

        public string Optimize()
        {
            if (_core.Count == 0 && _alts.Count == 0)
                return "No requirements found.";

            // group complex expressions
            var groups = new List<List<RequirementEx>>(_alts.Count + 1);
            groups.Add(RequirementEx.Combine(_core));
            for (int i = 0; i < _alts.Count; i++)
                groups.Add(RequirementEx.Combine(_alts[i]));

            foreach (var group in groups)
                MergeAddSourceConstants(group);

            // attempt to extract ResetNextIf into alt group
            NormalizeResetNextIfs(groups);

            // convert ResetIfs and PauseIfs without HitCounts to standard requirements
            bool hasHitCount = HasHitCount(groups);
            NormalizeNonHitCountResetAndPauseIfs(groups, hasHitCount);

            // clamp memory reference comparisons to bounds; identify comparisons that can never
            // be true, or are always true; ensures constants are on the right
            foreach (var group in groups)
                NormalizeComparisons(group);

            // remove duplicates within a set of requirements
            RemoveDuplicates(groups[0], null);
            for (int i = groups.Count - 1; i > 0; i--)
            {
                RemoveDuplicates(groups[i], groups[0]);
                if (groups[i].Count == 0)
                    groups.RemoveAt(i);
            }

            // remove redundancies (i > 3 && i > 5) => (i > 5)
            foreach (var group in groups)
                RemoveRedundancies(group, hasHitCount);

            // bit1(x) && bit2(x) && bit3(x) && bit4(x) => low4(x)
            foreach (var group in groups)
                MergeBits(group);

            // merge duplicate alts
            MergeDuplicateAlts(groups);

            // identify any items common to all alts and promote them to core
            PromoteCommonAltsToCore(groups);

            // if the core group contains an always_true statement in addition to any other promoted statements, 
            // remove the always_true statement
            if (groups[0].Count > 1 && groups[0][0].Requirements.Count == 1 && groups[0][0].Requirements[0].Evaluate() == true)
                groups[0].RemoveAt(0);

            // if core is always_false, or all alts are always_false, the entire trigger is always_false.
            // otherwise, any always_falses in the alt groups can be removed as they have no impact on the trigger.
            RemoveAlwaysFalseAlts(groups);

            // if the core contains an OrNext and there are no alts, denormalize it to increase backwards compatibility
            DenormalizeOrNexts(groups);

            // convert back to flattened expressions
            _core.Clear();
            Unprocess(_core, groups[0]);

            for (int i = 1; i < groups.Count; i++)
            {
                if (i - 1 < _alts.Count)
                    _alts[i - 1].Clear();
                else
                    _alts.Add(new List<Requirement>());

                Unprocess(_alts[i - 1], groups[i]);
            }

            while (_alts.Count >= groups.Count)
                _alts.RemoveAt(_alts.Count - 1);

            // ensure only one Measured target exists
            uint measuredTarget = 0;
            string measuredError = CheckForMultipleMeasuredTargets(_core, ref measuredTarget);
            if (measuredError != null)
                return measuredError;

            foreach (var group in _alts)
            {
                measuredError = CheckForMultipleMeasuredTargets(group, ref measuredTarget);
                if (measuredError != null)
                    return measuredError;
            }

            // success!
            return null;
        }

        internal ParseErrorExpression CollapseForSubClause()
        {
            // only one AndNext chain allowed. core group will automatically generate an AndNext
            // chain (with every alt, which will often result in a too complex error)
            ICollection<Requirement> andNextAlt = null;
            if (_core.Count > 0)
            {
                // turn the core group into an AndNext chain
                foreach (var requirement in _core)
                {
                    if (requirement.Type == RequirementType.None)
                        requirement.Type = RequirementType.AndNext;
                }

                andNextAlt = _core;
            }

            // the last item cannot have its own HitCount as it will hold the HitCount for the group.
            // if necessary, find one without a HitCount and make it the last.
            EnsureLastGroupHasNoHitCount(_alts);

            // merge the alts into the core group as an OrNext chain
            var newCore = new List<Requirement>();
            foreach (var alt in _alts)
            {
                if (alt.Last().Type != RequirementType.None)
                    return new ParseErrorExpression("Modifier not allowed in subclause");

                if (alt.Count() > 1)
                {
                    bool hasAndNext = false;
                    foreach (var requirement in alt)
                    {
                        if (requirement.Type == RequirementType.None || requirement.Type == RequirementType.AndNext)
                        {
                            if (hasAndNext)
                            {
                                // only one AndNext group allowed
                                if (andNextAlt != null)
                                    return new ParseErrorExpression("Combination of &&s and ||s is too complex for subclause");

                                andNextAlt = alt;
                            }

                            requirement.Type = RequirementType.AndNext;
                            hasAndNext = true;
                        }
                    }
                }

                alt.Last().Type = RequirementType.OrNext;

                if (alt != andNextAlt)
                    newCore.AddRange(alt);
            }

            if (andNextAlt == _core)
            {
                if (newCore.Any())
                    newCore.Last().Type = RequirementType.AndNext;
                newCore.AddRange(_core);
            }
            else if (andNextAlt != null)
            {
                newCore.InsertRange(0, andNextAlt);
            }

            var last = newCore.Last();
            if (last.Type == RequirementType.AndNext || last.Type == RequirementType.OrNext)
                last.Type = RequirementType.None;

            _core = newCore;
            _alts.Clear();
            return null;
        }

        internal static void EnsureLastGroupHasNoHitCount(List<ICollection<Requirement>> requirements)
        {
            if (requirements.Count == 0)
                return;

            int index = requirements.Count - 1;
            if (requirements[index].Last().HitCount > 0)
            {
                do
                {
                    index--;
                } while (index >= 0 && requirements[index].Last().HitCount > 0);

                if (index == -1)
                {
                    // all requirements had HitCount limits, add a dummy item that's never true for the total HitCount
                    requirements.Add(new Requirement[] { AlwaysFalseFunction.CreateAlwaysFalseRequirement() });
                }
                else
                {
                    // found a requirement without a HitCount limit, move it to the last spot for the total HitCount
                    var requirement = requirements[index];
                    requirements.RemoveAt(index);
                    requirements.Add(requirement);
                }
            }
        }
    }
}
