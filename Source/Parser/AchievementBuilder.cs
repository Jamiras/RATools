using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions;
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

        internal bool IsDumped { get; set; }

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

        public void ParseValue(Tokenizer tokenizer)
        {
            var current = _core;
            do
            {
                do
                {
                    var requirement = new Requirement();
                    requirement.Type = RequirementType.AddSource;

                    requirement.Left = Field.Deserialize(tokenizer);

                    requirement.Operator = ReadOperator(tokenizer);
                    if (requirement.Operator != RequirementOperator.None)
                    {
                        requirement.Right = Field.Deserialize(tokenizer);
                        if (requirement.Right.Type == FieldType.Value &&
                            (requirement.Right.Value & 0x80000000) != 0)
                        {
                            requirement.Type = RequirementType.SubSource;
                            if (requirement.Right.Value == 0xFFFFFFFF)
                            {
                                requirement.Operator = RequirementOperator.None;
                                requirement.Right = new Field { Type = FieldType.Value, Value = 1 };
                            }
                            else
                            {
                                requirement.Right = new Field
                                {
                                    Type = FieldType.Value,
                                    Value = (uint)(-(int)requirement.Right.Value)
                                };
                            }
                        }
                    }

                    current.Add(requirement);

                    if (tokenizer.NextChar != '_')
                        break;

                    tokenizer.Advance();
                } while (true);

                var lastAddSource = current.Last();
                if (lastAddSource.Type != RequirementType.AddSource)
                {
                    lastAddSource = current.Last(r => r.Type == RequirementType.AddSource);
                    current.Remove(lastAddSource);
                    current.Add(lastAddSource);
                }
                lastAddSource.Type = RequirementType.None;

                if (tokenizer.NextChar != '$')
                    break;

                tokenizer.Advance();

                if (current.Count != 0)
                {
                    current = new List<Requirement>();
                    _alts.Add(current);
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
                    return RequirementOperator.BitwiseAnd;
            }

            return RequirementOperator.None;
        }

        public static double GetMinimumVersion(Achievement achievement)
        {
            var minimumVersion = MinimumVersion(achievement.CoreRequirements);
            foreach (var group in achievement.AlternateRequirements)
            {
                var altMinimumVersion = MinimumVersion(group);
                if (altMinimumVersion > minimumVersion)
                    minimumVersion = altMinimumVersion;
            }

            return minimumVersion;
        }

        private static double GetMinimumVersion(string trigger)
        {
            var achievementBuilder = new AchievementBuilder();
            achievementBuilder.ParseRequirements(Tokenizer.CreateTokenizer(trigger));
            return GetMinimumVersion(achievementBuilder.ToAchievement());
        }

        public static double GetMinimumVersion(Leaderboard leaderboard)
        {
            var minimumVersion = GetMinimumVersion(leaderboard.Start);
            minimumVersion = Math.Max(minimumVersion, GetMinimumVersion(leaderboard.Cancel));
            minimumVersion = Math.Max(minimumVersion, GetMinimumVersion(leaderboard.Submit));
            minimumVersion = Math.Max(minimumVersion, GetMinimumVersion(leaderboard.Value));
            return minimumVersion;
        }

        private static double MinimumVersion(IEnumerable<Requirement> requirements)
        {
            double minVer = 0.30;

            foreach (var requirement in requirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.AndNext:
                        // 0.76 21 Jun 2019
                        if (minVer < 0.76)
                            minVer = 0.76;
                        break;

                    case RequirementType.AddAddress:
                    case RequirementType.Measured:
                        // 0.77 30 Nov 2019
                        if (minVer < 0.77)
                            minVer = 0.77;
                        break;

                    case RequirementType.MeasuredIf:
                    case RequirementType.OrNext:
                        // 0.78 18 May 2020
                        if (minVer < 0.78)
                            minVer = 0.78;
                        break;

                    case RequirementType.ResetNextIf:
                    case RequirementType.Trigger:
                    case RequirementType.SubHits:
                        // 0.79 22 May 2021
                        if (minVer < 0.79)
                            minVer = 0.79;
                        break;

                    case RequirementType.MeasuredPercent:
                        // 0.80 TBD
                        if (minVer < 0.80)
                            minVer = 0.80;
                        break;

                    default:
                        break;
                }

                switch (requirement.Operator)
                {
                    case RequirementOperator.Multiply:
                    case RequirementOperator.Divide:
                    case RequirementOperator.BitwiseAnd:
                        // 0.78 18 May 2020
                        if (minVer < 0.78)
                            minVer = 0.78;
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
                            if (minVer < 0.76)
                                minVer = 0.76;
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
                            if (minVer < 0.77)
                                minVer = 0.77;
                            break;

                        case FieldSize.BitCount:
                            // 0.78 18 May 2020
                            if (minVer < 0.78)
                                minVer = 0.78;
                            break;

                        case FieldSize.BigEndianWord:
                        case FieldSize.BigEndianTByte:
                        case FieldSize.BigEndianDWord:
                        case FieldSize.Float:
                        case FieldSize.MBF32:
                            // 0.80 TBD
                            if (minVer < 0.80)
                                minVer = 0.80;
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
            var minimumVersion = MinimumVersion(core);
            foreach (var group in alts)
            {
                var altMinimumVersion = MinimumVersion(group);
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

        private static void SerializeRequirement(Requirement requirement, StringBuilder builder, double minimumVersion)
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

            if (requirement.IsScalable)
            {
                switch (requirement.Operator)
                {
                    case RequirementOperator.Multiply: builder.Append('*'); break;
                    case RequirementOperator.Divide: builder.Append('/'); break;
                    case RequirementOperator.BitwiseAnd: builder.Append('&'); break;
                    default:
                        if (minimumVersion < 0.77)
                            builder.Append("=0");
                        return;
                }

                requirement.Right.Serialize(builder);
            }
            else
            {
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
                    case RequirementOperator.BitwiseAnd: builder.Append('&'); break;
                    case RequirementOperator.None: return;
                }

                requirement.Right.Serialize(builder);
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
            var measuredIfs = new List<RequirementEx>();
            foreach (var group in groups)
            {
                if (group.IsMeasured)
                    measured = group;
                else if (group.Type == RequirementType.MeasuredIf)
                    measuredIfs.Add(group);
            }

            string measuredIfString = null;
            if (measuredIfs.Count > 0 && measured != null)
            {
                // if both a Measured and MeasuredIf exist, merge the MeasuredIf into the Measured group so
                // it can be converted to a 'when' parameter of the measured() call
                var measuredIfBuilder = new StringBuilder();
                foreach (var measuredIf in measuredIfs)
                {
                    groups.Remove(measuredIf);

                    var index = measuredIfBuilder.Length;
                    if (index > 0)
                    {
                        measuredIfBuilder.Append(" && ");
                        index += 4;
                    }

                    measuredIf.AppendString(measuredIfBuilder, numberFormat);

                    // remove "measured_if(" and ")" - they're not needed when used as a when clause
                    measuredIfBuilder.Length--;
                    measuredIfBuilder.Remove(index, 12);
                }

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

        private static bool? NormalizeLimits(Requirement requirement, bool forSubclause)
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

            if (!forSubclause &&
                requirement.Left.Size == FieldSize.BitCount &&
                requirement.Operator == RequirementOperator.Equal &&
                requirement.Right.Type == FieldType.Value &&
                requirement.Type != RequirementType.Measured &&
                requirement.Type != RequirementType.MeasuredPercent)
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

        private static void NormalizeComparisons(IList<RequirementEx> requirements, bool forSubclause)
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
                    result = NormalizeLimits(requirement, forSubclause);

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
                    switch (requirement.Type)
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

        private static bool InvertRequirementLogic(RequirementEx requirementEx)
        {
            bool hasAndNext = false;
            bool hasOrNext = false;

            foreach (var requirement in requirementEx.Requirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.AddHits:
                        // if AddHits is present, we can't invert the logic because we can't determine which conditions
                        // will be true or false for any given frame
                        return false;

                    case RequirementType.AndNext:
                        hasAndNext = true;
                        break;

                    case RequirementType.OrNext:
                        hasOrNext = true;
                        break;
                }
            }

            // if both AndNext and OrNext are present, we can't just invert the logic as the order of operations may be affected
            if (hasAndNext && hasOrNext)
                return false;

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

            var lastRequirement = requirementEx.Requirements.Last();
            lastRequirement.Operator = Requirement.GetOpposingOperator(lastRequirement.Operator);
            return true;
        }

        private bool NormalizeResetIfs(List<List<RequirementEx>> groups)
        {
            // a ResetIf condition in an achievement without any HitCount conditions is the same as a non-ResetIf 
            // condition for the opposite comparison (i.e. ResetIf val = 0 is the same as val != 0). Some developers 
            // use ResetIf conditions to keep the HitCount counter at 0 until the achievement is activated. This is 
            // a bad practice, as it makes the achievement harder to read, and it normally adds an additional 
            // condition to be evaluated every frame.
            bool converted = false;
            foreach (var group in groups)
            {
                foreach (var requirementEx in group.Where(r => r.Type == RequirementType.ResetIf))
                {
                    if (InvertRequirementLogic(requirementEx))
                    {
                        var lastRequirement = requirementEx.Requirements.Last();
                        lastRequirement.Type = RequirementType.None;
                        lastRequirement.HitCount = 0;
                        converted = true;
                    }
                }
            }

            return converted;
        }

        private void NormalizePauseIfs(List<List<RequirementEx>> groups)
        {
            // a PauseIf condition in an group that's not guarding anything is the same as a non-PauseIf
            // condition for the opposite comparison (i.e. PauseIf val = 0 is the same as val != 0).
            foreach (var group in groups)
            {
                if (!group.Any(r => r.Type == RequirementType.PauseIf))
                    continue;
                if (group.Any(r => r.IsAffectedByPauseIf))
                    continue;

                foreach (var requirementEx in group.Where(r => r.Type == RequirementType.PauseIf))
                {
                    // if the PauseIf has a HitTarget, it's a PauseLock. don't invert it even if
                    // nothing else is affected.
                    var lastRequirement = requirementEx.Requirements.Last();
                    if (lastRequirement.HitCount != 0)
                        continue;

                    if (InvertRequirementLogic(requirementEx))
                    {
                        lastRequirement = requirementEx.Requirements.Last();
                        lastRequirement.Type = RequirementType.None;
                        lastRequirement.HitCount = 0;
                    }
                }
            }
        }

        private static void MergeDuplicateAlts(List<List<RequirementEx>> groups)
        {
            RequirementMerger.MergeRequirementGroups(groups);
        }

        private static void PromoteCommonAltsToCore(List<List<RequirementEx>> groups)
        {
            if (groups.Count < 2) // no alts
                return;

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

            if (requirementsFoundInAll.Any())
                PromoteCommonAltsToCore(groups, requirementsFoundInAll);

            // if only two groups remain, merge the alt group into the core (unless ResetIf/PauseIf conflict)
            if (groups.Count == 2)
            {
                if (groups[1].Any(r => r.Type == RequirementType.PauseIf))
                {
                    // PauseIf cannot be promoted if a ResetIf or HitTarget exists in core as the PauseIf may trump the ResetIf/HitTarget
                    if (groups[0].Any(r => r.IsAffectedByPauseIf))
                        return;
                }

                // ResetIf/HitTarget cannot be promoted if a PauseIf exists in core as the PauseIf may trump the ResetIf/HitTarget
                if (groups[0].Any(r => r.Type == RequirementType.PauseIf))
                {
                    if (groups[1].Any(r => r.IsAffectedByPauseIf))
                        return;
                }

                groups[0].AddRange(groups[1]);
                groups.RemoveAt(1);
            }
        }

        private static void PromoteCommonAltsToCore(List<List<RequirementEx>> groups, List<RequirementEx> requirementsFoundInAll)
        {
            if (groups[0].Any(r => r.IsAffectedByPauseIf))
            {
                // PauseIf cannot be promoted if a ResetIf exists in core as the PauseIf may trump the ResetIf
                // PauseIf cannot be promoted if a HitTarget exists in core as the PauseIf may trump the HitTarget
                requirementsFoundInAll.RemoveAll(r => r.Type == RequirementType.PauseIf);
            }
            else
            {
                // PauseIf only affects the alt group that it's in, so it can only be promoted if the entire alt group is promoted
                if (requirementsFoundInAll.Any(r => r.Type == RequirementType.PauseIf))
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
                        requirementsFoundInAll.RemoveAll(r => r.Type == RequirementType.PauseIf);
                }
            }

            // ResetIf and HitTargets cannot be promoted if a PauseIf exists in core as the PauseIf may trump the ResetIf/HitTarget
            if (groups[0].Any(r => r.Type == RequirementType.PauseIf))
                requirementsFoundInAll.RemoveAll(r => r.IsAffectedByPauseIf);

            // ResetIf and HitTargets cannot be promoted if they were being guarded by a PauseIf that wasn't promoted
            if (requirementsFoundInAll.Any(r => r.IsAffectedByPauseIf))
            {
                bool canPromote = true;

                for (int i = 1; i < groups.Count; i++)
                {
                    foreach (var requirementEx in groups[i])
                    {
                        if (requirementEx.Type == RequirementType.PauseIf &&
                            !requirementsFoundInAll.Contains(requirementEx))
                        {
                            canPromote = false;
                            break;
                        }
                    }

                    if (!canPromote)
                        break;
                }

                if (!canPromote)
                    requirementsFoundInAll.RemoveAll(r => r.IsAffectedByPauseIf);
            }

            // Measured and MeasuredIf cannot be separated. If any Measured or MeasuredIf items
            // remain in one of the alt groups (or exist in the core), don't promote any of the others.
            if (requirementsFoundInAll.Any(r => r.IsMeasured || r.Type == RequirementType.MeasuredIf))
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    bool allPromoted = true;
                    foreach (var requirementEx in groups[i])
                    {
                        if (requirementEx.IsMeasured || requirementEx.Type == RequirementType.MeasuredIf)
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
                        requirementsFoundInAll.RemoveAll(r => r.IsMeasured || r.Type == RequirementType.MeasuredIf);
                        break;
                    }
                }
            }

            // remove the redundant requirements from each alt group
            if (requirementsFoundInAll.Any())
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    groups[i].RemoveAll(r => requirementsFoundInAll.Any(r2 => r2 == r));

                    // if the alt group has been fully moved into core, it can be discarded
                    if (groups[i].Count == 0)
                        groups.RemoveAt(i);
                }

                // put one copy of the repeated requirements in the core group
                groups[0].AddRange(requirementsFoundInAll);
            }
        }

        private static void RemoveAlwaysFalseAlts(List<List<RequirementEx>> groups)
        {
            bool alwaysFalse = false;

            Predicate<List<RequirementEx>> isAlwaysFalse = 
                group => group.Count == 1 && group[0].Evaluate() == false;

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
            bool canMoveToAlts = false;
            if (groups.Count == 1)
            {
                // if a single group has multiple OrNext clauses, don't bother promoting either to alt groups
                var orNextClauseCount = groups[0].Count(g => g.Requirements.Any(r => r.Type == RequirementType.OrNext));
                if (orNextClauseCount == 0)
                    return; // nothing to do

                canMoveToAlts = orNextClauseCount < 2;
            }

            for (int j = groups.Count - 1; j >= 0; --j)
            {
                var group = groups[j];
                for (int i = 0; i < group.Count; ++i)
                {
                    var requirementEx = group[i];
                    if (requirementEx.Requirements.Count < 2)
                        continue;

                    var moveToAlts = false;
                    var orNextGroupType = requirementEx.Type;
                    switch (orNextGroupType)
                    {
                        case RequirementType.None:
                        case RequirementType.Trigger:
                            // logic conditions have to be moved into alts
                            if (!canMoveToAlts)
                                continue;
                            moveToAlts = true;
                            goto case RequirementType.ResetIf;

                        case RequirementType.ResetIf:
                        case RequirementType.PauseIf:
                            // if it's a ResetIf, PauseIf, or Trigger, it can be split into multiple clauses
                            bool seenOrNext = false;
                            bool canBeSplit = true;
                            foreach (var requirement in requirementEx.Requirements)
                            {
                                if (requirement.Type == RequirementType.OrNext)
                                {
                                    seenOrNext = true;
                                }
                                else if (requirement.Type == RequirementType.AndNext)
                                {
                                    // if there's an AndNext after the OrNext, we can't split it up.
                                    // ((A || B) && C)
                                    canBeSplit &= !seenOrNext;
                                }
                                else if (requirement.Type == RequirementType.ResetNextIf)
                                {
                                    // if there's a ResetNextIf, we can't split it up.
                                    canBeSplit = false;
                                }
                                else if (requirement.HitCount > 0)
                                {
                                    // if there's an hit target after the OrNext, we can't split it up.
                                    // once(A || B)
                                    canBeSplit &= !seenOrNext;
                                }
                            }

                            if (seenOrNext && canBeSplit)
                            {
                                var subclause = new RequirementEx();

                                if (moveToAlts)
                                {
                                    group.RemoveAt(i);
                                    groups.Add(new List<RequirementEx>() { subclause });
                                }
                                else
                                {
                                    group[i] = subclause;
                                }

                                foreach (var requirement in requirementEx.Requirements)
                                {
                                    subclause.Requirements.Add(requirement);

                                    if (requirement.Type == RequirementType.OrNext)
                                    {
                                        requirement.Type = orNextGroupType;

                                        subclause = new RequirementEx();

                                        if (moveToAlts)
                                            groups.Add(new List<RequirementEx>() { subclause });
                                        else
                                            group.Insert(++i, subclause);
                                    }
                                }
                            }
                            break;

                        default:
                            break;
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
                            else if (group[j].Type == RequirementType.PauseIf)
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
                for (int j = 0; j < i; j++)
                {
                    if (group[j].Requirements.Count > 1)
                        continue;

                    RequirementEx merged = RequirementMerger.MergeRequirements(group[i], group[j], ConditionalOperation.And);
                    if (merged != null)
                    {
                        if (group[i] == merged)
                        {
                            group.RemoveAt(j);
                        }
                        else
                        {
                            group[j] = merged;
                            group.RemoveAt(i);
                        }
                        break;
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

        private class BitReferences
        {
            public uint address;
            public FieldType memoryType;
            public ushort flags;
            public ushort value;
            public List<Requirement> requirements;

            public static BitReferences Find(List<BitReferences> references, uint address, FieldType memoryType)
            {
                BitReferences bitReferences = references.FirstOrDefault(r => r.address == address && r.memoryType == memoryType);
                if (bitReferences == null)
                {
                    bitReferences = new BitReferences();
                    bitReferences.address = address;
                    bitReferences.memoryType = memoryType;
                    bitReferences.requirements = new List<Requirement>();
                    references.Add(bitReferences);
                }

                return bitReferences;
            }
        };

        private static void MergeBitCount(RequirementEx requirementEx)
        {
            var references = new List<BitReferences>();
            bool inAddAddress = false;
            for (int i = 0; i < requirementEx.Requirements.Count; i++)
            {
                var requirement = requirementEx.Requirements[i];
                if (i != requirementEx.Requirements.Count - 1)
                {
                    switch (requirement.Type)
                    {
                        case RequirementType.AddAddress:
                            inAddAddress = true;
                            continue;

                        case RequirementType.AddSource:
                            if (requirement.Operator != RequirementOperator.None)
                                goto default;
                            break;

                        default:
                            inAddAddress = false;
                            continue;
                    }
                }

                if (!inAddAddress &&
                    requirement.Left.Size >= FieldSize.Bit0 && requirement.Left.Size <= FieldSize.Bit7)
                {
                    BitReferences bitReferences = BitReferences.Find(references, requirement.Left.Value, requirement.Left.Type);
                    bitReferences.requirements.Add(requirement);
                    bitReferences.flags |= (ushort)(1 << (requirement.Left.Size - FieldSize.Bit0));
                }

                inAddAddress = false;
            }

            var comparison = requirementEx.Requirements.Last();
            foreach (var bitReference in references)
            {
                if (bitReference.flags == 0xFF)
                {
                    var requirements = bitReference.requirements;
                    while (requirements.Count >= 8)
                    {
                        var matches = new List<Requirement>(8);
                        for (var bit = FieldSize.Bit0; bit <= FieldSize.Bit7; bit++)
                        {
                            var requirement = requirements.FirstOrDefault(r => r.Left.Size == bit);
                            if (requirement == null) // second pass may not have enough items for a second BitCount
                                break;
                            requirements.Remove(requirement);
                            matches.Add(requirement);
                        }

                        if (matches.Count < 8) // second pass may not have enough items for a second BitCount
                            break;

                        int insertIndex = requirementEx.Requirements.Count - 1;
                        foreach (var requirement in matches)
                        {
                            var index = requirementEx.Requirements.IndexOf(requirement);
                            if (index < insertIndex)
                                insertIndex = index;

                            requirementEx.Requirements.RemoveAt(index);
                        }

                        requirementEx.Requirements.Insert(insertIndex, new Requirement
                        {
                            Type = RequirementType.AddSource,
                            Left = new Field
                            {
                                Size = FieldSize.BitCount,
                                Type = bitReference.memoryType,
                                Value = bitReference.address
                            }
                        });
                    }
                }
            }

            var comparisonIndex = requirementEx.Requirements.IndexOf(comparison);
            if (comparisonIndex == -1)
            {
                var requirement = requirementEx.Requirements.Last();
                comparison.Left = requirement.Left;
                requirementEx.Requirements.RemoveAt(requirementEx.Requirements.Count - 1);
                requirementEx.Requirements.Add(comparison);
            }
            else
            {
                requirementEx.Requirements.RemoveAt(comparisonIndex);
                requirementEx.Requirements.Add(comparison);
            }
        }

        private static void MergeBits(IList<RequirementEx> group)
        {
            var references = new List<BitReferences>();
            foreach (var requirementEx in group)
            {
                // convert AddSource bit chain to bitcount
                if (requirementEx.Requirements.Count >= 8)
                    MergeBitCount(requirementEx);

                // ignore complex conditions (AddAddress, AndNext, etc)
                if (requirementEx.Requirements.Count != 1)
                    continue;

                // can only merge equality comparisons (bit0=0, bit1=1, etc)
                var requirement = requirementEx.Requirements[0];
                if (requirement.Operator != RequirementOperator.Equal)
                    continue;
                if (!requirement.Left.IsMemoryReference || requirement.Right.Type != FieldType.Value)
                    continue;

                // cannot merge unless all hit counts are the same
                if (requirement.HitCount != 0)
                    continue;

                // cannot merge if logic is attached to the condition
                if (requirement.Type != RequirementType.None)
                    continue;

                var bitReference = BitReferences.Find(references, requirement.Left.Value, requirement.Left.Type);
                switch (requirement.Left.Size)
                {
                    case FieldSize.Bit0:
                        bitReference.flags |= 0x01;
                        if (requirement.Right.Value != 0)
                            bitReference.value |= 0x01;
                        break;
                    case FieldSize.Bit1:
                        bitReference.flags |= 0x02;
                        if (requirement.Right.Value != 0)
                            bitReference.value |= 0x02;
                        break;
                    case FieldSize.Bit2:
                        bitReference.flags |= 0x04;
                        if (requirement.Right.Value != 0)
                            bitReference.value |= 0x04;
                        break;
                    case FieldSize.Bit3:
                        bitReference.flags |= 0x08;
                        if (requirement.Right.Value != 0)
                            bitReference.value |= 0x08;
                        break;
                    case FieldSize.Bit4:
                        bitReference.flags |= 0x10;
                        if (requirement.Right.Value != 0)
                            bitReference.value |= 0x10;
                        break;
                    case FieldSize.Bit5:
                        bitReference.flags |= 0x20;
                        if (requirement.Right.Value != 0)
                            bitReference.value |= 0x20;
                        break;
                    case FieldSize.Bit6:
                        bitReference.flags |= 0x40;
                        if (requirement.Right.Value != 0)
                            bitReference.value |= 0x40;
                        break;
                    case FieldSize.Bit7:
                        bitReference.flags |= 0x80;
                        if (requirement.Right.Value != 0)
                            bitReference.value |= 0x80;
                        break;
                    case FieldSize.LowNibble:
                        bitReference.flags |= 0x0F;
                        bitReference.value |= (ushort)(requirement.Right.Value & 0x0F);
                        break;
                    case FieldSize.HighNibble:
                        bitReference.flags |= 0xF0;
                        bitReference.value |= (ushort)((requirement.Right.Value & 0x0F) << 4);
                        break;
                    case FieldSize.Byte:
                        bitReference.flags |= 0xFF;
                        bitReference.value |= (ushort)(requirement.Right.Value & 0xFF);
                        break;
                    default:
                        continue;
                }

                bitReference.requirements.Add(requirement);
            }

            foreach (var bitReference in references)
            {
                if ((bitReference.flags & 0xFF) == 0xFF)
                    MergeBits(group, bitReference, FieldSize.Byte);
                else if ((bitReference.flags & 0x0F) == 0x0F)
                    MergeBits(group, bitReference, FieldSize.LowNibble);
                else if ((bitReference.flags & 0xF0) == 0xF0)
                    MergeBits(group, bitReference, FieldSize.HighNibble);
            }
        }

        private static void MergeBits(IList<RequirementEx> group, BitReferences bitReference, FieldSize newSize)
        {
            uint newValue = bitReference.value;
            switch (newSize)
            {
                case FieldSize.LowNibble:
                    newValue &= 0x0F;
                    break;

                case FieldSize.HighNibble:
                    newValue = (newValue >> 4) & 0x0F;
                    break;
            }

            bool insert = true;
            int insertAt = 0;
            for (int i = group.Count - 1; i >= 0; i--)
            {
                if (group[i].Requirements.Count != 1)
                    continue;

                var requirement = group[i].Requirements[0];
                if (!bitReference.requirements.Contains(requirement))
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
                requirement.Left = new Field { Size = newSize, Type = bitReference.memoryType, Value = bitReference.address };
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

                        for (int i = 0; i < requirementEx.Requirements.Count; i++)
                        {
                            var requirement = requirementEx.Requirements[i];
                            if (requirement.Type == RequirementType.ResetNextIf)
                            {
                                hasResetNextIf = true;

                                var subclause = new RequirementEx();
                                int j = FindResetNextIfStart(requirementEx.Requirements, i);
                                for (int k = j; k <= i; k++)
                                    subclause.Requirements.Add(requirementEx.Requirements[k]);

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
            return Optimize(false);
        }

        public string OptimizeForSubClause()
        {
            return Optimize(true);
        }

        private string Optimize(bool forSubclause)
        {
            if (_core.Count == 0 && _alts.Count == 0)
                return "No requirements found.";

            // group complex expressions
            var groups = new List<List<RequirementEx>>(_alts.Count + 1);
            groups.Add(RequirementEx.Combine(_core));
            for (int i = 0; i < _alts.Count; i++)
                groups.Add(RequirementEx.Combine(_alts[i]));

            // combine multiple constants in an AddSource chain
            foreach (var group in groups)
                MergeAddSourceConstants(group);

            if (!forSubclause)
            {
                // convert PauseIfs not guarding anything to standard requirements
                NormalizePauseIfs(groups);

                // attempt to convert ResetNextIf into ResetIf and place in alt group
                NormalizeResetNextIfs(groups);
            }

            bool hasHitCount = HasHitCount(groups);
            if (!hasHitCount && !forSubclause)
            {
                // convert ResetIfs to standard requirements if there aren't any hits to reset
                if (NormalizeResetIfs(groups))
                {
                    // if at least one ResetIf was converted, check for unnecessary PauseIfs again
                    // in case one of them was guarding the affected ResetIf
                    NormalizePauseIfs(groups);
                }
            }

            // clamp memory reference comparisons to bounds; identify comparisons that can never
            // be true, or are always true; ensures constants are on the right
            foreach (var group in groups)
                NormalizeComparisons(group, forSubclause);

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
            if (groups[0].Count > 1)
                groups[0].RemoveAll(g => g.Evaluate() == true);

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

            if (!forSubclause)
            {
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
            }

            // success!
            return null;
        }

        private static int FindResetNextIfStart(IList<Requirement> requirements, int resetNextIfIndex)
        {
            int i = resetNextIfIndex;
            while (i > 0)
            {
                switch (requirements[i - 1].Type)
                {
                    case RequirementType.AddAddress:
                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                    case RequirementType.AndNext:
                    case RequirementType.OrNext:
                    case RequirementType.ResetNextIf:
                        // these have higher precedence than ResetNextIf, drag them with
                        i--;
                        continue;
                }

                break;
            }

            return i;
        }

        /// <summary>
        /// Squashes alt groups into the core group using AndNexts and OrNexts.
        /// </summary>
        internal ErrorExpression CollapseForSubClause()
        {
            var newCore = new List<Requirement>();

            // if a ResetIf is found, change it to a ResetNextIf and move it to the front
            if (_core.Any(r => r.Type == RequirementType.ResetIf))
            {
                for (int i = 0; i < _core.Count; i++)
                {
                    if (_core[i].Type == RequirementType.ResetIf)
                    {
                        int j = FindResetNextIfStart(_core, i);

                        for (int k = j; k <= i; k++)
                            newCore.Add(_core[k]);

                        _core.RemoveRange(j, i - j + 1);
                        i = j - 1;
                    }
                }

                // if ResetIfs are attached to something, change them to ResetNextIfs
                if (_core.Count != 0)
                {
                    foreach (var r in newCore)
                    {
                        if (r.Type == RequirementType.ResetIf)
                            r.Type = RequirementType.ResetNextIf;
                    }
                }
            }

            if (_alts.Count > 0)
            {
                // the last item cannot have its own HitCount as it will hold the HitCount for the group.
                // if necessary, find one without a HitCount and make it the last.
                if (_core.Count == 0)
                    EnsureLastGroupHasNoHitCount(_alts);
            }

            // merge the alts into the core group as an OrNext chain. only one AndNext chain can be generated
            // by the alt groups or the logic cannot be represented using only AndNext and OrNext conditions
            ICollection<Requirement> andNextAlt = null;
            foreach (var alt in _alts)
            {
                if (alt.Last().Type != RequirementType.None)
                    return new ErrorExpression(alt.Last().Type + " modifier not allowed in subclause");

                alt.Last().Type = RequirementType.OrNext;

                bool hasAndNext = false;
                foreach (var requirement in alt)
                {
                    if (requirement.Type == RequirementType.None)
                    {
                        requirement.Type = RequirementType.AndNext;
                        hasAndNext = true;
                    }
                    else if (requirement.Type == RequirementType.AndNext)
                    {
                        hasAndNext = true;
                    }
                }

                if (!hasAndNext)
                {
                    // single clause, just append it
                    newCore.AddRange(alt);
                }
                else
                {
                    // only one AndNext group allowed
                    if (andNextAlt != null)
                        return new ErrorExpression("Combination of &&s and ||s is too complex for subclause");

                    andNextAlt = alt;

                    // AndNext clause has to be the first part of the subclause
                    newCore.InsertRange(0, alt);
                }
            }

            // core group is another AndNext clause, but it can be appended to the set of OrNexts.
            //
            //   (d && e) && (a || b || c)   =>   (a || b || c) && (d && e)
            //
            if (_core.Count > 0)
            {
                if (newCore.Count > 0 && newCore.Last().Type != RequirementType.ResetNextIf)
                    newCore.Last().Type = RequirementType.AndNext;

                // turn the core group into an AndNext chain and append it to the end of the clause
                foreach (var requirement in _core)
                {
                    if (requirement.Type == RequirementType.None)
                        requirement.Type = RequirementType.AndNext;
                }

                newCore.AddRange(_core);
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
