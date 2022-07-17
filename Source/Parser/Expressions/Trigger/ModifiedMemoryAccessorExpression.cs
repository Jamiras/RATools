using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class ModifiedMemoryAccessorExpression : ExpressionBase, ITriggerExpression, IExecutableExpression,
        IMathematicCombineExpression, IMathematicCombineInverseExpression,
        IComparisonNormalizeExpression, IUpconvertibleExpression
    {
        public ModifiedMemoryAccessorExpression()
            : base(ExpressionType.ModifiedMemoryAccessor)
        {
        }

        public ModifiedMemoryAccessorExpression(MemoryAccessorExpression source)
            : this()
        {
            MemoryAccessor = source.Clone();
            Location = source.Location;
        }

        public MemoryAccessorExpression MemoryAccessor { get; set; }

        public RequirementType CombiningOperator { get; set; }
        public RequirementOperator ModifyingOperator { get; set; }
        public Field Modifier { get; set; }

        public void InvertCombiningOperator()
        {
            if (CombiningOperator == RequirementType.SubSource)
                CombiningOperator = RequirementType.AddSource;
            else
                CombiningOperator = RequirementType.SubSource;
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ModifiedMemoryAccessorExpression;
            return (that != null && MemoryAccessor == that.MemoryAccessor);
        }

        internal override void AppendString(StringBuilder builder)
        {
            switch (CombiningOperator)
            {
                case RequirementType.AddSource:
                    builder.Append("+ ");
                    break;

                case RequirementType.SubSource:
                    builder.Append("- ");
                    break;
            }

            MemoryAccessor.AppendString(builder);

            switch (ModifyingOperator)
            {
                case RequirementOperator.Multiply:
                    builder.Append(" * ");
                    break;

                case RequirementOperator.Divide:
                    builder.Append(" / ");
                    break;

                case RequirementOperator.BitwiseAnd:
                    builder.Append(" & ");
                    break;

                default:
                    return;
            }

            if (Modifier.IsMemoryReference && MemoryAccessor.PointerChain.Any())
            {
                var clone = MemoryAccessor.Clone();
                clone.Field = Modifier;
                clone.AppendString(builder);
            }
            else if (ModifyingOperator == RequirementOperator.BitwiseAnd)
            {
                Modifier.AppendString(builder, NumberFormat.Hexadecimal);
            }
            else
            { 
                Modifier.AppendString(builder, NumberFormat.Decimal);
            }
        }

        public ModifiedMemoryAccessorExpression Clone()
        {
            var clone = new ModifiedMemoryAccessorExpression() { Location = this.Location };
            clone.MemoryAccessor = MemoryAccessor.Clone();
            clone.CombiningOperator = CombiningOperator;
            clone.ModifyingOperator = ModifyingOperator;
            clone.Modifier = Modifier.Clone();
            return clone;
        }

        /// <summary>
        /// Combines the current expression with the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to combine with the current expression.</param>
        /// <param name="operation">How to combine the expressions.</param>
        /// <returns>
        /// An expression representing the combined values on success, or <c>null</c> if the expressions could not be combined.
        /// </returns>
        public ExpressionBase Combine(ExpressionBase right, MathematicOperation operation)
        {
            var modifiedMemoryAccessorExpression = Clone();
            return modifiedMemoryAccessorExpression.ApplyMathematic(right, operation);
        }

        public ExpressionBase CombineInverse(ExpressionBase left, MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add:
                case MathematicOperation.Subtract:
                    var clause = new MemoryValueExpression();
                    clause = clause.ApplyMathematic(left, MathematicOperation.Add) as MemoryValueExpression;
                    if (clause != null)
                        return clause.ApplyMathematic(this, operation);
                    break;

                case MathematicOperation.Multiply:
                case MathematicOperation.BitwiseAnd:
                    return Combine(left, operation);

                case MathematicOperation.Divide:
                    return new ErrorExpression("Cannot divide by a complex runtime value");

                case MathematicOperation.Modulus:
                    return new ErrorExpression("Cannot modulus using a runtime value");
            }

            return null;
        }

        public ExpressionBase ApplyMathematic(ExpressionBase right, MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add:
                case MathematicOperation.Subtract:
                    var memoryValue = new MemoryValueExpression();
                    memoryValue.ApplyMathematic(this, MathematicOperation.Add);
                    return memoryValue.ApplyMathematic(right, operation);
            }

            Field field;

            var rightAccessor = right as MemoryAccessorExpression;
            if (rightAccessor != null && MemoryAccessor.PointerChainMatches(rightAccessor))
            {
                // FieldFactory won't process a MemoryAccessor with a Pointer chain. We want
                // to allow it, but only if it matches the pointer chain of this MemorAccessor.
                field = rightAccessor.Field;
            }
            else
            {
                field = FieldFactory.CreateField(right);
                if (field.Type == FieldType.None)
                    return new MathematicExpression(this, operation, right);
            }

            var newModifyingOperator = GetModifyingOperator(operation);
            if (newModifyingOperator == RequirementOperator.None)
            {
                if (operation == MathematicOperation.Modulus)
                {
                    if ((field.Type == FieldType.Value && field.Value == 1) ||
                        (field.Type == FieldType.Float && field.Float == 1.0))
                    {
                        // a % 1  =>  0
                        return new IntegerConstantExpression(0);
                    }

                    if ((field.Type == FieldType.Value && field.Value == 0) ||
                        (field.Type == FieldType.Float && field.Value == 0.0))
                    {
                        return new ErrorExpression("Division by zero");
                    }

                    if (MemoryAccessor.Field.IsMemoryReference)
                        return new ErrorExpression("Cannot modulus using a runtime value");
                }

                return new MathematicExpression(this, operation, right);
            }

            switch (ModifyingOperator)
            {
                case RequirementOperator.None:
                    ModifyingOperator = newModifyingOperator;
                    break;

                case RequirementOperator.Multiply:
                case RequirementOperator.Divide:
                case RequirementOperator.BitwiseAnd:
                    if (field.Type == FieldType.Float || Modifier.Type == FieldType.Float)
                    {
                        Modifier = FieldFactory.ConvertToFloat(Modifier);
                        field = FieldFactory.ConvertToFloat(field);

                        if (ModifyingOperator == RequirementOperator.Divide)
                        {
                            if (newModifyingOperator == RequirementOperator.Multiply)
                            {
                                // a / 6.0 * 2.0 => a / (6.0 / 2.0)
                                newModifyingOperator = RequirementOperator.Divide;
                            }
                            else if (newModifyingOperator == RequirementOperator.Divide)
                            {
                                // a / 2.0 / 4.0 => a / (2.0 * 4.0)
                                newModifyingOperator = RequirementOperator.Multiply;
                            }
                        }
                    }
                    else if (ModifyingOperator != newModifyingOperator)
                    {
                        if (Modifier.Type == FieldType.Value && field.Type == FieldType.Value)
                        {
                            if (ModifyingOperator == RequirementOperator.Multiply && newModifyingOperator == RequirementOperator.Divide)
                            {
                                // a * 4 / 2 => a * 2
                                if (Modifier.Value % field.Value == 0)
                                {
                                    field = FieldFactory.ApplyMathematic(Modifier, RequirementOperator.Divide, field);
                                    break;
                                }

                                // a * 2 / 4 => a / 2
                                if (field.Value % Modifier.Value == 0)
                                {
                                    ModifyingOperator = RequirementOperator.Divide;
                                    field = FieldFactory.ApplyMathematic(field, RequirementOperator.Divide, Modifier);
                                    break;
                                }
                            }
                        }
                        goto default;
                    }
                    else
                    {
                        // a / 2 / 4 => a / (2 * 4)
                        if (newModifyingOperator == RequirementOperator.Divide)
                            newModifyingOperator = RequirementOperator.Multiply;
                    }

                    field = FieldFactory.ApplyMathematic(Modifier, newModifyingOperator, field);
                    if (field.Type == FieldType.None)
                        goto default;

                    break;

                default:
                    return new MathematicExpression(this, operation, right);
            }

            if ((field.Type == FieldType.Value && field.Value == 0) ||
                (field.Type == FieldType.Float && field.Float == 0.0))
            {
                switch (ModifyingOperator)
                {
                    case RequirementOperator.Divide:
                        return new ErrorExpression("Division by zero");

                    case RequirementOperator.Multiply:   // a * 0  =>  0
                    case RequirementOperator.BitwiseAnd: // a & 0  =>  0
                        return new IntegerConstantExpression(0); 
                }
            }

            Modifier = field;
            CheckForIdentity();
            return this;
        }

        private void CheckForIdentity()
        {
            if ((Modifier.Type == FieldType.Value && Modifier.Value == 1) ||
                (Modifier.Type == FieldType.Float && Modifier.Float == 1.0))
            {
                switch (ModifyingOperator)
                {
                    case RequirementOperator.Multiply: // a * 1  =>  a
                    case RequirementOperator.Divide:   // a / 1  =>  a
                        ModifyingOperator = RequirementOperator.None;
                        break;
                }
            }
        }

        public void GetMinMax(out long min, out long max)
        {
            long accessorMin = 0;
            long accessorMax = Field.GetMaxValue(MemoryAccessor.Field.Size);
            if (MemoryAccessor.Field.Type == FieldType.Value)
                accessorMin = accessorMax = MemoryAccessor.Field.Value;

            uint modifierMin = 0;
            uint modifierMax = Field.GetMaxValue(Modifier.Size);
            if (Modifier.Type == FieldType.Value)
                modifierMin = modifierMax = Modifier.Value;
                
            switch (ModifyingOperator)
            {
                case RequirementOperator.Multiply:
                    min = accessorMin * modifierMin;
                    max = accessorMax * modifierMax;
                    break;

                case RequirementOperator.Divide:
                    if (MemoryAccessor.Field == Modifier)
                    {
                        // X / X will always be 0 or 1
                        min = 0;
                        max = 1;
                    }
                    else if (modifierMin == 0)
                    {
                        // division by zero in the runtime will output a 0
                        min = 0;
                        max = (modifierMax == 0) ? 0 : accessorMax;
                    }
                    else
                    {
                        // smallest value will be the smallest left divided by the largest right
                        // largest value will be the largest left divided by the smallest right
                        // don't need to worry about division by zero because a non-zero modifierMin
                        // also implies a non-zero modifiedMax
                        min = accessorMin / modifierMax;
                        max = accessorMax / modifierMin;
                    }
                    break;

                case RequirementOperator.BitwiseAnd:
                    min = 0;
                    max = modifierMax;
                    break;

                default:
                    min = accessorMin;
                    max = accessorMax;
                    break;
            }
        }

        private static RequirementOperator GetModifyingOperator(MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Multiply: return RequirementOperator.Multiply;
                case MathematicOperation.Divide: return RequirementOperator.Divide;
                case MathematicOperation.BitwiseAnd: return RequirementOperator.BitwiseAnd;
                default: return RequirementOperator.None;
            }
        }

        private static MathematicOperation GetMathematicOperation(RequirementOperator operation)
        {
            switch (operation)
            {
                case RequirementOperator.Multiply: return MathematicOperation.Multiply;
                case RequirementOperator.Divide: return MathematicOperation.Divide;
                case RequirementOperator.BitwiseAnd: return MathematicOperation.BitwiseAnd;
                default: return MathematicOperation.None;
            }
        }

        /// <summary>
        /// Normalizes the comparison between the current expression and the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to compare with the current expression.</param>
        /// <param name="operation">How to compare the expressions.</param>
        /// <returns>
        /// An expression representing the normalized comparison, or <c>null</c> if normalization did not occur.
        /// </returns>
        public ExpressionBase NormalizeComparison(ExpressionBase right, ComparisonOperation operation)
        {
            if (ModifyingOperator == RequirementOperator.None && CombiningOperator == RequirementType.None)
            {
                var normalized = MemoryAccessor.NormalizeComparison(right, operation);
                if (normalized != null)
                    return normalized;
            }

            var memoryValue = right as MemoryValueExpression;
            if (memoryValue != null)
            {
                var converted = memoryValue.ConvertToModifiedMemoryAccessor();
                if (converted != null)
                    right = converted;
            }

            var modifiedMemoryAccessor = right as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
            {
                var opposingOperator = MathematicExpression.GetOppositeOperation(GetMathematicOperation(modifiedMemoryAccessor.ModifyingOperator));
                if (opposingOperator != MathematicOperation.Multiply && opposingOperator != MathematicOperation.Divide)
                    return null;

                // prefer modifiers on left - try to merge left
                var modifier = modifiedMemoryAccessor.CreateModifierExpression();
                var newLeft = Clone();
                var newRight = modifiedMemoryAccessor.Clone();
                var result = newLeft.ApplyMathematic(modifier, opposingOperator);

                var mergeSuccessful = true;
                if (result is MathematicExpression)
                {
                    // could not merge 
                    mergeSuccessful = false;
                }
                else
                {
                    var modifiedMemoryAccessorResult = result as ModifiedMemoryAccessorExpression;
                    if (modifiedMemoryAccessorResult != null && 
                        modifiedMemoryAccessorResult.ModifyingOperator == RequirementOperator.Divide &&
                        ModifyingOperator == RequirementOperator.Multiply)
                    {
                        // multiplication changed to division may result in false positives due to integer
                        // division truncation. i.e. 17*10 is not equal to 8*20, but 17/2 is equal to 8 
                        mergeSuccessful = false;
                    }
                }

                if (!mergeSuccessful)
                {
                    // could not merge left. try merging right
                    opposingOperator = MathematicExpression.GetOppositeOperation(GetMathematicOperation(ModifyingOperator));
                    modifier = CreateModifierExpression();
                    result = newRight.ApplyMathematic(modifier, opposingOperator);

                    if (result is MathematicExpression)
                        return new ErrorExpression("Result can never be true using integer math");

                    // swap so modifier is on left
                    newRight = newLeft;
                    operation = ComparisonExpression.ReverseComparisonOperation(operation);
                }

                if (result is ErrorExpression)
                    return result;

                newRight.ModifyingOperator = RequirementOperator.None;
                return new ComparisonExpression(result, operation, newRight);
            }

            var mathematicOperation = GetMathematicOperation(ModifyingOperator);
            if (mathematicOperation != MathematicOperation.None)
            {
                var modifier = CreateModifierExpression();
                if (modifier != null)
                {
                    var newLeft = Clone();
                    newLeft.ModifyingOperator = RequirementOperator.None;

                    var mathematic = new MathematicExpression(newLeft, mathematicOperation, modifier);
                    return mathematic.NormalizeComparison(right, operation);
                }
            }

            if (right is FloatConstantExpression && !MemoryAccessor.Field.IsFloat)
                return ComparisonExpression.NormalizeFloatComparisonForInteger(this, operation, right);

            return null;
        }

        private ExpressionBase CreateModifierExpression()
        {
            switch (Modifier.Type)
            {
                case FieldType.Value:
                    return new IntegerConstantExpression((int)Modifier.Value);

                case FieldType.Float:
                    return new FloatConstantExpression(Modifier.Float);

                default:
                    return new MemoryAccessorExpression(Modifier);
            }
        }

        /// <summary>
        /// Attempts to create a new expression from the current expression without loss of data.
        /// </summary>
        /// <param name="newType">The type of express to try to convert to.</param>
        /// <returns>
        /// A new expression of the requested type, or <c>null</c> if the conversion could not be performed.
        /// </returns>
        public ExpressionBase UpconvertTo(ExpressionType newType)
        {
            switch (newType)
            {
                case ExpressionType.MemoryValue:
                    var clause = new MemoryValueExpression();
                    return clause.ApplyMathematic(this, MathematicOperation.Add);

                default:
                    return null;
            }
        }

        public ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            MemoryAccessor.BuildTrigger(context);
            if (context.LastRequirement.Type != RequirementType.None)
                return new ErrorExpression("Cannot combine modified requirement", MemoryAccessor);
            context.LastRequirement.Type = CombiningOperator;

            if (ModifyingOperator != RequirementOperator.None)
            {
                if (context.LastRequirement.Operator != RequirementOperator.None)
                    return new ErrorExpression("Cannot combine modified requirement", MemoryAccessor);
                context.LastRequirement.Operator = ModifyingOperator;

                if (context.LastRequirement.Right.Type != FieldType.None)
                    return new ErrorExpression("Cannot combine modified requirement", MemoryAccessor);
                context.LastRequirement.Right = Modifier;
            }

            return null;
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            return MemoryAccessor.Execute(scope);
        }
    }
}
