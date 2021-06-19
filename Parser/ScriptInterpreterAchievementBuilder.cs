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
            var expansionSize = 1;
            foreach (var clause in orConditions)
            {
                var flattened = new List<ExpressionBase>();
                FlattenOrClause(clause, flattened);
                flattenedClauses.Add(flattened);

                expansionSize *= flattened.Count();
            }

            if (expansionSize >= 20)
            {
                foreach (var flattened in flattenedClauses)
                {
                    var orNextChain = BuildOrNextChain(flattened);
                    if (orNextChain != null)
                    {
                        expansionSize /= flattened.Count();

                        flattened.Clear();
                        flattened.Add(orNextChain);
                    }
                }
            }

            const int MAX_EXPANSION_SIZE = 10000;
            if (expansionSize > MAX_EXPANSION_SIZE)
                return new ParseErrorExpression(String.Format("Expansion of complex clause would result in {0} alt groups (exceeds {1} limit)", expansionSize, MAX_EXPANSION_SIZE));

            // then, create an alt group for every possible combination of items from each of the flattened lists
            var numFlattenedClauses = flattenedClauses.Count();
            var partIndex = new int[numFlattenedClauses];
            var parts = new List<ExpressionBase>();
            do
            {
                var andPart = flattenedClauses[numFlattenedClauses - 1][partIndex[numFlattenedClauses - 1]];
                for (int clauseIndex = numFlattenedClauses - 2; clauseIndex >= 0; clauseIndex--)
                {
                    var expression = flattenedClauses[clauseIndex][partIndex[clauseIndex]];
                    andPart = new ConditionalExpression(expression, ConditionalOperation.And, andPart);
                }

                parts.Add(andPart);

                int i = numFlattenedClauses - 1;
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

        private static bool IsValidInOrNextChain(ExpressionBase expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.IntegerConstant:
                    return true;

                case ExpressionType.Comparison:
                    var comparison = (ComparisonExpression)expression;
                    return IsValidInOrNextChain(comparison.Left) && IsValidInOrNextChain(comparison.Right);

                case ExpressionType.FunctionCall:
                    var funcCall = (FunctionCallExpression)expression;

                    // only memory accessor functions are allowed. all other functions
                    // are assumed to flag the condition or set a hitcount on it.
                    var globalScope = AchievementScriptInterpreter.GetGlobalScope();
                    var funcDef = globalScope.GetFunction(funcCall.FunctionName.Name);
                    if (funcDef is MemoryAccessorFunction)
                        return true;

                    if (funcDef is AlwaysFalseFunction)
                        return true;

                    if (funcDef is AlwaysTrueFunction)
                        return true;

                    return false;

                default:
                    return false;
            }
        }

        private static ExpressionBase BuildOrNextChain(List<ExpressionBase> clause)
        {
            if (clause.Count == 1)
                return clause[0];

            foreach (var condition in clause)
            {
                if (!IsValidInOrNextChain(condition))
                    return null;
            }

            ExpressionBase result = null;
            foreach (var condition in clause)
            {
                if (result == null)
                    result = condition;
                else
                    result = new ConditionalExpression(result, ConditionalOperation.Or, condition);
            }

            result = new FunctionCallExpression("repeated", new ExpressionBase[]
            {
                new IntegerConstantExpression(0),
                result
            });

            return result;
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
            var andedConditions = new List<ExpressionBase>();
            var orConditions = new List<ExpressionBase>();
            if (!SortConditions(expression, andedConditions, orConditions, out error))
                return false;

            if (orConditions.Count() > 1)
            {
                var altPart = CrossMultiplyOrConditions(orConditions);
                if (altPart.Type == ExpressionType.ParseError)
                {
                    expression.CopyLocation(altPart);
                    error = (ParseErrorExpression)altPart;
                    return false;
                }

                andedConditions.Add(altPart);
            }
            else if (orConditions.Count() == 1)
            {
                andedConditions.Add(orConditions[0]);
            }

            var context = new TriggerBuilderContext { Trigger = CoreRequirements };
            var innerScope = new InterpreterScope(scope) { Context = context };
            foreach (var condition in andedConditions)
            {
                error = ExecuteAchievementClause(condition, innerScope);

                if (error != null)
                {
                    switch (condition.Type)
                    {
                        case ExpressionType.Comparison:
                        case ExpressionType.Conditional:
                            break;

                        default:
                            error = ParseErrorExpression.WrapError(error, "Invalid condition", condition);
                            break;
                    }
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
                    if (!expression.ReplaceVariables(scope, out operand))
                        return new ParseErrorExpression(operand, expression);

                    if (expression is FunctionReferenceExpression)
                        return new ParseErrorExpression("Function used like a variable", expression);

                    return ExecuteAchievementExpression(operand, scope);
            }

            return new ParseErrorExpression("Cannot generate trigger from " + expression.Type, expression);
        }

        private ParseErrorExpression ExecuteAchievementClause(ExpressionBase expression, InterpreterScope scope)
        {
            var error = ExecuteAchievementExpression(expression, scope);
            if (error != null)
                return error;

            var context = scope.GetContext<TriggerBuilderContext>();
            if (context.LastRequirement == null || context.LastRequirement.Operator == RequirementOperator.None)
                return new ParseErrorExpression("Incomplete trigger condition", expression);

            return null;
        }

        private ParseErrorExpression ExecuteAchievementMathematic(MathematicExpression mathematic, InterpreterScope scope)
        {
            ParseErrorExpression error;
            var context = scope.GetContext<TriggerBuilderContext>();

            var operation = mathematic.Operation;
            switch (operation)
            {
                case MathematicOperation.Add:
                case MathematicOperation.Subtract:
                    break;

                case MathematicOperation.Multiply:
                case MathematicOperation.Divide:
                    // generate the condition for the right side
                    Field operand;
                    if (mathematic.Right.Type == ExpressionType.IntegerConstant)
                    {
                        operand = new Field
                        {
                            Size = FieldSize.DWord,
                            Type = FieldType.Value,
                            Value = (uint)((IntegerConstantExpression)mathematic.Right).Value
                        };
                    }
                    else
                    {
                        var requirements = new List<Requirement>();
                        var innerContext = new TriggerBuilderContext() { Trigger = requirements };
                        var innerScope = new InterpreterScope(scope) { Context = innerContext };

                        error = ExecuteAchievementExpression(mathematic.Right, innerScope);
                        if (error != null)
                            return error;
                        if (requirements.Count > 1)
                            return new ParseErrorExpression("Multiplication by complex value not supported", mathematic);

                        operand = requirements[0].Left;
                    }

                    // generate the conditions for the left side
                    error = ExecuteAchievementExpression(mathematic.Left, scope);
                    if (error != null)
                        return error;

                    context.LastRequirement.Operator = (operation == MathematicOperation.Multiply) ?
                        RequirementOperator.Multiply : RequirementOperator.Divide;
                    context.LastRequirement.Right = operand;
                    return null;

                default:
                    return new ParseErrorExpression("Cannot normalize expression to eliminate " + MathematicExpression.GetOperatorType(operation), mathematic);
            }

            var left = mathematic.Left;

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
                        case RequirementType.AddAddress:
                            // AddAddress is allowed as long as it's not the last requirement
                            if (ReferenceEquals(requirement, requirements.Last()))
                                return new ParseErrorExpression("Cannot normalize expression for negation", mathematic);
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
                var lastRequirement = context.LastRequirement;
                if (lastRequirement.Type != RequirementType.SubSource)
                {
                    lastRequirement.Type = RequirementType.AddSource;
                    if (lastRequirement.IsComparison)
                    {
                        lastRequirement.Operator = RequirementOperator.None;
                        lastRequirement.Right = new Field();
                    }
                }
            }
            else
            {
                return new ParseErrorExpression(String.Format("Cannot normalize expression to eliminate {0}", MathematicExpression.GetOperatorType(mathematic.Operation)), mathematic);
            }

            var integerConstant = right as IntegerConstantExpression;
            if (integerConstant != null)
            {
                context.Trigger.Add(new Requirement
                {
                    Type = RequirementType.None,
                    Left = new Field { Type = FieldType.Value, Value = (uint)integerConstant.Value },
                    Operator = RequirementOperator.None,
                    Right = new Field()
                });
                return null;
            }

            // generate the condition for the second expression
            error = ExecuteAchievementExpression(right, scope);
            if (error != null)
                error = new ParseErrorExpression(error.Message, mathematic);

            // make sure the mathematic expression doesn't result in a comparison
            {
                var lastRequirement = context.LastRequirement;
                if (lastRequirement.IsComparison)
                {
                    lastRequirement.Operator = RequirementOperator.None;
                    lastRequirement.Right = new Field();
                }
            }

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
                    error = ExecuteAchievementClause(condition.Left, scope);
                    if (error != null)
                        return error;

                    error = ExecuteAchievementClause(condition.Right, scope);
                    if (error != null)
                        return error;

                    return null;

                case ConditionalOperation.Or:
                    BeginAlt(context);
                    error = ExecuteAchievementClause(condition.Left, scope);
                    if (error != null)
                        return error;

                    BeginAlt(context);
                    error = ExecuteAchievementClause(condition.Right, scope);
                    if (error != null)
                        return error;

                    return null;
            }

            return new ParseErrorExpression("Unsupported conditional", condition);
        }

        private static ParseErrorExpression HandleAddAddressComparison(ExpressionBase comparison,
            IList<Requirement> requirements, RequirementOperator op, Requirement extraRequirement)
        {
            // determine how long the AddAddress chain is
            var rightIndex = requirements.Count;
            while (requirements[rightIndex - 1].Type == RequirementType.AddAddress)
                rightIndex--;

            // attempt to match the AddAddress chain with the previous conditions
            bool match = true;
            var distance = requirements.Count - rightIndex;
            var leftIndex = rightIndex - 1;
            var leftStop = leftIndex - distance;
            var requirement = requirements[leftIndex]; // last element of left side
            while (leftIndex > leftStop)
            {
                // if the AddAddress portion doesn't match, skip over any remaining AddAddresses
                // for the previous condition, and bail
                if (requirements[leftIndex - 1] != requirements[leftIndex + distance])
                {
                    while (leftIndex > 0 && requirements[leftIndex - 1].Type == RequirementType.AddAddress)
                        --leftIndex;

                    match = false;
                    break;
                }

                --leftIndex;
            }

            // if the AddAddress chains match, then merge the conditions
            if (match)
            {
                while (requirements.Count > rightIndex)
                    requirements.RemoveAt(requirements.Count - 1);

                requirement.Operator = op;
                requirement.Right = extraRequirement.Left;
                return null;
            }

            // put the extra requirement back. we couldn't merge it with the left condition
            requirements.Add(extraRequirement);

            // AddAddress chains were not the same. Attempt to rearrange the logic using SubSource
            //           (if A cannot be moved)                        (if A can be moved)
            // A == B   ~>   A - B + 0 == 0   ~>   B - A     == 0   ~>   -A + B     == 0
            // A != B   ~>   A - B + 0 != 0   ~>   B - A     != 0   ~>   -A + B     != 0
            // A >  B   ~>   A - B + M >  M   ~>   B - A     <  0   ~>   -A + B     >  M  [leverage underflow]
            // A >= B   ~>   A - B + M >= M   ~>   B - A     <= 0   ~>   -A - 1 + B >= M  [leverage underflow]
            // A <  B   ~>   A - B + 0 >  M   ~>   B - A + M >  M   ~>   -A + B + M >  M  [avoid underflow]
            // A <= B   ~>   A - B + 0 >= M   ~>   B - A + M >= M   ~>   -A + B + M >= M  [avoid underflow]

            // calculate M
            uint maxValue = 0;
            if (op != RequirementOperator.Equal && op != RequirementOperator.NotEqual)
            {
                maxValue = Math.Max(Field.GetMaxValue(requirement.Left.Size), Field.GetMaxValue(extraRequirement.Left.Size));
                if (maxValue == 0xFFFFFFFF)
                    return new ParseErrorExpression("Indirect memory addresses must match on both sides of a comparison for 32-bit values", comparison);
            }

            // if A is preceded by an AddSource or SubSource, we can't change it to a SubSource
            Requirement prevReq = null;
            bool cannotBeChanged = false;

            if (leftIndex > 0)
            {
                prevReq = requirements[leftIndex - 1];
                cannotBeChanged =
                    (prevReq.Type == RequirementType.AddSource || prevReq.Type == RequirementType.SubSource);
            }

            if (cannotBeChanged)
            {
                requirement.Type = RequirementType.AddSource;
                extraRequirement.Type = RequirementType.SubSource;

                uint leftValue = 0;
                switch (op)
                {
                    case RequirementOperator.GreaterThan:
                    case RequirementOperator.GreaterThanOrEqual:
                        leftValue = maxValue;
                        break;

                    case RequirementOperator.LessThan:
                        op = RequirementOperator.GreaterThan;
                        break;

                    case RequirementOperator.LessThanOrEqual:
                        op = RequirementOperator.GreaterThanOrEqual;
                        break;
                }

                // if preceeded by a constant, merge the constant into the final condition
                if (prevReq.Left.Type == FieldType.Value)
                {
                    if (prevReq.Type == RequirementType.AddSource)
                        leftValue += prevReq.Left.Value;
                    else if (prevReq.Left.Value < leftValue)
                        leftValue -= prevReq.Left.Value;
                    else
                        maxValue += prevReq.Left.Value;

                    requirements.RemoveAt(leftIndex - 1);
                }

                extraRequirement = new Requirement();
                extraRequirement.Left = new Field { Type = FieldType.Value, Value = leftValue };
                extraRequirement.Operator = op;
                extraRequirement.Right = new Field { Type = FieldType.Value, Value = maxValue };

                requirements.Add(extraRequirement);
                return null;
            }

            // if A can be changed, make it a SubSource
            requirement.Type = RequirementType.SubSource;

            switch (op)
            {
                case RequirementOperator.GreaterThanOrEqual: // -A - 1 + B >= M
                    // subtract 1 from (B-A) in case B==A, so the result will still be negative
                    requirements.Insert(rightIndex, new Requirement
                    {
                        Type = RequirementType.SubSource,
                        Left = new Field { Type = FieldType.Value, Value = 1 }
                    });
                    goto case RequirementOperator.GreaterThan;

                case RequirementOperator.Equal:              // -A + B     == 0
                case RequirementOperator.NotEqual:           // -A + B     != 0
                case RequirementOperator.GreaterThan:        // -A + B     >  M
                    extraRequirement.Operator = op;
                    extraRequirement.Right = new Field { Type = FieldType.Value, Value = maxValue };
                    break;

                case RequirementOperator.LessThan:           // -A + B + M >  M
                case RequirementOperator.LessThanOrEqual:    // -A + B + M >= M
                    extraRequirement.Type = RequirementType.AddSource;

                    extraRequirement = new Requirement();
                    extraRequirement.Left = extraRequirement.Right =
                        new Field { Type = FieldType.Value, Value = maxValue };
                    extraRequirement.Operator = (op == RequirementOperator.LessThan) ?
                        RequirementOperator.GreaterThan : RequirementOperator.GreaterThanOrEqual;

                    requirements.Add(extraRequirement);
                    break;
            }

            return null;
        }

        private ParseErrorExpression ExecuteAchievementComparison(ComparisonExpression comparison, InterpreterScope scope)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
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
                return ParseErrorExpression.WrapError(error, "Invalid value", left);

            var integerRight = right as IntegerConstantExpression;
            if (integerRight != null)
            {
                int newValue = integerRight.Value;

                var lastRequirement = context.LastRequirement;
                if (lastRequirement.Operator != RequirementOperator.None)
                {
                    // try to rearrange so the last requirement doesn't have a modifier
                    for (int i = context.Trigger.Count - 2; i >= 0; --i)
                    {
                        var requirement = context.Trigger.ElementAt(i);
                        if (requirement.Type == RequirementType.AddSource && requirement.Operator == RequirementOperator.None)
                        {
                            context.Trigger.Remove(lastRequirement);
                            context.Trigger.Add(new Requirement { Left = requirement.Left });
                            requirement.Left = lastRequirement.Left;
                            requirement.Operator = lastRequirement.Operator;
                            requirement.Right = lastRequirement.Right;
                            lastRequirement = context.LastRequirement;
                            break;
                        }

                        if (requirement.Type != RequirementType.AddSource &&
                            requirement.Type != RequirementType.SubSource &&
                            requirement.Type != RequirementType.AddAddress)
                        {
                            break;
                        }
                    }

                    if (lastRequirement.Operator != RequirementOperator.None)
                    {
                        // last requirement still has a modifier, have to add a dummy condition.
                        lastRequirement.Type = RequirementType.AddSource;
                        context.Trigger.Add(new Requirement { Left = new Field { Size = lastRequirement.Left.Size, Type = FieldType.Value, Value = 0 } });
                        lastRequirement = context.LastRequirement;
                    }
                }

                lastRequirement.Operator = op;
                lastRequirement.Right = new Field { Size = lastRequirement.Left.Size, Type = FieldType.Value, Value = (uint)newValue };
            }
            else
            {
                error = ExecuteAchievementExpression(right, scope);
                if (error != null)
                    return ParseErrorExpression.WrapError(error, "Invalid value", right);

                var extraRequirement = context.LastRequirement;
                ((IList<Requirement>)context.Trigger).RemoveAt(context.Trigger.Count - 1);

                var requirement = context.LastRequirement;
                if (requirement != null)
                {
                    if (requirement.Type == RequirementType.AddAddress)
                    {
                        return HandleAddAddressComparison(comparison, (IList<Requirement>)context.Trigger, op, extraRequirement);
                    }
                    else if (context.Trigger.Count > 1 && context.Trigger.ElementAt(context.Trigger.Count - 2).Type == RequirementType.AddAddress)
                    {
                        // if left side is an AddAddress chain, but right side is a not, we have to keep the
                        // dummy condition to prevent the AddAddress from modifying the memory address on the
                        // right side. integers are handled above.
                        requirement.Type = RequirementType.AddSource;
                        extraRequirement.Right = extraRequirement.Left;
                        extraRequirement.Left = new Field { Type = FieldType.Value, Value = 0 };
                        extraRequirement.Operator = op;
                        context.Trigger.Add(extraRequirement);
                    }
                    else
                    {
                        // no AddAddress on either side, just merge the conditions
                        requirement.Operator = op;
                        requirement.Right = extraRequirement.Left;
                    }
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
            ExpressionBase evaluated;
            if (!functionCall.ReplaceVariables(scope, out evaluated))
                return (ParseErrorExpression)evaluated;

            functionCall = evaluated as FunctionCallExpression;
            if (functionCall != null)
            {
                var context = scope.GetContext<TriggerBuilderContext>();
                return context.CallFunction(functionCall, scope);
            }

            return ExecuteAchievementClause(evaluated, scope);
        }
    }
}