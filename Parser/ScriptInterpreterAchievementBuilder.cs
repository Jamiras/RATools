using RATools.Data;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser
{
    internal class ScriptInterpreterAchievementBuilder : AchievementBuilder
    {
        public ScriptInterpreterAchievementBuilder() : base()
        {
        }

        /// <summary>
        /// Begins an new alt group.
        /// </summary>
        /// <returns>The requirement collection for the new alt group.</returns>
        private void BeginAlt(TriggerBuilderContext context)
        {
            if (ReferenceEquals(context.Trigger, CoreRequirements) || context.Trigger.Count > 0)
            {
                var newAlt = new List<Requirement>();
                context.Trigger = newAlt;
                AlternateRequirements.Add(newAlt);
            }
        }

        private static bool NormalizeNots(ref ExpressionBase expression, out ParseErrorExpression error)
        {
            error = null;

            // not a condition - don't need to worry about it
            var condition = expression as ConditionalExpression;
            if (condition == null)
                return true;

            // not a not, just recurse
            if (condition.Operation != ConditionalOperation.Not)
            {
                var left = condition.Left;
                if (!NormalizeNots(ref left, out error))
                    return false;

                var right = condition.Right;
                if (!NormalizeNots(ref right, out error))
                    return false;

                if (!ReferenceEquals(left, condition.Left) || !ReferenceEquals(right, condition.Right))
                    expression = new ConditionalExpression(left, condition.Operation, right);

                return true;
            }

            // found a not - eliminate it
            var operand = ((ConditionalExpression)expression).Right;

            // logical inversion
            condition = operand as ConditionalExpression;
            if (condition != null)
            {
                switch (condition.Operation)
                {
                    case ConditionalOperation.Not:
                        // !(!A) => A
                        expression = condition.Right;
                        break;

                    case ConditionalOperation.And:
                        // !(A && B) => !A || !B
                        expression = new ConditionalExpression(
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Left),
                            ConditionalOperation.Or,
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Right));
                        break;

                    case ConditionalOperation.Or:
                        // !(A || B) => !A && !B
                        expression = new ConditionalExpression(
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Left),
                            ConditionalOperation.And,
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Right));
                        break;

                    default:
                        throw new NotImplementedException("Unsupported condition operation");
                }

                return NormalizeNots(ref expression, out error);
            }

            // comparative inversion
            var comparison = operand as ComparisonExpression;
            if (comparison != null)
            {
                // !(A == B) => A != B, !(A < B) => A >= B, ...
                expression = new ComparisonExpression(
                    comparison.Left,
                    ComparisonExpression.GetOppositeComparisonOperation(comparison.Operation),
                    comparison.Right);

                return NormalizeNots(ref expression, out error);
            }

            // unsupported inversion
            error = new ParseErrorExpression("! operator cannot be applied to " + operand.Type, operand);
            return false;
        }

        private static void FlattenOrClause(ExpressionBase clause, List<ExpressionBase> flattened)
        {
            var condition = clause as ConditionalExpression;
            if (condition != null && condition.Operation == ConditionalOperation.Or)
            {
                FlattenOrClause(condition.Left, flattened);
                FlattenOrClause(condition.Right, flattened);
            }
            else
            {
                flattened.Add(clause);
            }
        }

        private static ExpressionBase CrossMultiplyOrConditions(List<ExpressionBase> orConditions)
        {
            // This creates a combinatorial collection from one or more collections of OR'd conditions.
            // Redundancies will be optimized out later.
            //
            // (A || B)                         => (A || B)
            //
            // (A || B) && C                    => (A && C) || (B && C)
            //
            // (A || B) && (C || D)             => (A && C) || (A && D) || (B && C) || (B && D)
            //
            // (A || B || C) && (D || E || F)   => (A && D) || (A && E) || (A && F) ||
            //                                     (B && D) || (B && E) || (B && F) ||
            //                                     (C && D) || (C && E) || (C && F)
            //
            // (A || B) && (C || D) && (E || F) => (A && C && E) ||
            //                                     (A && C && F) ||
            //                                     (A && D && E) ||
            //                                     (A && D && F) ||
            //                                     (B && C && E) ||
            //                                     (B && C && F) ||
            //                                     (B && D && E) ||
            //                                     (B && D && F)
            // ...

            // first turn the OR trees into flat lists -- (A || (B || (C || D))) -> A, B, C, D
            var flattenedClauses = new List<List<ExpressionBase>>();
            foreach (var clause in orConditions)
            {
                var flattened = new List<ExpressionBase>();
                FlattenOrClause(clause, flattened);
                flattenedClauses.Add(flattened);
            }

            // then, create an alt group for every possible combination of items from each of the flattened lists
            var numFlattendClauses = flattenedClauses.Count();
            var partIndex = new int[numFlattendClauses];
            var parts = new List<ExpressionBase>();
            do
            {
                var andPart = flattenedClauses[numFlattendClauses - 1][partIndex[numFlattendClauses - 1]];
                for (int clauseIndex = numFlattendClauses - 2; clauseIndex >= 0; clauseIndex--)
                {
                    var expression = flattenedClauses[clauseIndex][partIndex[clauseIndex]];
                    andPart = new ConditionalExpression(expression, ConditionalOperation.And, andPart);
                }

                parts.Add(andPart);

                int i = numFlattendClauses - 1;
                do
                {
                    if (++partIndex[i] < flattenedClauses[i].Count)
                        break;

                    if (i == 0)
                    {
                        var orPart = parts[parts.Count - 1];
                        for (i = parts.Count - 2; i >= 0; i--)
                            orPart = new ConditionalExpression(parts[i], ConditionalOperation.Or, orPart);

                        return orPart;
                    }

                    partIndex[i--] = 0;
                } while (true);
            } while (true);
        }

        private static ConditionalExpression BubbleUpOrs(ConditionalExpression condition)
        {
            bool modified = false;
            bool hasChildOr = false;

            ConditionalExpression left = condition.Left as ConditionalExpression;
            if (left != null)
            {
                left = BubbleUpOrs(left);
                modified |= !ReferenceEquals(left, condition.Left);
                hasChildOr |= (left.Operation == ConditionalOperation.Or);
            }

            ConditionalExpression right = condition.Right as ConditionalExpression;
            if (right != null)
            {
                right = BubbleUpOrs(right);
                modified |= !ReferenceEquals(right, condition.Right);
                hasChildOr |= (right.Operation == ConditionalOperation.Or);
            }

            if (modified)
            {
                var newCondition = new ConditionalExpression(
                    left ?? condition.Left, condition.Operation, right ?? condition.Right);
                condition.CopyLocation(newCondition);
                condition = newCondition;
            }

            if (condition.Operation == ConditionalOperation.And && hasChildOr)
            {
                var orConditions = new List<ExpressionBase>();
                orConditions.Add(left ?? condition.Left);
                orConditions.Add(right ?? condition.Right);

                var expression = CrossMultiplyOrConditions(orConditions);
                return (ConditionalExpression)expression;
            }

            return condition;
        }

        private static bool SortConditions(ExpressionBase expression, List<ExpressionBase> andedConditions, List<ExpressionBase> orConditions, out ParseErrorExpression error)
        {
            var condition = expression as ConditionalExpression;
            if (condition == null)
            {
                andedConditions.Add(expression);
                error = null;
                return true;
            }

            switch (condition.Operation)
            {
                case ConditionalOperation.And:
                    if (!SortConditions(condition.Left, andedConditions, orConditions, out error))
                        return false;
                    if (!SortConditions(condition.Right, andedConditions, orConditions, out error))
                        return false;
                    break;

                case ConditionalOperation.Or:
                    condition = BubbleUpOrs(condition);
                    orConditions.Add(condition);
                    break;

                default:
                    error = new ParseErrorExpression("Unexpected condition: " + condition.Operation, condition);
                    return false;
            }

            error = null;
            return true;
        }

        internal bool PopulateFromExpression(ExpressionBase expression, InterpreterScope scope, out ParseErrorExpression error)
        {
            if (!NormalizeNots(ref expression, out error))
                return false;

            var andedConditions = new List<ExpressionBase>();
            var orConditions = new List<ExpressionBase>();
            if (!SortConditions(expression, andedConditions, orConditions, out error))
                return false;

            if (orConditions.Count() != 0)
            {
                var altPart = CrossMultiplyOrConditions(orConditions);
                andedConditions.Add(altPart);
            }

            var context = new TriggerBuilderContext { Trigger = CoreRequirements };
            var innerScope = new InterpreterScope(scope) { Context = context };
            foreach (var condition in andedConditions)
            {
                error = ExecuteAchievementExpression(condition, innerScope);
                if (error != null)
                {
                    if (error.InnerError != null)
                        error = new ParseErrorExpression(error.InnermostError.Message, error.Line, error.Column, error.EndLine, error.EndColumn);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Populates the <see cref="AchievementBuilder"/> from an expression.
        /// </summary>
        /// <param name="expression">The expression to populate from.</param>
        /// <returns><c>null</c> if successful, otherwise an error message indicating why it failed.</returns>
        public string PopulateFromExpression(ExpressionBase expression)
        {
            var context = new TriggerBuilderContext { Trigger = CoreRequirements };
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope()) { Context = context };

            ExpressionBase result;
            if (!expression.ReplaceVariables(scope, out result))
                return ((ParseErrorExpression)result).Message;

            ParseErrorExpression error;
            if (!PopulateFromExpression(result, scope, out error))
                return error.Message;

            return null;
        }

        private ParseErrorExpression ExecuteAchievementExpression(ExpressionBase expression, InterpreterScope scope)
        {
            ExpressionBase operand;

            switch (expression.Type)
            {
                case ExpressionType.FunctionCall:
                    return ExecuteAchievementFunction((FunctionCallExpression)expression, scope);

                case ExpressionType.Conditional:
                    return ExecuteAchievementConditional((ConditionalExpression)expression, scope);

                case ExpressionType.Comparison:
                    return ExecuteAchievementComparison((ComparisonExpression)expression, scope);

                case ExpressionType.Mathematic:
                    return ExecuteAchievementMathematic((MathematicExpression)expression, scope);

                case ExpressionType.Variable:
                    if (!((VariableExpression)expression).ReplaceVariables(scope, out operand))
                        return new ParseErrorExpression(operand, expression);

                    return ExecuteAchievementExpression(operand, scope);
            }

            return new ParseErrorExpression("Cannot generate trigger from " + expression.Type, expression);
        }

        private ParseErrorExpression ExecuteAchievementMathematic(MathematicExpression mathematic, InterpreterScope scope)
        {
            var operation = mathematic.Operation;
            if (operation != MathematicOperation.Add && operation != MathematicOperation.Subtract)
                return new ParseErrorExpression("Cannot normalize expression to eliminate " + MathematicExpression.GetOperatorType(operation), mathematic);

            var left = mathematic.Left;
            var context = scope.GetContext<TriggerBuilderContext>();
            ParseErrorExpression error;

            ExpressionBase right;
            if (!mathematic.Right.ReplaceVariables(scope, out right))
                return (ParseErrorExpression)right;

            var integerOperand = right as IntegerConstantExpression;
            if (integerOperand != null)
            {
                context.Trigger.Add(new Requirement
                {
                    Type = (operation == MathematicOperation.Add) ? RequirementType.AddSource : RequirementType.SubSource,
                    Left = new Field { Type = FieldType.Value, Value = (uint)integerOperand.Value },
                    Operator = RequirementOperator.None,
                    Right = new Field()
                });
            }

            if (operation == MathematicOperation.Subtract && (right is FunctionCallExpression || right is MathematicExpression))
            {
                // if subtracting a non-integer, swap the order to perform a SubSource
                var requirements = new List<Requirement>();
                var innerContext = new TriggerBuilderContext() { Trigger = requirements };
                var innerScope = new InterpreterScope(scope) { Context = innerContext };

                // generate the condition for the right side
                error = ExecuteAchievementExpression(right, innerScope);
                if (error != null)
                    return error;

                foreach (var requirement in requirements)
                {
                    switch (requirement.Type)
                    {
                        case RequirementType.None:
                        case RequirementType.AddSource:
                            requirement.Type = RequirementType.SubSource;
                            break;
                        case RequirementType.SubSource:
                            requirement.Type = RequirementType.AddSource;
                            break;
                        default:
                            return new ParseErrorExpression("Cannot normalize expression for negation", mathematic);
                    }

                    context.Trigger.Add(requirement);
                }

                right = mathematic.Left;
                operation = MathematicOperation.Add;
            }
            else
            {
                // generate the condition for the first expression
                error = ExecuteAchievementExpression(left, scope);
                if (error != null)
                    return error;
            }

            if (integerOperand != null)
                return null;

            if (operation == MathematicOperation.Add)
            {
                // adding two memory accessors - make sure previous is AddSource or SubSource
                if (context.LastRequirement.Type != RequirementType.SubSource)
                {
                    context.LastRequirement.Type = RequirementType.AddSource;
                    context.LastRequirement.Operator = RequirementOperator.None;
                    context.LastRequirement.Right = new Field();
                }
            }
            else
            {
                return new ParseErrorExpression(String.Format("Cannot normalize expression to eliminate {0}", MathematicExpression.GetOperatorType(mathematic.Operation)), mathematic);
            }

            // generate the condition for the second expression
            error = ExecuteAchievementExpression(right, scope);
            if (error != null)
                error = new ParseErrorExpression(error.Message, mathematic);
            return error;
        }

        private ParseErrorExpression ExecuteAchievementConditional(ConditionalExpression condition, InterpreterScope scope)
        {
            ParseErrorExpression error;
            var context = scope.GetContext<TriggerBuilderContext>();

            switch (condition.Operation)
            {
                case ConditionalOperation.Not:
                    return new ParseErrorExpression("! operator should have been normalized out", condition);

                case ConditionalOperation.And:
                    error = ExecuteAchievementExpression(condition.Left, scope);
                    if (error != null)
                        return error;

                    error = ExecuteAchievementExpression(condition.Right, scope);
                    if (error != null)
                        return error;

                    return null;

                case ConditionalOperation.Or:
                    BeginAlt(context);
                    error = ExecuteAchievementExpression(condition.Left, scope);
                    if (error != null)
                        return error;

                    BeginAlt(context);
                    error = ExecuteAchievementExpression(condition.Right, scope);
                    if (error != null)
                        return error;

                    return null;
            }

            return new ParseErrorExpression("Unsupported conditional", condition);
        }

        private bool MoveConstantsToRightHandSide(ComparisonExpression comparisonExpression, InterpreterScope scope, out ExpressionBase result)
        {
            ComparisonExpression newRoot;
            var mathematic = (MathematicExpression)comparisonExpression.Left;

            // if the rightmost expression of the left side of the comparison is an integer, shift it to the 
            // right side of the equation and attempt to merge it with whatever is already there.
            var integer = mathematic.Right as IntegerConstantExpression;
            if (integer == null)
            {
                result = comparisonExpression;
                return true;
            }

            // apply the inverse of the mathematical operation to generate a new right side
            var operation = MathematicExpression.GetOppositeOperation(mathematic.Operation);
            var right = new MathematicExpression(comparisonExpression.Right, operation, mathematic.Right);
            if (!right.ReplaceVariables(scope, out result))
                return false;

            var newRight = result;
            var comparisonOperation = comparisonExpression.Operation;

            // multiplication is converted to division. if the division is not exact, modify the comparison 
            // so its still logically valid (if possible).
            if (operation == MathematicOperation.Divide && newRight is IntegerConstantExpression)
            {
                var reversed = new MathematicExpression(result, MathematicOperation.Multiply, mathematic.Right);
                if (!reversed.ReplaceVariables(scope, out result))
                    return false;

                if (comparisonExpression.Right != result)
                {
                    // division was not exact
                    switch (comparisonOperation)
                    {
                        case ComparisonOperation.Equal:
                            // a * 10 == 9999 can never be true
                            result = new ParseErrorExpression("Result can never be true using integer math", comparisonExpression);
                            return false;

                        case ComparisonOperation.NotEqual:
                            // a * 10 != 9999 is always true
                            result = new ParseErrorExpression("Result is always true using integer math", comparisonExpression);
                            return false;

                        case ComparisonOperation.LessThan:
                            // a * 10 < 9999 becomes a < 999
                            break;

                        case ComparisonOperation.LessThanOrEqual:
                            // a * 10 <= 9999 becomes a <= 999
                            break;

                        case ComparisonOperation.GreaterThan:
                            // a * 10 > 9999 becomes a > 999
                            break;

                        case ComparisonOperation.GreaterThanOrEqual:
                            // a * 10 >= 9999 becomes a > 999
                            comparisonOperation = ComparisonOperation.GreaterThan;
                            break;
                    }
                }
            }

            // construct the new equation and recurse if applicable
            newRoot = new ComparisonExpression(mathematic.Left, comparisonOperation, newRight);
            if (newRoot.Left.Type == ExpressionType.Mathematic)
                return MoveConstantsToRightHandSide(newRoot, scope, out result);

            comparisonExpression.CopyLocation(newRoot);
            result = newRoot;
            return true;
        }

        private bool EnsureSingleExpressionOnRightHandSide(ComparisonExpression comparisonExpression, InterpreterScope scope, ref int underflowAdjustment, out ExpressionBase result)
        {
            MathematicExpression newLeft;
            ComparisonExpression newRoot;

            // if the right hand side of the comparison is a mathematic, shift part of it to the left hand side so 
            // the right hand side eventually has a single value/address
            var mathematic = (MathematicExpression)comparisonExpression.Right;
            bool moveLeft = true;

            switch (mathematic.Operation)
            {
                case MathematicOperation.Add:
                    // the leftmost part of the right side will be moved to the left side as a subtraction.
                    // when subtracting a memory accessor, the result could be negative. since we're using
                    // unsigned values, this could result in a very high positive number. if not doing an exact
                    // comparison, check for potential underflow and offset total calculation to prevent it.
                    if (comparisonExpression.Operation != ComparisonOperation.Equal && comparisonExpression.Operation != ComparisonOperation.NotEqual)
                    {
                        var functionCall = mathematic.Left as FunctionCallExpression;
                        if (functionCall != null)
                        {
                            var memoryAccessor = scope.GetFunction(functionCall.FunctionName.Name) as MemoryAccessorFunction;
                            if (memoryAccessor != null && memoryAccessor.Size != FieldSize.DWord)
                                underflowAdjustment += (int)Field.GetMaxValue(memoryAccessor.Size);
                        }
                    }

                    if (mathematic.Right is IntegerConstantExpression)
                        moveLeft = false;
                    break;

                case MathematicOperation.Subtract:
                    // the rightmost part of the right side will be moved to the left side as an addition
                    moveLeft = false;
                    break;

                default:
                    result = new ParseErrorExpression("Cannot eliminate " + MathematicExpression.GetOperatorType(mathematic.Operation) +
                        " from right side of comparison", comparisonExpression);
                    return false;
            }

            if (moveLeft)
            {
                // left side is implicitly added on the right, so explicitly subtract it on the left
                newLeft = new MathematicExpression(comparisonExpression.Left, MathematicOperation.Subtract, mathematic.Left);
                newRoot = new ComparisonExpression(newLeft, comparisonExpression.Operation, mathematic.Right);
            }
            else
            {
                // invert the operation when moving the right side to the left
                newLeft = new MathematicExpression(comparisonExpression.Left, MathematicExpression.GetOppositeOperation(mathematic.Operation), mathematic.Right);
                newRoot = new ComparisonExpression(newLeft, comparisonExpression.Operation, mathematic.Left);
            }

            // ensure the IntegerConstant is the rightmost element of the left side
            mathematic = comparisonExpression.Left as MathematicExpression;
            if (mathematic != null && mathematic.Right is IntegerConstantExpression)
            {
                newLeft.Left = mathematic.Left;
                mathematic.Left = newLeft;
                newRoot.Left = mathematic;
            }

            // recurse if necessary
            if (newRoot.Right is MathematicExpression)
                return EnsureSingleExpressionOnRightHandSide(newRoot, scope, ref underflowAdjustment, out result);

            result = newRoot;
            return true;
        }

        private static int CalculateUnderflow(MathematicExpression mathematic, InterpreterScope scope, bool invert, bool hasSubtract)
        {
            int underflowAdjustment = 0;

            var subsourceOperation = invert ? MathematicOperation.Add : MathematicOperation.Subtract;
            if (mathematic.Operation == subsourceOperation)
            {
                var functionCall = mathematic.Right as FunctionCallExpression;
                if (functionCall != null)
                {
                    var memoryAccessor = scope.GetFunction(functionCall.FunctionName.Name) as MemoryAccessorFunction;
                    if (memoryAccessor != null && memoryAccessor.Size != FieldSize.DWord)
                        underflowAdjustment += (int)Field.GetMaxValue(memoryAccessor.Size);
                }
            }
            else if (hasSubtract)
            {
                var functionCall = mathematic.Left as FunctionCallExpression;
                if (functionCall != null)
                {
                    var memoryAccessor = scope.GetFunction(functionCall.FunctionName.Name) as MemoryAccessorFunction;
                    if (memoryAccessor != null && memoryAccessor.Size != FieldSize.DWord)
                        underflowAdjustment += (int)Field.GetMaxValue(memoryAccessor.Size);
                }
            }

            var mathematicLeft = mathematic.Left as MathematicExpression;
            if (mathematicLeft != null)
                underflowAdjustment += CalculateUnderflow(mathematicLeft, scope, invert, hasSubtract);

            var mathematicRight = mathematic.Right as MathematicExpression;
            if (mathematicRight != null)
            {
                if (mathematic.Operation == MathematicOperation.Subtract)
                    underflowAdjustment += CalculateUnderflow(mathematicRight, scope, !invert, true);
                else
                    underflowAdjustment += CalculateUnderflow(mathematicRight, scope, invert, hasSubtract);
            }

            return underflowAdjustment;
        }

        private ParseErrorExpression ExecuteAchievementComparison(ComparisonExpression comparison, InterpreterScope scope)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            var insertIndex = context.Trigger.Count;
            int underflowAdjustment = 0;

            if (comparison.Left.Type == ExpressionType.Mathematic)
            {
                var mathematic = (MathematicExpression)comparison.Left;
                comparison.Left = mathematic = MathematicExpression.BubbleUpIntegerConstant(mathematic);

                // if comparison is not direct equality/inequality, need to check for underflow
                if (comparison.Operation != ComparisonOperation.Equal && comparison.Operation != ComparisonOperation.NotEqual)
                {
                    underflowAdjustment = CalculateUnderflow((MathematicExpression)comparison.Left, scope, false, false);

                    if (underflowAdjustment != 0 &&
                        (comparison.Operation == ComparisonOperation.LessThan || comparison.Operation == ComparisonOperation.LessThanOrEqual))
                    {
                        // if the user has specified an underflow adjustment, keep it, regardless of the calculated value
                        if (mathematic.Right is IntegerConstantExpression)
                            underflowAdjustment = ((IntegerConstantExpression)mathematic.Right).Value;
                    }
                }

                ExpressionBase result;
                if (!MoveConstantsToRightHandSide(comparison, scope, out result))
                    return (ParseErrorExpression)result;

                comparison = (ComparisonExpression)result;
            }

            if (comparison.Right.Type == ExpressionType.Mathematic)
            {
                ExpressionBase result;
                if (!EnsureSingleExpressionOnRightHandSide(comparison, scope, ref underflowAdjustment, out result))
                    return (ParseErrorExpression)result;

                comparison = (ComparisonExpression)result;
            }

            if (underflowAdjustment > 0)
            {
                // add a dummy variable to the right side and rebalance again to move the existing right hand side to the left hand side
                var newRight = new MathematicExpression(comparison.Right, MathematicOperation.Add, new VariableExpression("unused"));
                comparison = new ComparisonExpression(comparison.Left, comparison.Operation, newRight);

                ExpressionBase result;
                if (!EnsureSingleExpressionOnRightHandSide(comparison, scope, ref underflowAdjustment, out result))
                    return (ParseErrorExpression)result;

                comparison = (ComparisonExpression)result;
                System.Diagnostics.Debug.Assert(comparison.Right is VariableExpression);

                // add the new underflow to both sides of the comparison
                MathematicExpression newLeft = comparison.Left as MathematicExpression;
                if (newLeft != null && newLeft.Right is IntegerConstantExpression)
                {
                    var value = ((IntegerConstantExpression)newLeft.Right).Value;
                    if (newLeft.Operation == MathematicOperation.Add)
                    {
                        newLeft.Right = new IntegerConstantExpression(value + underflowAdjustment);
                        comparison = new ComparisonExpression(newLeft, comparison.Operation, new IntegerConstantExpression(underflowAdjustment));
                    }
                    else if (newLeft.Operation == MathematicOperation.Subtract)
                    {
                        newLeft = new MathematicExpression(newLeft.Left, MathematicOperation.Add, new IntegerConstantExpression(underflowAdjustment));
                        comparison = new ComparisonExpression(newLeft, comparison.Operation, new IntegerConstantExpression(underflowAdjustment + value));
                    }
                }
                else
                {
                    newLeft = new MathematicExpression(comparison.Left, MathematicOperation.Add, new IntegerConstantExpression(underflowAdjustment));
                    comparison = new ComparisonExpression(newLeft, comparison.Operation, new IntegerConstantExpression(underflowAdjustment));
                }
            }

            var left = comparison.Left;
            var right = comparison.Right;

            var op = GetRequirementOperator(comparison.Operation);

            if (left.Type == ExpressionType.IntegerConstant)
            {
                if (right.Type == ExpressionType.IntegerConstant)
                {
                    // comparing two constants, convert to always_true or always_false
                    var requirement = new Requirement
                    {
                        Left = new Field { Type = FieldType.Value, Value = (uint)((IntegerConstantExpression)left).Value },
                        Operator = op,
                        Right = new Field { Type = FieldType.Value, Value = (uint)((IntegerConstantExpression)right).Value },
                    };

                    if (requirement.Evaluate() == true)
                        context.Trigger.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                    else
                        context.Trigger.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());

                    return null;
                }

                // swap the operands and operator so the constant is on the right
                var temp = left;
                left = right;
                right = temp;

                op = Requirement.GetReversedRequirementOperator(op);
            }

            var error = ExecuteAchievementExpression(left, scope);
            if (error != null)
                return error;

            var integerRight = right as IntegerConstantExpression;
            if (integerRight != null)
            {
                int newValue = integerRight.Value;

                var requirement = context.LastRequirement;
                requirement.Operator = op;
                requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = (uint)newValue };
            }
            else
            {
                error = ExecuteAchievementExpression(right, scope);
                if (error != null)
                    return error;

                var extraRequirement = context.LastRequirement;
                context.Trigger.Remove(extraRequirement);

                var requirement = context.LastRequirement;
                if (requirement != null)
                {
                    if (requirement.Type == RequirementType.AddAddress)
                    {
                        var addAddressRequirements = new List<Requirement>();
                        do
                        {
                            addAddressRequirements.Add(requirement);
                            ((IList<Requirement>)context.Trigger).RemoveAt(context.Trigger.Count - 1);

                            requirement = context.LastRequirement;
                        } while (requirement.Type == RequirementType.AddAddress);

                        if (context.Trigger.Count <= addAddressRequirements.Count)
                            return new ParseErrorExpression("Indirect memory addresses must match on both sides of a comparison", comparison);

                        for (int i = 0; i < addAddressRequirements.Count; i++)
                        {
                            var previousRequirement = context.Trigger.ElementAt(context.Trigger.Count - addAddressRequirements.Count - 1 + i);
                            if (previousRequirement != addAddressRequirements[i])
                                return new ParseErrorExpression("Indirect memory addresses must match on both sides of a comparison", comparison);
                        }
                    }

                    requirement.Operator = op;
                    requirement.Right = extraRequirement.Left;
                }
            }

            return null;
        }

        private static RequirementOperator GetRequirementOperator(ComparisonOperation comparisonOperation)
        {
            switch (comparisonOperation)
            {
                case ComparisonOperation.Equal: return RequirementOperator.Equal;
                case ComparisonOperation.NotEqual: return RequirementOperator.NotEqual;
                case ComparisonOperation.LessThan: return RequirementOperator.LessThan;
                case ComparisonOperation.LessThanOrEqual: return RequirementOperator.LessThanOrEqual;
                case ComparisonOperation.GreaterThan: return RequirementOperator.GreaterThan;
                case ComparisonOperation.GreaterThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                default: return RequirementOperator.None;
            }
        }

        private ParseErrorExpression ExecuteAchievementFunction(FunctionCallExpression functionCall, InterpreterScope scope)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            return context.CallFunction(functionCall, scope);
        }
    }
}