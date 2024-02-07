using Jamiras.Components;
using System;
using System.Text;

namespace RATools.Data
{
    /// <summary>
    /// Defines a single requirement within an <see cref="Achievement"/>.
    /// </summary>
    public class Requirement
    {
        /// <summary>
        /// Gets or sets the left part of the requirement.
        /// </summary>
        public Field Left { get; set; }

        /// <summary>
        /// Gets or sets the right part of the requirement.
        /// </summary>
        public Field Right { get; set; }

        /// <summary>
        /// Gets or sets the requirement type.
        /// </summary>
        public RequirementType Type { get; set; }

        /// <summary>
        /// Gets whether or not the requirement affects the following requirement.
        /// </summary>
        public bool IsCombining
        {
            get
            {
                switch (Type)
                {
                    case RequirementType.AddHits:
                    case RequirementType.SubHits:
                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                    case RequirementType.AndNext:
                    case RequirementType.OrNext:
                    case RequirementType.AddAddress:
                    case RequirementType.ResetNextIf:
                        return true;

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets whether or not the requirement can be scaled.
        /// </summary>
        public bool IsScalable
        {
            get
            {
                switch (Type)
                {
                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                    case RequirementType.AddAddress:
                        return true;

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets whether or not the requirement is comparing two values.
        /// </summary>
        public bool IsComparison
        {
            get
            {
                switch (Operator)
                {
                    case RequirementOperator.Equal:
                    case RequirementOperator.NotEqual:
                    case RequirementOperator.LessThan:
                    case RequirementOperator.LessThanOrEqual:
                    case RequirementOperator.GreaterThan:
                    case RequirementOperator.GreaterThanOrEqual:
                        return true;

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets whether or not the requirement can be measured.
        /// </summary>
        public bool IsMeasured
        {
            get
            {
                switch (Type)
                {
                    case RequirementType.Measured:
                    case RequirementType.MeasuredPercent:
                        return true;

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets or sets the requirement operator.
        /// </summary>
        public RequirementOperator Operator { get; set; }

        /// <summary>
        /// Gets or sets the required hit count for the requirement.
        /// </summary>
        /// <remarks>
        /// <c>0</c> means the requirement must be true at the time the achievement triggers.
        /// Any other value indicates the number of frames a requirement must be true before the achievement can trigger.
        /// </remarks>
        public uint HitCount { get; set; }

        [Flags]
        public enum Optimizations
        {
            None = 0,
            ConvertOrNextToAlt = 1,
        }
        /// <summary>
        /// Gets or sets which optimizations should be ignored by this requirement.
        /// </summary>
        public Optimizations DisabledOptimizations { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder, NumberFormat.Decimal);
            return builder.ToString();
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder"/>.
        /// </summary>
        public void AppendString(StringBuilder builder, NumberFormat numberFormat, 
            string addSources = null, string subSources = null, string addHits = null, 
            string andNext = null, string addAddress = null, string measuredIf = null,
            string resetNextIf = null)
        {
            switch (Type)
            {
                case RequirementType.ResetIf:
                    builder.Append("never(");
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);
                    builder.Append(')');
                    break;

                case RequirementType.PauseIf:
                    if (resetNextIf != null)
                    {
                        var comparison = new StringBuilder();
                        AppendRepeatedCondition(comparison, numberFormat, addSources, subSources, addHits, andNext, addAddress, null);
                        if (HitCount == 1)
                        {
                            comparison.Remove(0, 5); // "once("
                            comparison.Length--;     // ")"
                        }

                        builder.Append("disable_when(");
                        builder.Append(comparison.ToString());
                        builder.Append(", until=");
                        builder.Append(resetNextIf);
                        builder.Append(')');
                    }
                    else
                    {
                        builder.Append("unless(");
                        AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);
                        builder.Append(')');
                    }
                    break;

                case RequirementType.Measured:
                case RequirementType.MeasuredPercent:
                    builder.Append("measured(");

                    // if there's no HitTarget and there's an AndNext or OrNext clause, assume we're counting
                    // complex conditions for a Value clause and wrap it in a "tally(0, ...)"
                    if (HitCount == 0 && !String.IsNullOrEmpty(andNext))
                    {
                        var measuredClause = new StringBuilder();
                        AppendRepeatedCondition(measuredClause, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);

                        builder.Append("tally(0, ");
                        builder.Append(RemoveOuterParentheses(measuredClause.ToString()));
                        builder.Append(')');
                    }
                    else
                    {
                        AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);
                    }

                    if (measuredIf != null)
                    {
                        builder.Append(", when=");
                        builder.Append(measuredIf);
                    }
                    if (Type == RequirementType.MeasuredPercent)
                        builder.Append(", format=\"percent\"");
                    builder.Append(')');
                    break;

                case RequirementType.MeasuredIf:
                    // this is displayed in the achievement details page and doesn't accurately represent the syntax
                    builder.Append("measured_if(");
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);
                    builder.Append(')');
                    break;

                case RequirementType.AddAddress:
                    // this is displayed in the achievement details page and doesn't accurately represent the syntax
                    builder.Append("addaddress(");
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);
                    builder.Append(") ->");
                    break;

                case RequirementType.ResetNextIf:
                    // this is displayed in the achievement details page and doesn't accurately represent the syntax
                    builder.Append("resetnext_if(");
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);
                    builder.Append(')');
                    break;

                case RequirementType.Trigger:
                    builder.Append("trigger_when(");
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);
                    builder.Append(')');
                    break;

                default:
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress, resetNextIf);
                    break;
            }
        }

        private void AppendRepeatedCondition(StringBuilder builder, NumberFormat numberFormat,
            string addSources, string subSources, string addHits, string andNext, string addAddress, string resetNextIf)
        {
            if (HitCount == 0 && addHits == null)
            {
                bool wrapInParenthesis = false;

                if (!string.IsNullOrEmpty(andNext) && andNext[0] != '(' &&
                    (andNext.Contains(" && ") || andNext.Contains(" || ")))
                {
                    wrapInParenthesis = true;
                }

                if (wrapInParenthesis)
                    builder.Append('(');

                AppendCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress);

                if (resetNextIf != null)
                {
                    builder.Append(" && never(");
                    builder.Append(RemoveOuterParentheses(resetNextIf));
                    builder.Append(")");
                }

                if (wrapInParenthesis)
                    builder.Append(')');
            }
            else
            {
                if (HitCount == 1)
                    builder.Append("once(");
                else if (addHits != null)
                    builder.AppendFormat("tally({0}, ", HitCount);
                else
                    builder.AppendFormat("repeated({0}, ", HitCount);

                bool needsParenthesis = (resetNextIf != null && andNext != null && andNext.LastIndexOf(" || ") > andNext.LastIndexOf(')'));
                if (needsParenthesis)
                    builder.Append('(');

                AppendCondition(builder, numberFormat, addSources, subSources, addHits, andNext, addAddress);

                if (resetNextIf != null)
                {
                    if (needsParenthesis)
                        builder.Append(')');

                    builder.Append(" && never(");
                    builder.Append(RemoveOuterParentheses(resetNextIf));
                    builder.Append(')');
                }

                builder.Append(')');
            }
        }

        private static string RemoveOuterParentheses(string input)
        {
            while (input.Length > 2 && input[0] == '(' && input[input.Length - 1] == ')')
                input = input.Substring(1, input.Length - 2);

            return input;
        }

        internal void AppendCondition(StringBuilder builder, NumberFormat numberFormat, 
            string addSources = null, string subSources = null, string addHits = null, 
            string andNext = null, string addAddress = null)
        {
            if (!string.IsNullOrEmpty(andNext))
            {
                builder.Append(andNext);
            }

            if (!string.IsNullOrEmpty(addSources))
            {
                builder.Append('(');
                builder.Append(addSources);
            }
            else if (!string.IsNullOrEmpty(subSources))
            {
                builder.Append('(');
            }
            else if (!string.IsNullOrEmpty(addHits))
            {
                builder.Append(addHits);
            }

            string suffix = null;
            switch (Type)
            {
                case RequirementType.AddSource:
                    Left.AppendString(builder, numberFormat);
                    suffix = " + ";
                    break;

                case RequirementType.SubSource:
                    builder.Append(" - ");
                    Left.AppendString(builder, numberFormat);
                    break;

                default:
                    if (Operator != RequirementOperator.None &&
                        string.IsNullOrEmpty(subSources) && string.IsNullOrEmpty(addSources))
                    {
                        var clone = new Requirement
                        {
                            Left = this.Left,
                            Operator = this.Operator,
                            Right = this.Right
                        };
                        var result = clone.Evaluate();
                        if (result == true)
                        {
                            builder.Append("always_true()");
                            return;
                        }
                        else if (result == false)
                        {
                            builder.Append("always_false()");
                            return;
                        }
                    }

                    Left.AppendString(builder, numberFormat, addAddress);
                    break;
            }

            // scaling operators need to be appended before chained operations
            switch (Operator)
            {
                case RequirementOperator.Multiply:
                    builder.Append(" * ");
                    Right.AppendString(builder, numberFormat, addAddress);
                    break;
                case RequirementOperator.Divide:
                    builder.Append(" / ");
                    Right.AppendString(builder, numberFormat, addAddress);
                    break;
                case RequirementOperator.BitwiseAnd:
                    builder.Append(" & ");
                    Right.AppendString(builder, numberFormat, addAddress);
                    break;
            }

            // append chained operations
            if (!string.IsNullOrEmpty(subSources))
            {
                builder.Append(subSources);
                builder.Append(')');
            }
            else if (!string.IsNullOrEmpty(addSources))
            {
                builder.Append(')');
            }

            // handle comparison operators
            switch (Operator)
            {
                case RequirementOperator.Equal:
                    builder.Append(" == ");
                    break;
                case RequirementOperator.NotEqual:
                    builder.Append(" != ");
                    break;
                case RequirementOperator.LessThan:
                    builder.Append(" < ");
                    break;
                case RequirementOperator.LessThanOrEqual:
                    builder.Append(" <= ");
                    break;
                case RequirementOperator.GreaterThan:
                    builder.Append(" > ");
                    break;
                case RequirementOperator.GreaterThanOrEqual:
                    builder.Append(" >= ");
                    break;

                case RequirementOperator.Multiply:
                case RequirementOperator.Divide:
                case RequirementOperator.BitwiseAnd:
                    // handled above, treat like none

                case RequirementOperator.None:
                    if (suffix != null)
                        builder.Append(suffix);
                    return;
            }

            Right.AppendString(builder, numberFormat, addAddress);

            if (suffix != null)
                builder.Append(suffix);
        }

        public void Serialize(StringBuilder builder, double minimumVersion = 0.0, int addressWidth = 6)
        {
            switch (Type)
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

            Left.Serialize(builder, addressWidth);

            if (IsScalable)
            {
                switch (Operator)
                {
                    case RequirementOperator.Multiply: builder.Append('*'); break;
                    case RequirementOperator.Divide: builder.Append('/'); break;
                    case RequirementOperator.BitwiseAnd: builder.Append('&'); break;
                    case RequirementOperator.BitwiseXor: builder.Append('^'); break;
                    default:
                        if (minimumVersion < 0.77)
                            builder.Append("=0");
                        return;
                }

                Right.Serialize(builder, addressWidth);
            }
            else
            {
                switch (Operator)
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
                    case RequirementOperator.BitwiseXor: builder.Append('^'); break;
                    case RequirementOperator.None: return;
                }

                Right.Serialize(builder, addressWidth);
            }

            if (HitCount > 0)
            {
                builder.Append('.');
                builder.Append(HitCount);
                builder.Append('.');
            }
        }

        public double MinimumVersion()
        {
            double minVer = 0.30;

            switch (Type)
            {
                case RequirementType.AndNext:
                    // 0.76 21 Jun 2019
                    minVer = 0.76;
                    break;

                case RequirementType.AddAddress:
                case RequirementType.Measured:
                    // 0.77 30 Nov 2019
                    minVer = 0.77;
                    break;

                case RequirementType.MeasuredIf:
                case RequirementType.OrNext:
                    // 0.78 18 May 2020
                    minVer = 0.78;
                    break;

                case RequirementType.ResetNextIf:
                case RequirementType.Trigger:
                case RequirementType.SubHits:
                    // 0.79 22 May 2021
                    minVer = 0.79;
                    break;

                case RequirementType.MeasuredPercent:
                    // 1.0 29 Jan 2022
                    minVer = 1.0;
                    break;

                default:
                    break;
            }

            switch (Operator)
            {
                case RequirementOperator.Multiply:
                case RequirementOperator.Divide:
                case RequirementOperator.BitwiseAnd:
                    // 0.78 18 May 2020
                    if (minVer < 0.78)
                        minVer = 0.78;
                    break;

                case RequirementOperator.BitwiseXor:
                    // 1.1 15 Nov 2022
                    if (minVer < 1.1)
                        minVer = 1.1;
                    break;

                default:
                    break;
            }

            foreach (var type in new FieldType[] { Left.Type, Right.Type })
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

            foreach (var size in new FieldSize[] { Left.Size, Right.Size })
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
                        // 1.0 29 Jan 2022
                        if (minVer < 1.0)
                            minVer = 1.0;
                        break;

                    case FieldSize.LittleEndianMBF32:
                        // 1.1 15 Nov 2022
                        if (minVer < 1.1)
                            minVer = 1.1;
                        break;

                    case FieldSize.BigEndianFloat:
                        // 1.3 TBD
                        if (minVer < 1.3)
                            minVer = 1.3;
                        break;

                    default:
                        break;
                }
            }

            return minVer;
        }

        /// <summary>
        /// Determines if the requirement always evaluates true or false.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the requirement is always true, <c>false</c> if it's always false, or 
        /// <c>null</c> if the result cannot be determined at this time.
        /// </returns>
        public bool? Evaluate()
        {
            bool result = false;

            if (Left.IsMemoryReference || Right.IsMemoryReference)
            {
                // memory reference - can only be equal or not equal to same memory reference
                if (Left.Value != Right.Value || Left.Type != Right.Type || Left.Size != Right.Size)
                    return null;

                // same memory reference in the same frame is always equal
                switch (Operator)
                {
                    case RequirementOperator.Equal:
                    case RequirementOperator.GreaterThanOrEqual:
                    case RequirementOperator.LessThanOrEqual:
                        result = true;
                        break;

                    case RequirementOperator.NotEqual:
                    case RequirementOperator.LessThan:
                    case RequirementOperator.GreaterThan:
                        result = false;
                        break;

                    default:
                        return null;
                }
            }
            else if (Left.Type == FieldType.Float || Right.Type == FieldType.Float)
            {
                float leftFloat = (Left.Type == FieldType.Float) ? Left.Float : (float)Left.Value;
                float rightFloat = (Right.Type == FieldType.Float) ? Right.Float : (float)Right.Value;

                // comparing constants
                switch (Operator)
                {
                    case RequirementOperator.Equal:
                        result = (leftFloat == rightFloat);
                        break;
                    case RequirementOperator.NotEqual:
                        result = (leftFloat != rightFloat);
                        break;
                    case RequirementOperator.LessThan:
                        result = (leftFloat < rightFloat);
                        break;
                    case RequirementOperator.LessThanOrEqual:
                        result = (leftFloat <= rightFloat);
                        break;
                    case RequirementOperator.GreaterThan:
                        result = (leftFloat > rightFloat);
                        break;
                    case RequirementOperator.GreaterThanOrEqual:
                        result = (leftFloat >= rightFloat);
                        break;
                    default:
                        return null;
                }
            }
            else
            {
                // comparing constants
                switch (Operator)
                {
                    case RequirementOperator.Equal:
                        result = (Left.Value == Right.Value);
                        break;
                    case RequirementOperator.NotEqual:
                        result = (Left.Value != Right.Value);
                        break;
                    case RequirementOperator.LessThan:
                        result = (Left.Value < Right.Value);
                        break;
                    case RequirementOperator.LessThanOrEqual:
                        result = (Left.Value <= Right.Value);
                        break;
                    case RequirementOperator.GreaterThan:
                        result = (Left.Value > Right.Value);
                        break;
                    case RequirementOperator.GreaterThanOrEqual:
                        result = (Left.Value >= Right.Value);
                        break;
                    default:
                        return null;
                }
            }

            // even if the condition is always true, if there's a target hit count, it won't be true initially.
            if (result && HitCount > 1)
                return null;

            return result;
        }

        /// <summary>
        /// Creates a copy of the <see cref="Requirement"/>.
        /// </summary>
        public Requirement Clone()
        {
            return (Requirement)MemberwiseClone();
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            var that = obj as Requirement;
            if (ReferenceEquals(that, null))
                return false;

            if (that.Type != this.Type || that.Operator != this.Operator || that.HitCount != this.HitCount)
                return false;

            return (that.Left == this.Left && that.Right == this.Right);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Determines if two <see cref="Requirement"/>s are equivalent.
        /// </summary>
        public static bool operator ==(Requirement left, Requirement right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            return left.Equals(right);
        }

        /// <summary>
        /// Determines if two <see cref="Requirement"/>s are not equivalent.
        /// </summary>
        public static bool operator !=(Requirement left, Requirement right)
        {
            if (ReferenceEquals(left, right))
                return false;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return true;

            return !left.Equals(right);
        }

        internal static Requirement Deserialize(Tokenizer tokenizer)
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

            return requirement;
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

        internal static RequirementOperator ReadOperator(Tokenizer tokenizer)
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


        /// <summary>
        /// Gets the logically opposing operator.
        /// </summary>
        public static RequirementOperator GetOpposingOperator(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal: return RequirementOperator.NotEqual;
                case RequirementOperator.NotEqual: return RequirementOperator.Equal;
                case RequirementOperator.LessThan: return RequirementOperator.GreaterThanOrEqual;
                case RequirementOperator.LessThanOrEqual: return RequirementOperator.GreaterThan;
                case RequirementOperator.GreaterThan: return RequirementOperator.LessThanOrEqual;
                case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.LessThan;
                default: return RequirementOperator.None;
            }
        }

        /// <summary>
        /// Gets the equivalent operator if the operands are switched.
        /// </summary>
        public static RequirementOperator GetReversedRequirementOperator(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal: return RequirementOperator.Equal;
                case RequirementOperator.NotEqual: return RequirementOperator.NotEqual;
                case RequirementOperator.LessThan: return RequirementOperator.GreaterThan;
                case RequirementOperator.LessThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                case RequirementOperator.GreaterThan: return RequirementOperator.LessThan;
                case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.LessThanOrEqual;
                default: return RequirementOperator.None;
            }
        }

        /// <summary>
        /// Creates a requirement that will always evaluate true.
        /// </summary>
        public static Requirement CreateAlwaysTrueRequirement()
        {
            var requirement = new Requirement();
            requirement.Left = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 1 };
            requirement.Operator = RequirementOperator.Equal;
            requirement.Right = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 1 };
            return requirement;
        }

        /// <summary>
        /// Creates a requirement that will always evaluate false.
        /// </summary>
        public static Requirement CreateAlwaysFalseRequirement()
        {
            var requirement = new Requirement();
            requirement.Left = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 0 };
            requirement.Operator = RequirementOperator.Equal;
            requirement.Right = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 1 };
            return requirement;
        }
    }

    /// <summary>
    /// Specifies how the <see cref="Requirement.Left"/> and <see cref="Requirement.Right"/> values should be compared.
    /// </summary>
    public enum RequirementOperator
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        None = 0,

        /// <summary>
        /// The left and right values are equivalent.
        /// </summary>
        Equal,

        /// <summary>
        /// The left and right values are not equivalent.
        /// </summary>
        NotEqual,

        /// <summary>
        /// The left value is less than the right value.
        /// </summary>
        LessThan,

        /// <summary>
        /// The left value is less than or equal to the right value.
        /// </summary>
        LessThanOrEqual,

        /// <summary>
        /// The left value is greater than the right value.
        /// </summary>
        GreaterThan,

        /// <summary>
        /// The left value is greater than or equal to the right value.
        /// </summary>
        GreaterThanOrEqual,

        /// <summary>
        /// The left value is multiplied by the right value. (combining conditions only)
        /// </summary>
        Multiply,

        /// <summary>
        /// The left value is divided by the right value. (combining conditions only)
        /// </summary>
        Divide,

        /// <summary>
        /// The left value is masked by the right value. (combining conditions only)
        /// </summary>
        BitwiseAnd,

        /// <summary>
        /// The bits in the left value are toggled by the bits in the right value. (combining conditions only)
        /// </summary>
        BitwiseXor,
    }

    /// <summary>
    /// Special requirement behaviors
    /// </summary>
    public enum RequirementType
    {
        /// <summary>
        /// No special behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Resets any HitCounts in the current requirement group if true.
        /// </summary>
        ResetIf,

        /// <summary>
        /// Pauses processing of the achievement if true.
        /// </summary>
        PauseIf,

        /// <summary>
        /// Adds the Left part of the requirement to the Left part of the next requirement.
        /// </summary>
        AddSource,

        /// <summary>
        /// Subtracts the Left part of the next requirement from the Left part of the requirement.
        /// </summary>
        SubSource,

        /// <summary>
        /// Adds the HitsCounts from this requirement to the next requirement.
        /// </summary>
        AddHits,

        /// <summary>
        /// Subtracts the HitsCounts from this requirement from the next requirement.
        /// </summary>
        SubHits,

        /// <summary>
        /// This requirement must also be true for the next requirement to be true.
        /// </summary>
        AndNext,

        /// <summary>
        /// This requirement or the following requirement must be true for the next requirement to be true.
        /// </summary>
        OrNext,

        /// <summary>
        /// Meta-flag indicating that this condition tracks progress as a raw value.
        /// </summary>
        Measured,

        /// <summary>
        /// Meta-flag indicating that this condition must be true to track progress.
        /// </summary>
        MeasuredIf,

        /// <summary>
        /// Adds the Left part of the requirement to the addresses in the next requirement.
        /// </summary>
        AddAddress,

        /// <summary>
        /// Resets any HitCounts on the next requirement group if true.
        /// </summary>
        ResetNextIf,

        /// <summary>
        /// While all non-Trigger conditions are true, a challenge indicator will be displayed.
        /// </summary>
        Trigger,

        /// <summary>
        /// Meta-flag indicating that this condition tracks progress as a percentage.
        /// </summary>
        MeasuredPercent,
    }
}
