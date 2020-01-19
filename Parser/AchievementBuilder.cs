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
                    else if (tokenizer.Match("N:"))
                        requirement.Type = RequirementType.AndNext;
                    else if (tokenizer.Match("M:"))
                        requirement.Type = RequirementType.Measured;
                    else if (tokenizer.Match("I:"))
                        requirement.Type = RequirementType.AddAddress;

                    requirement.Left = Field.Deserialize(tokenizer);

                    requirement.Operator = ReadOperator(tokenizer);
                    requirement.Right = Field.Deserialize(tokenizer);

                    switch (requirement.Type)
                    {
                        case RequirementType.AddSource:
                        case RequirementType.SubSource:
                        case RequirementType.AddAddress:
                            requirement.Operator = RequirementOperator.None;
                            requirement.Right = new Field();
                            break;

                        default:
                            if (requirement.Right.Size == FieldSize.None)
                                requirement.Right = new Field { Type = requirement.Right.Type, Size = requirement.Left.Size, Value = requirement.Right.Value };
                            break;
                    }

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
            }

            return RequirementOperator.None;
        }

        private static bool HasNewFeatures(IEnumerable<Requirement> requirements)
        {
            foreach (var requirement in requirements)
            {
                if (requirement.Type == RequirementType.AddAddress ||
                    requirement.Type == RequirementType.Measured)
                {
                    return true;
                }

                if (requirement.Left.Size == FieldSize.TByte ||
                    requirement.Right.Size == FieldSize.TByte)
                {
                    return true;
                }
            }

            return false;
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
            bool preferLegacyFormat = !HasNewFeatures(core);
            if (preferLegacyFormat)
            {
                foreach (var group in alts)
                {
                    if (HasNewFeatures(core))
                    {
                        preferLegacyFormat = false;
                        break;
                    }
                }
            }

            foreach (Requirement requirement in core)
            {
                SerializeRequirement(requirement, builder, preferLegacyFormat);
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
                    SerializeRequirement(requirement, builder, preferLegacyFormat);
                    builder.Append('_');
                }

                builder.Length--; // remove last _
            }

            return builder.ToString();
        }

        private static void SerializeRequirement(Requirement requirement, StringBuilder builder, bool preferLegacyFormat)
        {
            switch (requirement.Type)
            {
                case RequirementType.ResetIf: builder.Append("R:"); break;
                case RequirementType.PauseIf: builder.Append("P:"); break;
                case RequirementType.AddSource: builder.Append("A:"); break;
                case RequirementType.SubSource: builder.Append("B:"); break;
                case RequirementType.AddHits: builder.Append("C:"); break;
                case RequirementType.AndNext: builder.Append("N:"); break;
                case RequirementType.Measured: builder.Append("M:"); break;
                case RequirementType.AddAddress: builder.Append("I:"); break;
            }

            requirement.Left.Serialize(builder);

            switch (requirement.Type)
            {
                case RequirementType.AddSource:
                case RequirementType.SubSource:
                case RequirementType.AddAddress:
                    if (preferLegacyFormat)
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

        public static void AppendStringGroup(StringBuilder builder, IEnumerable<Requirement> group, 
            NumberFormat numberFormat, int wrapWidth = Int32.MaxValue, int indent = 14)
        {
            bool needsAmpersand = false;
            int width = wrapWidth - indent;

            var enumerator = group.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var requirement = enumerator.Current;

                var addSources = new StringBuilder();
                var subSources = new StringBuilder();
                var addHits = new StringBuilder();
                var andNext = new StringBuilder();
                var addAddress = new StringBuilder();
                bool isCombining = true;
                do
                {
                    switch (requirement.Type)
                    {
                        case RequirementType.AddSource:
                            requirement.Left.AppendString(addSources, numberFormat);
                            addSources.Append(" + ");
                            break;

                        case RequirementType.SubSource:
                            subSources.Append(" - ");
                            requirement.Left.AppendString(subSources, numberFormat);
                            break;

                        case RequirementType.AddHits:
                            requirement.AppendString(addHits, numberFormat);
                            addHits.Append(" || ");
                            break;

                        case RequirementType.AndNext:
                            requirement.AppendString(andNext, numberFormat);
                            andNext.Append(" && ");
                            break;

                        case RequirementType.AddAddress:
                            if (addAddress.Length > 0)
                            {
                                var builder2 = new StringBuilder();
                                requirement.Left.AppendString(builder2, numberFormat, addAddress.ToString());
                                addAddress = builder2;
                            }
                            else
                            {
                                requirement.Left.AppendString(addAddress, numberFormat);
                            }
                            addAddress.Append(" + ");
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

                if (addSources.Length == 0 && subSources.Length == 0 && addHits.Length == 0 && 
                    andNext.Length == 0 && addAddress.Length == 0)
                {
                    var result = requirement.Evaluate();
                    if (result == true)
                        definition.Append("always_true()");
                    else if (result == false)
                        definition.Append("always_false()");
                    else
                        requirement.AppendString(definition, numberFormat);
                }
                else
                {
                    requirement.AppendString(definition, numberFormat,
                        addSources.Length > 0 ? addSources.ToString() : null,
                        subSources.Length > 0 ? subSources.ToString() : null,
                        addHits.Length > 0 ? addHits.ToString() : null,
                        andNext.Length > 0 ? andNext.ToString() : null,
                        addAddress.Length > 0 ? addAddress.ToString() : null);
                }

                if (needsAmpersand)
                {
                    builder.Append(" && ");
                    width -= 4;
                }
                else
                {
                    needsAmpersand = true;
                }

                while (definition.Length > wrapWidth)
                {
                    var index = width;
                    while (index > 0 && definition[index] != ' ')
                        index--;

                    builder.Append(definition.ToString(), 0, index);
                    builder.AppendLine();
                    builder.Append(' ', indent);
                    definition.Remove(0, index);
                    width = wrapWidth - indent;
                }

                if (width - definition.Length < 0)
                {
                    builder.AppendLine();
                    builder.Append(' ', indent);
                    width = wrapWidth - indent;
                }
                else
                {
                    width -= definition.Length;
                }

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

        // ==== Optimize helpers ====

        private static void NormalizeComparisons(IList<RequirementEx> requirements)
        {
            var alwaysTrue = new List<RequirementEx>();
            var alwaysFalse = new List<RequirementEx>();

            foreach (var requirementEx in requirements)
            {
                if (requirementEx.Requirements.Count > 1)
                    continue;

                var requirement = requirementEx.Requirements[0];
                if (requirement.HitCount > 1)
                {
                    // any condition with a HitCount cannot be always true or false, evaluate the condition 
                    // without the HitCount, and if that is always true or false, replace with the equivalent
                    var clone = new Requirement
                    {
                        Left = requirement.Left,
                        Operator = requirement.Operator,
                        Right = requirement.Right,
                    };

                    var result = clone.Evaluate();
                    if (result == true)
                    {
                        var alwaysTrueRequirement = AlwaysTrueFunction.CreateAlwaysTrueRequirement();
                        requirement.Left = alwaysTrueRequirement.Left;
                        requirement.Operator = alwaysTrueRequirement.Operator;
                        requirement.Right = alwaysTrueRequirement.Right;
                    }
                    else if (result == false)
                    {
                        var alwaysFalseRequirement = AlwaysFalseFunction.CreateAlwaysFalseRequirement();
                        requirement.Left = alwaysFalseRequirement.Left;
                        requirement.Operator = alwaysFalseRequirement.Operator;
                        requirement.Right = alwaysFalseRequirement.Right;
                    }
                }
                else
                {
                    var result = requirement.Evaluate();
                    if (result == true)
                    {
                        alwaysTrue.Add(requirementEx);
                        continue;
                    }
                    else if (result == false)
                    {
                        alwaysFalse.Add(requirementEx);
                        continue;
                    }
                }

                if (requirement.Right.Type != FieldType.Value)
                {
                    // if the comparison is between two memory addresses, we cannot determine the relationship of the values
                    if (requirement.Left.Type != FieldType.Value)
                        continue;

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
                            alwaysFalse.Add(requirementEx);
                            continue;

                        case RequirementOperator.LessThanOrEqual: // n <= 0 -> n == 0
                            requirement.Operator = RequirementOperator.Equal;
                            break;

                        case RequirementOperator.GreaterThan: // n > 0 -> n != 0
                            requirement.Operator = RequirementOperator.NotEqual;
                            break;

                        case RequirementOperator.GreaterThanOrEqual: // n >= 0 -> always true
                            alwaysTrue.Add(requirementEx);
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
                }

                if (requirement.Right.Value >= max)
                {
                    switch (requirement.Operator)
                    {
                        case RequirementOperator.GreaterThan: // n > max -> always false
                            alwaysFalse.Add(requirementEx);
                            break;

                        case RequirementOperator.GreaterThanOrEqual: // n >= max -> n == max
                            if (requirement.Right.Value == max)
                                requirement.Operator = RequirementOperator.Equal;
                            else
                                alwaysFalse.Add(requirementEx);
                            break;

                        case RequirementOperator.LessThanOrEqual: // n <= max -> always true
                            alwaysTrue.Add(requirementEx);
                            break;

                        case RequirementOperator.LessThan: // n < max -> n != max
                            if (requirement.Right.Value == max)
                                requirement.Operator = RequirementOperator.NotEqual;
                            else
                                alwaysTrue.Add(requirementEx);
                            break;

                        case RequirementOperator.Equal:
                            if (requirement.Right.Value > max)
                                alwaysFalse.Add(requirementEx);
                            break;

                        case RequirementOperator.NotEqual:
                            if (requirement.Right.Value > max)
                                alwaysTrue.Add(requirementEx);
                            break;
                    }
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
                            if (alwaysTrue.Contains(requirement))
                            {
                                // always True PauseIf ensures the group is always paused, so just replace the entire 
                                // group with an always_false().
                                requirements.Clear();
                                i = 0;
                            }
                            else if (alwaysFalse.Contains(requirement))
                            {
                                requirements.RemoveAt(i);
                            }
                            break;

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
                foreach (var requirementEx in group)
                {
                    var requirement = requirementEx.Requirements.Last();

                    if (requirement.Type == RequirementType.PauseIf || requirement.Type == RequirementType.ResetIf)
                    {
                        if (requirementEx.Requirements.Any(r => r.Type == RequirementType.AndNext))
                        {
                            // the inverse of an AndNext is OrNext, which isn't currently possible. and we've already
                            // expanded all of the OR clauses into alt groups, so another round of expansion at this time
                            // is unreasonable. just leave the resetif/pauseif.
                        }
                        else
                        {
                            requirement.Type = RequirementType.None;
                            requirement.Operator = Requirement.GetOpposingOperator(requirement.Operator);
                        }
                    }
                }
            }
        }

        private static RequirementEx MergeRequirements(RequirementEx into, RequirementEx from)
        {
            for (int i = 0; i < into.Requirements.Count; i++)
            {
                if (into.Requirements[i].HitCount > 0 && from.Requirements[i].HitCount > into.Requirements[i].HitCount)
                    into.Requirements[i].HitCount = from.Requirements[i].HitCount;
            }

            return into;
        }

        private static bool MergeRequirements(RequirementEx first, RequirementEx second, ConditionalOperation condition, out RequirementEx merged)
        {
            merged = null;
            Requirement left, right;

            // make sure all the lefts are the same
            for (int i = 0; i < first.Requirements.Count; i++)
            {
                left = first.Requirements[i];
                right = second.Requirements[i];

                if (left.Type != right.Type)
                    return false;

                if (left.HitCount != right.HitCount && (left.HitCount == 0 || right.HitCount == 0))
                    return false;

                if (left.Left != right.Left)
                    return false;
            }

            // if the operator and the final right are the same, we have an exact match
            left = first.Requirements[first.Requirements.Count - 1];
            right = second.Requirements[second.Requirements.Count - 1];
            if (left.Operator == right.Operator && left.Right == right.Right)
            {
                merged = MergeRequirements(first, second);
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
                    merged = MergeRequirements(second, first);
                    return true;
                }

                if (useLeft)
                {
                    merged = MergeRequirements(first, second);
                    return true;
                }

                if (newOperator != RequirementOperator.None)
                {
                    merged = MergeRequirements(first, second);
                    merged.Requirements.Last().Operator = newOperator;
                    return true;
                }
            }
            else
            {
                if (useRight)
                {
                    merged = MergeRequirements(first, second);
                    return true;
                }

                if (useLeft)
                {
                    merged = MergeRequirements(second, first);
                    return true;
                }

                if (newOperator != RequirementOperator.None)
                {
                    merged = MergeRequirements(first, second);
                    merged.Requirements.Last().Operator = RequirementOperator.Equal;
                    return true;
                }
            }

            return false;
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
            bool commonPauseIf = false;
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
                {
                    requirementsFoundInAll.Add(requirementI);

                    if (requirementI.Requirements.Last().Type == RequirementType.PauseIf)
                        commonPauseIf = true;
                }
            }

            // PauseIf only affects the alt group that it's in, so it can only be promoted if the entire alt group is promoted
            if (commonPauseIf)
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
                            // always_true has special meaning and shouldn't be eliminated if present in the core group
                            if (group[j].Requirements.Count > 1 || group[j].Requirements[0].Evaluate() != true)
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
                            if (merged == null)
                            {
                                // conflicting requirements, replace the entire requirement set with an always_false()
                                group.Clear();
                                group.Add(new RequirementEx());
                                group.Last().Requirements.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
                                return;
                            }

                            group[j] = merged;
                            group.RemoveAt(i);
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

        private class RequirementEx
        {
            public RequirementEx()
            {
                Requirements = new List<Requirement>();
            }

            public List<Requirement> Requirements { get; private set; }

            public bool HasHitCount
            {
                get
                {
                    foreach (var requirement in Requirements)
                    {
                        if (requirement.HitCount > 0)
                            return true;
                    }

                    return false;
                }
            }

            public override string ToString()
            {
                var builder = new StringBuilder();
                foreach (var requirement in Requirements)
                    requirement.AppendString(builder, NumberFormat.Hexadecimal);

                return builder.ToString();
            }

            public override bool Equals(object obj)
            {
                var that = obj as RequirementEx;
                if (ReferenceEquals(that, null))
                    return false;

                if (that.Requirements.Count != Requirements.Count)
                    return false;

                for (int i = 0; i < Requirements.Count; i++)
                {
                    if (that.Requirements[i] != Requirements[i])
                        return false;
                }

                return true;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static bool operator ==(RequirementEx left, RequirementEx right)
            {
                if (ReferenceEquals(left, null))
                    return ReferenceEquals(right, null);

                return left.Equals(right);
            }

            public static bool operator !=(RequirementEx left, RequirementEx right)
            {
                if (ReferenceEquals(left, null))
                    return !ReferenceEquals(right, null);

                return !left.Equals(right);
            }
        }

        private static List<RequirementEx> Process(ICollection<Requirement> requirements)
        {
            var group = new List<RequirementEx>();

            bool combiningRequirement = false;
            foreach (var requirement in requirements)
            {
                if (combiningRequirement)
                    group.Last().Requirements.Add(requirement);

                if (requirement.IsCombining)
                {
                    if (combiningRequirement)
                        continue;

                    combiningRequirement = true;
                }
                else if (combiningRequirement)
                {
                    combiningRequirement = false;
                    continue;
                }

                group.Add(new RequirementEx());
                group.Last().Requirements.Add(requirement);
            }

            return group;
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
                if (requirement.Type == RequirementType.Measured)
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

        public string Optimize()
        {
            if (_core.Count == 0 && _alts.Count == 0)
                return "No requirements found.";

            // group complex expressions
            var groups = new List<List<RequirementEx>>(_alts.Count + 1);
            groups.Add(Process(_core));
            for (int i = 0; i < _alts.Count; i++)
                groups.Add(Process(_alts[i]));

            // convert ResetIfs and PauseIfs without HitCounts to standard requirements
            bool hasHitCount = HasHitCount(groups);
            NormalizeNonHitCountResetAndPauseIfs(groups, hasHitCount);

            // normalize BitX() methods to compare against 1
            for (int i = groups.Count - 1; i >= 0; i--)
                NormalizeComparisons(groups[i]);

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

            // convert back to flattened expressions
            _core.Clear();
            Unprocess(_core, groups[0]);

            for (int i = 1; i < groups.Count; i++)
            {
                _alts[i - 1].Clear();
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
    }
}
