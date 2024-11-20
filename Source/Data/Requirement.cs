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
                return Type.IsCombining();
            }
        }

        /// <summary>
        /// Gets whether or not the requirement is comparing two values.
        /// </summary>
        public bool IsComparison
        {
            get
            {
                return Operator.IsComparison();
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

            if (Type != RequirementType.None)
            {
                builder.Append(Type.ToString());
                builder.Append(' ');
            }

            Left.AppendString(builder, NumberFormat.Decimal);

            if (Operator != RequirementOperator.None)
            {
                builder.Append(' ');
                builder.Append(Operator.ToOperatorString());
                builder.Append(' ');

                Right.AppendString(builder, NumberFormat.Decimal);
            }

            if (HitCount > 0)
                builder.AppendFormat(" ({0})", HitCount);

            return builder.ToString();
        }

        public void Serialize(StringBuilder builder, SerializationContext serializationContext)
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
                case RequirementType.Remember: builder.Append("K:"); break;
            }

            Left.Serialize(builder, serializationContext);

            if (Type.IsScalable())
            {
                switch (Operator)
                {
                    case RequirementOperator.Add: builder.Append('+'); break;
                    case RequirementOperator.Subtract: builder.Append('-'); break;
                    case RequirementOperator.Multiply: builder.Append('*'); break;
                    case RequirementOperator.Divide: builder.Append('/'); break;
                    case RequirementOperator.Modulus: builder.Append('%'); break;
                    case RequirementOperator.BitwiseAnd: builder.Append('&'); break;
                    case RequirementOperator.BitwiseXor: builder.Append('^'); break;
                    default:
                        if (serializationContext.MinimumVersion < Version._0_77)
                            builder.Append("=0");
                        return;
                }

                Right.Serialize(builder, serializationContext);
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
                    case RequirementOperator.Modulus: builder.Append('%'); break;
                    case RequirementOperator.BitwiseAnd: builder.Append('&'); break;
                    case RequirementOperator.BitwiseXor: builder.Append('^'); break;
                    case RequirementOperator.None: return;
                }

                Right.Serialize(builder, serializationContext);
            }

            if (HitCount > 0)
            {
                builder.Append('.');
                builder.Append(HitCount);
                builder.Append('.');
            }
        }

        public SoftwareVersion MinimumVersion()
        {
            var minimumVersion = Version.MinimumVersion;

            switch (Type)
            {
                case RequirementType.ResetIf:
                case RequirementType.PauseIf:
                    if (HitCount > 0)
                        minimumVersion = Version._0_73;
                    break;

                case RequirementType.AndNext:
                    minimumVersion = Version._0_76;
                    break;

                case RequirementType.AddAddress:
                case RequirementType.Measured:
                    minimumVersion = Version._0_77;
                    break;

                case RequirementType.MeasuredIf:
                case RequirementType.OrNext:
                    minimumVersion = Version._0_78;
                    break;

                case RequirementType.ResetNextIf:
                case RequirementType.Trigger:
                case RequirementType.SubHits:
                    minimumVersion = Version._0_79;
                    break;

                case RequirementType.MeasuredPercent:
                    minimumVersion = Version._1_0;
                    break;

                case RequirementType.Remember:
                    minimumVersion = Version._1_3_1;
                    break;

                default:
                    break;
            }

            switch (Operator)
            {
                case RequirementOperator.Multiply:
                case RequirementOperator.Divide:
                case RequirementOperator.BitwiseAnd:
                    minimumVersion = minimumVersion.OrNewer(Version._0_78);
                    break;

                case RequirementOperator.BitwiseXor:
                    minimumVersion = minimumVersion.OrNewer(Version._1_1);
                    break;

                case RequirementOperator.Add:
                case RequirementOperator.Subtract:
                case RequirementOperator.Modulus:
                    minimumVersion = minimumVersion.OrNewer(Version._1_3_1);
                    break;

                default:
                    break;
            }

            foreach (var type in new FieldType[] { Left.Type, Right.Type })
            {
                switch (type)
                {
                    case FieldType.PriorValue:
                        minimumVersion = minimumVersion.OrNewer(Version._0_76);
                        break;

                    case FieldType.Recall:
                        minimumVersion = minimumVersion.OrNewer(Version._1_3_1);
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
                        minimumVersion = minimumVersion.OrNewer(Version._0_77);
                        break;

                    case FieldSize.BitCount:
                        minimumVersion = minimumVersion.OrNewer(Version._0_78);
                        break;

                    case FieldSize.BigEndianWord:
                    case FieldSize.BigEndianTByte:
                    case FieldSize.BigEndianDWord:
                    case FieldSize.Float:
                    case FieldSize.MBF32:
                        minimumVersion = minimumVersion.OrNewer(Version._1_0);
                        break;

                    case FieldSize.LittleEndianMBF32:
                        minimumVersion = minimumVersion.OrNewer(Version._1_1);
                        break;

                    case FieldSize.BigEndianFloat:
                    case FieldSize.Double32:
                    case FieldSize.BigEndianDouble32:
                        minimumVersion = minimumVersion.OrNewer(Version._1_3);
                        break;

                    default:
                        break;
                }
            }

            return minimumVersion;
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
            if (that is null)
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
            if (left is null || right is null)
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
            if (left is null || right is null)
                return true;

            return !left.Equals(right);
        }

        private static RequirementType ReadRequirementType(Tokenizer tokenizer)
        {
            RequirementType type;

            switch (tokenizer.NextChar)
            {
                case 'R': type = RequirementType.ResetIf; break;
                case 'P': type = RequirementType.PauseIf; break;
                case 'A': type = RequirementType.AddSource; break;
                case 'B': type = RequirementType.SubSource; break;
                case 'C': type = RequirementType.AddHits; break;
                case 'D': type = RequirementType.SubHits; break;
                case 'N': type = RequirementType.AndNext; break;
                case 'O': type = RequirementType.OrNext; break;
                case 'I': type = RequirementType.AddAddress; break;
                case 'M': type = RequirementType.Measured; break;
                case 'G': type = RequirementType.MeasuredPercent; break;
                case 'Q': type = RequirementType.MeasuredIf; break;
                case 'Z': type = RequirementType.ResetNextIf; break;
                case 'T': type = RequirementType.Trigger; break;
                case 'K': type = RequirementType.Remember; break;

                default: 
                    return RequirementType.None;
            }

            var prefix = tokenizer.NextChar + ":";
            if (tokenizer.Match(prefix))
                return type;

            return RequirementType.None;
        }

        internal static Requirement Deserialize(Tokenizer tokenizer)
        {
            var requirement = new Requirement();
            requirement.Type = ReadRequirementType(tokenizer);
            requirement.Left = Field.Deserialize(tokenizer);

            requirement.Operator = ReadOperator(tokenizer);
            if (requirement.Operator != RequirementOperator.None)
                requirement.Right = Field.Deserialize(tokenizer);

            if (requirement.Type.IsScalable() && requirement.IsComparison)
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

                case '+':
                    tokenizer.Advance();
                    return RequirementOperator.Add;

                case '-':
                    tokenizer.Advance();
                    return RequirementOperator.Subtract;

                case '*':
                    tokenizer.Advance();
                    return RequirementOperator.Multiply;

                case '/':
                    tokenizer.Advance();
                    return RequirementOperator.Divide;

                case '&':
                    tokenizer.Advance();
                    return RequirementOperator.BitwiseAnd;

                case '^':
                    tokenizer.Advance();
                    return RequirementOperator.BitwiseXor;
            }

            return RequirementOperator.None;
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
}
