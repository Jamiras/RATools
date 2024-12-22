using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RATools.Parser.Internal
{
    internal class RequirementMerger
    {
        private enum MoreRestrictiveRequirement
        {
            None = 0,
            Left,
            Right,
            Same,
            Conflict,
            Everything,
            LeftMergeOperator,
            RightMergeOperator,
        }

        public static KeyValuePair<RequirementOperator, IComparable> MergeComparisons(
            RequirementOperator comparison1, IComparable value1, RequirementOperator comparison2, IComparable value2,
            ConditionalOperation mergeCondition)
        {
            RequirementOperator mergedOperator;
            IComparable mergedValue;

            if (value1 == value2)
            {
                // same value, just merge operators
                mergedValue = value1;
                if (mergeCondition == ConditionalOperation.And)
                    mergedOperator = MergeOperatorsAnd(comparison1, comparison2);
                else
                    mergedOperator = MergeOperatorsOr(comparison1, comparison2);
            }
            else
            {
                if (mergeCondition == ConditionalOperation.Or)
                {
                    comparison1 = comparison1.OppositeOperator();
                    comparison2 = comparison2.OppositeOperator();
                }

                switch (GetMoreRestrictiveRequirement(comparison1, value1, comparison2, value2))
                {
                    case MoreRestrictiveRequirement.None:
                        mergedOperator = RequirementOperator.None;
                        mergedValue = 0;
                        break;

                    case MoreRestrictiveRequirement.Conflict:
                        mergedOperator = RequirementOperator.Divide; // signal for always false
                        mergedValue = 0;
                        break;

                    default:
                    case MoreRestrictiveRequirement.Left:
                    case MoreRestrictiveRequirement.Same:
                        mergedOperator = comparison1;
                        mergedValue = value1;
                        break;

                    case MoreRestrictiveRequirement.Right:
                        mergedOperator = comparison2;
                        mergedValue = value2;
                        break;

                    case MoreRestrictiveRequirement.LeftMergeOperator:
                        mergedOperator = MergeOperatorsAnd(comparison1, comparison2);
                        mergedValue = value1;
                        break;

                    case MoreRestrictiveRequirement.RightMergeOperator:
                        mergedOperator = MergeOperatorsAnd(comparison1, comparison2);
                        mergedValue = value2;
                        break;
                }

                if (mergeCondition == ConditionalOperation.Or)
                {
                    // assert: does not affect None, Multiply, or Divide
                    switch (mergedOperator)
                    {
                        case RequirementOperator.None:
                            break;

                        case RequirementOperator.Divide:
                            mergedOperator = RequirementOperator.Multiply; // signal for always_true
                            break;

                        default:
                            mergedOperator = mergedOperator.OppositeOperator();
                            break;
                    }
                }
            }

            return new KeyValuePair<RequirementOperator, IComparable>(mergedOperator, mergedValue);
        }

        private static MoreRestrictiveRequirement GetMoreRestrictiveRequirement(
            RequirementOperator comparison1, IComparable value1, RequirementOperator comparison2, IComparable value2)
        {
            var diff = value1.CompareTo(value2);

            switch (comparison2)                                    //  comp1    comp2  |  comp1    comp2  |  comp1    comp2
            {                                                       // value1 == value2 | value1 <  value2 | value1 >  value2  
                case RequirementOperator.Equal:
                    switch (comparison1)
                    {
                        case RequirementOperator.Equal:             // a == 1 && a == 1 | a == 1 && a == 2 | a == 2 && a == 1
                            return diff == 0 ? MoreRestrictiveRequirement.Same : MoreRestrictiveRequirement.Conflict;
                        case RequirementOperator.LessThanOrEqual:   // a <= 1 && a == 1 | a <= 1 && a == 2 | a <= 2 && a == 1
                            return diff < 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.GreaterThanOrEqual:// a >= 1 && a == 1 | a >= 1 && a == 2 | a >= 2 && a == 1
                            return diff > 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.NotEqual:          // a != 1 && a == 1 | a != 1 && a == 2 | a != 2 && a == 1
                            return diff == 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.LessThan:          // a <  1 && a == 1 | a <  1 && a == 2 | a <  2 && a == 1
                            return diff <= 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.GreaterThan:       // a >  1 && a == 1 | a >  1 && a == 2 | a >  2 && a == 1
                            return diff >= 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.Right;
                        default:
                            break;
                    }
                    break;

                case RequirementOperator.NotEqual:
                    switch (comparison1)
                    {
                        case RequirementOperator.Equal:             // a == 1 && a != 1 | a == 1 && a != 2 | a == 2 && a != 1
                            return diff == 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.Left;
                        case RequirementOperator.LessThanOrEqual:   // a <= 1 && a != 1 | a <= 1 && a != 2 | a <= 2 && a != 1
                            return diff == 0 ? MoreRestrictiveRequirement.LeftMergeOperator :
                                diff < 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.None;
                        case RequirementOperator.GreaterThanOrEqual:// a >= 1 && a != 1 | a >= 1 && a != 2 | a >= 2 && a != 1
                            return diff == 0 ? MoreRestrictiveRequirement.LeftMergeOperator :
                                diff > 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.None;
                        case RequirementOperator.NotEqual:          // a != 1 && a != 1 | a != 1 && a != 2 | a != 2 && a != 1
                            return diff == 0 ? MoreRestrictiveRequirement.Same : MoreRestrictiveRequirement.None;
                        case RequirementOperator.LessThan:          // a <  1 && a != 1 | a <  1 && a != 2 | a <  2 && a != 1
                            return diff <= 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.None;
                        case RequirementOperator.GreaterThan:       // a >  1 && a != 1 | a >  1 && a != 2 | a >  2 && a != 1
                            return diff >= 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.None;
                        default:
                            break;
                    }
                    break;

                case RequirementOperator.LessThan:
                    switch (comparison1)
                    {
                        case RequirementOperator.Equal:             // a == 1 && a <  1 | a == 1 && a <  2 | a == 2 && a <  1
                            return diff < 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.Conflict;
                        case RequirementOperator.LessThanOrEqual:   // a <= 1 && a <  1 | a <= 1 && a <  2 | a <= 2 && a <  1
                            return diff < 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.GreaterThanOrEqual:// a >= 1 && a <  1 | a >= 1 && a <  2 | a >= 2 && a <  1
                            return diff < 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Conflict;
                        case RequirementOperator.NotEqual:          // a != 1 && a <  1 | a != 1 && a <  2 | a != 2 && a <  1
                            return diff < 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.LessThan:          // a <  1 && a <  1 | a <  1 && a <  2 | a <  2 && a <  1
                            return diff == 0 ? MoreRestrictiveRequirement.Same :
                                diff < 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.GreaterThan:       // a >  1 && a <  1 | a >  1 && a <  2 | a >  2 && a <  1
                            return diff < 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Conflict;
                        default:
                            break;
                    }
                    break;

                case RequirementOperator.LessThanOrEqual:
                    switch (comparison1)
                    {
                        case RequirementOperator.Equal:             // a == 1 && a <= 1 | a == 1 && a <= 2 | a == 2 && a <= 1
                            return diff > 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.Left;
                        case RequirementOperator.LessThanOrEqual:   // a <= 1 && a <= 1 | a <= 1 && a <= 2 | a <= 2 && a <= 1
                            return diff == 0 ? MoreRestrictiveRequirement.Same :
                                diff < 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.GreaterThanOrEqual:// a >= 1 && a <= 1 | a >= 1 && a <= 2 | a >= 2 && a <= 1
                            return diff == 0 ? MoreRestrictiveRequirement.LeftMergeOperator :
                                diff < 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Conflict;
                        case RequirementOperator.NotEqual:          // a != 1 && a <= 1 | a != 1 && a <= 2 | a != 2 && a <= 1
                            return diff == 0 ? MoreRestrictiveRequirement.RightMergeOperator :
                                diff < 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.LessThan:          // a <  1 && a <= 1 | a <  1 && a <= 2 | a <  2 && a <= 1
                            return diff > 0 ? MoreRestrictiveRequirement.Right : MoreRestrictiveRequirement.Left;
                        case RequirementOperator.GreaterThan:       // a >  1 && a <= 1 | a >  1 && a <= 2 | a >  2 && a <= 1
                            return diff < 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Conflict;
                        default:
                            break;
                    }
                    break;

                case RequirementOperator.GreaterThan:
                    switch (comparison1)
                    {
                        case RequirementOperator.Equal:             // a == 1 && a >  1 | a == 1 && a >  2 | a == 2 && a >  1
                            return diff > 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.Conflict;
                        case RequirementOperator.LessThanOrEqual:   // a <= 1 && a >  1 | a <= 1 && a >  2 | a <= 2 && a >  1
                            return diff > 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Conflict;
                        case RequirementOperator.GreaterThanOrEqual:// a >= 1 && a >  1 | a >= 1 && a >  2 | a >= 2 && a >  1
                            return diff > 0 ? MoreRestrictiveRequirement.Left : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.NotEqual:          // a != 1 && a >  1 | a != 1 && a >  2 | a != 2 && a >  1
                            return diff > 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Right;
                        case RequirementOperator.LessThan:          // a <  1 && a >  1 | a <  1 && a >  2 | a <  2 && a >  1
                            return diff > 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Conflict;
                        case RequirementOperator.GreaterThan:       // a >  1 && a >  1 | a >  1 && a >  2 | a >  2 && a >  1
                            return diff == 0 ? MoreRestrictiveRequirement.Same :
                                diff < 0 ? MoreRestrictiveRequirement.Right : MoreRestrictiveRequirement.Left;
                        default:
                            break;
                    }
                    break;

                case RequirementOperator.GreaterThanOrEqual:
                    switch (comparison1)
                    {
                        case RequirementOperator.Equal:             // a == 1 && a >= 1 | a == 1 && a >= 2 | a == 2 && a >= 1
                            return diff < 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.Left;
                        case RequirementOperator.LessThanOrEqual:   // a <= 1 && a >= 1 | a <= 1 && a >= 2 | a <= 2 && a >= 1
                            return diff == 0 ? MoreRestrictiveRequirement.LeftMergeOperator :
                                diff < 0 ? MoreRestrictiveRequirement.Conflict : MoreRestrictiveRequirement.None;
                        case RequirementOperator.GreaterThanOrEqual:// a >= 1 && a >= 1 | a >= 1 && a >= 2 | a >= 2 && a >= 1
                            return diff == 0 ? MoreRestrictiveRequirement.Same :
                                diff < 0 ? MoreRestrictiveRequirement.Right : MoreRestrictiveRequirement.Left;
                        case RequirementOperator.NotEqual:          // a != 1 && a >= 1 | a != 1 && a >= 2 | a != 2 && a >= 1
                            return diff == 0 ? MoreRestrictiveRequirement.RightMergeOperator :
                                diff < 0 ? MoreRestrictiveRequirement.Right : MoreRestrictiveRequirement.None;
                        case RequirementOperator.LessThan:          // a <  1 && a >= 1 | a <  1 && a >= 2 | a <  2 && a >= 1
                            return diff > 0 ? MoreRestrictiveRequirement.None : MoreRestrictiveRequirement.Conflict;
                        case RequirementOperator.GreaterThan:       // a >  1 && a >= 1 | a >  1 && a >= 2 | a >  2 && a >= 1
                            return diff < 0 ? MoreRestrictiveRequirement.Right : MoreRestrictiveRequirement.Left;
                        default:
                            break;
                    }
                    break;

            }

            return MoreRestrictiveRequirement.None;
        }

        private static RequirementOperator MergeOperatorsAnd(RequirementOperator left, RequirementOperator right)
        {
            switch (left)
            {
                case RequirementOperator.LessThanOrEqual:
                    switch (right)
                    {
                        case RequirementOperator.LessThan:
                        case RequirementOperator.NotEqual: return RequirementOperator.LessThan;
                        case RequirementOperator.Equal:
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.Equal;
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.LessThanOrEqual;
                        case RequirementOperator.GreaterThan: return RequirementOperator.Divide; // None
                        default: break;
                    }
                    break;

                case RequirementOperator.GreaterThanOrEqual:
                    switch (right)
                    {
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.NotEqual: return RequirementOperator.GreaterThan;
                        case RequirementOperator.Equal:
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.Equal;
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                        case RequirementOperator.LessThan: return RequirementOperator.Divide; // None
                        default: break;
                    }
                    break;

                case RequirementOperator.NotEqual:
                    switch (right)
                    {
                        case RequirementOperator.Equal: return RequirementOperator.Divide; // None
                        case RequirementOperator.NotEqual: return RequirementOperator.NotEqual;
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.GreaterThan;
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.LessThan;
                        case RequirementOperator.GreaterThan: return RequirementOperator.GreaterThan;
                        case RequirementOperator.LessThan: return RequirementOperator.LessThan;
                        default: break;
                    }
                    break;

                case RequirementOperator.Equal:
                    switch (right)
                    {
                        case RequirementOperator.GreaterThanOrEqual:
                        case RequirementOperator.LessThanOrEqual:
                        case RequirementOperator.Equal: return RequirementOperator.Equal;
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.LessThan:
                        case RequirementOperator.NotEqual: return RequirementOperator.Divide; // None
                        default: break;
                    }
                    break;

                case RequirementOperator.GreaterThan:
                    switch (right)
                    {
                        case RequirementOperator.NotEqual:
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.GreaterThan;
                        case RequirementOperator.LessThan:
                        case RequirementOperator.LessThanOrEqual:
                        case RequirementOperator.Equal: return RequirementOperator.Divide; // None
                        default: break;
                    }
                    break;

                case RequirementOperator.LessThan:
                    switch (right)
                    {
                        case RequirementOperator.NotEqual:
                        case RequirementOperator.LessThan:
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.LessThan;
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.GreaterThanOrEqual:
                        case RequirementOperator.Equal: return RequirementOperator.Divide; // None
                        default: break;
                    }
                    break;
            }

            return RequirementOperator.None;
        }

        private static RequirementOperator MergeOperatorsOr(RequirementOperator left, RequirementOperator right)
        {
            switch (left)
            {
                case RequirementOperator.LessThanOrEqual:
                    switch (right)
                    {
                        case RequirementOperator.NotEqual:
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.Multiply; // All
                        case RequirementOperator.Equal:
                        case RequirementOperator.LessThan:
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.LessThanOrEqual;
                        default: break;
                    }
                    break;

                case RequirementOperator.GreaterThanOrEqual:
                    switch (right)
                    {
                        case RequirementOperator.NotEqual:
                        case RequirementOperator.LessThan:
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.Multiply; // All
                        case RequirementOperator.Equal:
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                        default: break;
                    }
                    break;

                case RequirementOperator.NotEqual:
                    switch (right)
                    {
                        case RequirementOperator.GreaterThanOrEqual:
                        case RequirementOperator.LessThanOrEqual:
                        case RequirementOperator.Equal: return RequirementOperator.Multiply; // All
                        case RequirementOperator.LessThan:
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.NotEqual: return RequirementOperator.NotEqual;
                        default: break;
                    }
                    break;

                case RequirementOperator.Equal:
                    switch (right)
                    {
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                        case RequirementOperator.LessThan:
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.LessThanOrEqual;
                        case RequirementOperator.Equal: return RequirementOperator.Equal;
                        case RequirementOperator.NotEqual: return RequirementOperator.Multiply; // All
                        default: break;
                    }
                    break;

                case RequirementOperator.GreaterThan:
                    switch (right)
                    {
                        case RequirementOperator.LessThan:
                        case RequirementOperator.NotEqual: return RequirementOperator.NotEqual;
                        case RequirementOperator.GreaterThan: return RequirementOperator.GreaterThan;
                        case RequirementOperator.Equal:
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.Multiply; // All
                        default: break;
                    }
                    break;

                case RequirementOperator.LessThan:
                    switch (right)
                    {
                        case RequirementOperator.GreaterThan:
                        case RequirementOperator.NotEqual: return RequirementOperator.NotEqual;
                        case RequirementOperator.LessThan: return RequirementOperator.LessThan;
                        case RequirementOperator.Equal:
                        case RequirementOperator.LessThanOrEqual: return RequirementOperator.LessThanOrEqual;
                        case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.Multiply; //All
                        default: break;
                    }
                    break;
            }

            return RequirementOperator.None;
        }

        private static Requirement MergeRequirements(Requirement left, Requirement right, MoreRestrictiveRequirement moreRestrictive)
        {
            // create a copy that we can modify
            var merged = new Requirement
            {
                Type = left.Type,
                Left = left.Left,
                HitCount = Math.Max(left.HitCount, right.HitCount)
            };

            switch (moreRestrictive)
            {
                case MoreRestrictiveRequirement.None:
                    // can't be merged
                    return null;

                case MoreRestrictiveRequirement.Conflict:
                    // these are allowed to conflict with each other
                    if (left.HitCount > 0 || right.HitCount > 0)
                        return null;
                    if (left.Type == RequirementType.PauseIf)
                        return null;
                    if (left.Type == RequirementType.ResetIf)
                        return null;

                    // conditions conflict with each other, trigger is impossible
                    return AlwaysFalseFunction.CreateAlwaysFalseRequirement();

                case MoreRestrictiveRequirement.Same:
                case MoreRestrictiveRequirement.Left:
                    merged.Operator = left.Operator;
                    merged.Right = left.Right;
                    return merged;

                case MoreRestrictiveRequirement.Right:
                    merged.Operator = right.Operator;
                    merged.Right = right.Right;
                    return merged;

                case MoreRestrictiveRequirement.LeftMergeOperator:
                    merged.Operator = MergeOperatorsAnd(left.Operator, right.Operator);
                    merged.Right = left.Right;
                    return merged;

                case MoreRestrictiveRequirement.RightMergeOperator:
                    merged.Operator = MergeOperatorsAnd(left.Operator, right.Operator);
                    merged.Right = right.Right;
                    return merged;

                default:
                    return null;
            }
        }

        public static Requirement MergeRequirements(Requirement left, Requirement right, ConditionalOperation condition)
        {
            Requirement merged;
            MergeRequirements(left, right, condition, out merged);
            return merged;
        }

        private static MoreRestrictiveRequirement MergeRequirements(Requirement left, Requirement right, ConditionalOperation condition, out Requirement merged)
        {
            merged = null;

            // only allow merging if doing a comparison where both left sides are the same and have the same flag
            if (left.Type != right.Type || left.Left != right.Left || !left.IsComparison)
                return MoreRestrictiveRequirement.None;

            if (left.HitCount != right.HitCount)
            {
                // cannot merge hit counts if either of them is infinite
                // otherwise, the higher of the two will be selected when doing the merge
                if (left.HitCount == 0 || right.HitCount == 0)
                    return MoreRestrictiveRequirement.None;
            }
            else if (left.Operator == right.Operator && left.Right == right.Right)
            {
                // 100% match, use it
                merged = left;
                return MoreRestrictiveRequirement.Same;
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

            MoreRestrictiveRequirement moreRestrictive = MoreRestrictiveRequirement.None;
            if (left.Right.Type != FieldType.Value || right.Right.Type != FieldType.Value)
            {
                // if either right is not a value field, then we can't merge
                merged = null;

                if (left.Right == right.Right)
                {
                    // unless they're the same value, then we can merge operators
                    RequirementOperator mergedOperator;
                    if (condition == ConditionalOperation.And)
                        mergedOperator = MergeOperatorsAnd(left.Operator, right.Operator);
                    else
                        mergedOperator = MergeOperatorsOr(left.Operator, right.Operator);

                    if (mergedOperator == RequirementOperator.Multiply)
                    {
                        merged = AlwaysTrueFunction.CreateAlwaysTrueRequirement();
                        moreRestrictive = MoreRestrictiveRequirement.Everything;
                    }
                    else if (mergedOperator == RequirementOperator.Divide)
                    {
                        merged = AlwaysFalseFunction.CreateAlwaysFalseRequirement();
                        moreRestrictive = MoreRestrictiveRequirement.Conflict;
                    }
                    else if (mergedOperator != RequirementOperator.None)
                    {
                        merged = MergeRequirements(left, right, MoreRestrictiveRequirement.Same);
                        merged.Operator = mergedOperator;

                        if (mergedOperator == left.Operator)
                            moreRestrictive = MoreRestrictiveRequirement.Left;
                        else if (mergedOperator == right.Operator)
                            moreRestrictive = MoreRestrictiveRequirement.Right;
                        else
                            moreRestrictive = MoreRestrictiveRequirement.LeftMergeOperator;
                    }
                }
            }
            else
            {
                // both right fields are values, see if there's overlap
                if (condition == ConditionalOperation.And)
                {
                    // AND logic is straightforward
                    moreRestrictive = GetMoreRestrictiveRequirement(left.Operator, left.Right.Value, right.Operator, right.Right.Value);
                    merged = MergeRequirements(left, right, moreRestrictive);

                    if (merged == null) // merge could not be performed
                        moreRestrictive = MoreRestrictiveRequirement.None;
                }
                else
                {
                    // OR logic has to be doubly inverted:   A || B  ~>  !A && !B  ~>  C  ~>  !C
                    var notLeft = new Requirement
                    {
                        Type = left.Type,
                        Left = left.Left,
                        Operator = left.Operator.OppositeOperator(),
                        Right = left.Right,
                        HitCount = left.HitCount
                    };

                    var notRight = new Requirement
                    {
                        Type = right.Type,
                        Left = right.Left,
                        Operator = right.Operator.OppositeOperator(),
                        Right = right.Right,
                        HitCount = right.HitCount
                    };

                    moreRestrictive = GetMoreRestrictiveRequirement(notLeft.Operator, notLeft.Right.Value, notRight.Operator, notRight.Right.Value);
                    merged = MergeRequirements(notLeft, notRight, moreRestrictive);

                    if (merged == null)
                    {
                        // merge could not be performed
                        moreRestrictive = MoreRestrictiveRequirement.None;
                    }
                    else
                    {
                        if (moreRestrictive == MoreRestrictiveRequirement.Conflict)
                        {
                            // a conflict with the opposing conditions means all values will match when we invert again
                            // expect merged to be the always_false() function and convert it to always_true()
                            Debug.Assert(merged.Evaluate() == false);
                            merged = AlwaysTrueFunction.CreateAlwaysTrueRequirement();
                            moreRestrictive = MoreRestrictiveRequirement.Everything;
                        }
                        else
                        {
                            merged.Operator = merged.Operator.OppositeOperator();
                        }
                    }
                }
            }

            return moreRestrictive;
        }

        public static RequirementEx MergeRequirements(RequirementEx first, RequirementEx second, ConditionalOperation condition)
        {
            RequirementEx merged;
            MergeRequirements(first, second, condition, out merged);
            return merged;
        }

        private static MoreRestrictiveRequirement MergeRequirements(RequirementEx first, RequirementEx second, ConditionalOperation condition, out RequirementEx merged)
        {
            merged = null;

            if (first.Requirements.Count != second.Requirements.Count)
                return MoreRestrictiveRequirement.Conflict;

            Requirement mergedRequirement;
            var result = new RequirementEx();

            bool hasMerge = false;
            bool hasMoreRestrictiveLeft = false;
            bool hasMoreRestrictiveRight = false;
            bool hasConflict = false;
            bool hasEverything = false;
            for (int i = 0; i < first.Requirements.Count; i++)
            {
                var moreRestrictive = MergeRequirements(first.Requirements[i], second.Requirements[i], condition, out mergedRequirement);
                switch (moreRestrictive)
                {
                    case MoreRestrictiveRequirement.None:
                        // could not be merged
                        return MoreRestrictiveRequirement.None;

                    case MoreRestrictiveRequirement.Conflict:
                        hasConflict = true;
                        break;

                    case MoreRestrictiveRequirement.Everything:
                        hasEverything = true;
                        break;

                    case MoreRestrictiveRequirement.LeftMergeOperator:
                        if (hasMerge) // can only merge one subclause
                        {
                            hasConflict = true;
                            break;
                        }
                        hasMerge = true;
                        goto case MoreRestrictiveRequirement.Left;

                    case MoreRestrictiveRequirement.Left:
                        if (hasMoreRestrictiveRight) // can only normalize to one side or the other
                        {
                            hasConflict = true;
                            break;
                        }
                        hasMoreRestrictiveLeft = true;
                        break;

                    case MoreRestrictiveRequirement.RightMergeOperator:
                        if (hasMerge) // can only merge one subclause
                        {
                            hasConflict = true;
                            break;
                        }
                        hasMerge = true;
                        goto case MoreRestrictiveRequirement.Right;

                    case MoreRestrictiveRequirement.Right:
                        if (hasMoreRestrictiveLeft) // can only normalize to one side or the other
                        {
                            hasConflict = true;
                            break;
                        }
                        hasMoreRestrictiveRight = true;
                        break;

                    case MoreRestrictiveRequirement.Same:
                        break;

                    default:
                        if (mergedRequirement == null)
                            return MoreRestrictiveRequirement.None;
                        break;
                }

                result.Requirements.Add(mergedRequirement);
            }

            merged = result;

            if (hasConflict)
            {
                result.Requirements.Clear();
                result.Requirements.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
                return MoreRestrictiveRequirement.Conflict;
            }

            if (hasEverything)
            {
                result.Requirements.Clear();
                result.Requirements.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                return MoreRestrictiveRequirement.Everything;
            }

            if (hasMoreRestrictiveLeft)
                return hasMerge ? MoreRestrictiveRequirement.LeftMergeOperator : MoreRestrictiveRequirement.Left;
            if (hasMoreRestrictiveRight)
                return hasMerge ? MoreRestrictiveRequirement.RightMergeOperator : MoreRestrictiveRequirement.Right;
            return MoreRestrictiveRequirement.Same;
        }

        [DebuggerDisplay("Requirements.Count={Requirements.Count}, IsMergeable={IsMergeable}")]
        private class RequirementGroupInfo
        {
            public RequirementGroupInfo(List<RequirementEx> requirements)
            {
                Requirements = requirements;
                Addresses = new HashSet<uint>();
                IsMergeable = true;
            }

            public List<RequirementEx> Requirements { get; private set; }
            public HashSet<uint> Addresses { get; private set; }

            public bool IsMergeable { get; set; }
        }

        private static MoreRestrictiveRequirement MergeRequirementGroups(RequirementGroupInfo left, RequirementGroupInfo right, ConditionalOperation condition)
        {
            // if one group has more requirements, make sure it's on the right
            if (left.Requirements.Count > right.Requirements.Count)
            {
                var result = MergeRequirementGroups(right, left, condition);
                switch (result)
                {
                    case MoreRestrictiveRequirement.Left:
                        return MoreRestrictiveRequirement.Right;
                    case MoreRestrictiveRequirement.Right:
                        return MoreRestrictiveRequirement.Left;
                    default:
                        return result;
                }
            }

            // make sure the smaller group is dependent on the same set of addresses as the larger group
            if (!left.Addresses.All(a => right.Addresses.Contains(a)))
                return MoreRestrictiveRequirement.Conflict;

            // try to merge them
            bool hasMerge = false;
            bool hasConflict = false;
            int everythingCount = 0;
            MoreRestrictiveRequirement moreRestrictive = MoreRestrictiveRequirement.Same;
            RequirementEx[] merged = new RequirementEx[left.Requirements.Count];
            bool[] matches = new bool[right.Requirements.Count];

            for (int i = 0; i < left.Requirements.Count; i++)
            {
                bool matched = false;

                for (int j = 0; j < right.Requirements.Count; j++)
                {
                    if (matches[j]) // already matched this item, ignore it
                        continue;

                    var moreRestrictiveRequirement = MergeRequirements(
                        left.Requirements[i], right.Requirements[j], condition, out merged[i]);
                    switch (moreRestrictiveRequirement)
                    {
                        case MoreRestrictiveRequirement.LeftMergeOperator:
                            if (hasMerge) // only allowed to merge operator at a time
                                hasConflict = true;
                            hasMerge = true;
                            goto case MoreRestrictiveRequirement.Left;

                        case MoreRestrictiveRequirement.Left:
                            if (moreRestrictive == MoreRestrictiveRequirement.Right)
                                return MoreRestrictiveRequirement.Conflict;
                            moreRestrictive = MoreRestrictiveRequirement.Left;
                            break;

                        case MoreRestrictiveRequirement.RightMergeOperator:
                            if (hasMerge) // only allowed to merge operator at a time
                                hasConflict = true;
                            hasMerge = true;
                            goto case MoreRestrictiveRequirement.Right;

                        case MoreRestrictiveRequirement.Right:
                            if (moreRestrictive == MoreRestrictiveRequirement.Left)
                                return MoreRestrictiveRequirement.Conflict;
                            moreRestrictive = MoreRestrictiveRequirement.Right;
                            break;

                        case MoreRestrictiveRequirement.Same:
                            break;

                        case MoreRestrictiveRequirement.Conflict:
                            hasConflict = true;
                            break;

                        case MoreRestrictiveRequirement.Everything:
                            ++everythingCount;
                            break;

                        default:
                            continue;
                    }

                    matched = true;
                    matches[j] = true;
                    break;
                }

                if (!matched)
                    return MoreRestrictiveRequirement.None;
            }

            // the smaller group can be merged into the larger group
            if (right.Requirements.Count > left.Requirements.Count)
            {
                // if one or more conditions conflicts with its partner, we have to assume the extra
                // conditions are differentiators and we can't merge
                if (everythingCount != 0 || hasConflict)
                    return MoreRestrictiveRequirement.None;

                switch (moreRestrictive)
                {
                    case MoreRestrictiveRequirement.Same:
                        moreRestrictive = MoreRestrictiveRequirement.Left;
                        break;

                    case MoreRestrictiveRequirement.Left:
                        // if left fully encompasses the matched conditions of right, we can discard the
                        // extra conditions. if an operator had to be changed (hasMerge=true), we cannot.
                        if (hasMerge)
                            return MoreRestrictiveRequirement.None;
                        break;

                    case MoreRestrictiveRequirement.Right:
                        // if a subset of the right side fully encompasses the left, it's still more
                        // restrictive due to the extra conditions.
                        return MoreRestrictiveRequirement.None;

                    default:
                        return MoreRestrictiveRequirement.Conflict;
                }
            }

            // all conditions matched. if there was a Conflict, the whole thing failed
            if (hasConflict)
                return MoreRestrictiveRequirement.Conflict;

            // success, replace the more restrictive group's contents with the merged results
            RequirementGroupInfo resultGroup = left;
            switch (moreRestrictive)
            {
                case MoreRestrictiveRequirement.Same:
                    // Same is the default state of moreRestrictive, which means
                    // we only found Same, Conflicts or Everythings.

                    // if every pair of conditions conflicted with each other, the groups are mutually exclusive
                    if (everythingCount == left.Requirements.Count)
                        return MoreRestrictiveRequirement.None;

                    if (everythingCount > 0)
                    {
                        // if at least one requirement was merged into an always_true, replace the left
                        moreRestrictive = MoreRestrictiveRequirement.Left;
                        goto case MoreRestrictiveRequirement.Left;
                    }
                    break;

                case MoreRestrictiveRequirement.Left:
                    left.Requirements.Clear();
                    left.Requirements.AddRange(merged);
                    break;

                case MoreRestrictiveRequirement.Right:
                    resultGroup = right;
                    right.Requirements.Clear();
                    right.Requirements.AddRange(merged);
                    break;
            }

            // remove any always_true conditions. if nothing is left, return Everything so the parent
            // can replace both groups with a single always_true.
            if (everythingCount > 0)
            {
                resultGroup.Requirements.RemoveAll(r => r.Evaluate() == true);
                if (resultGroup.Requirements.Count == 0)
                {
                    var requirementEx = new RequirementEx();
                    requirementEx.Requirements.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                    resultGroup.Requirements.Add(requirementEx);
                    return MoreRestrictiveRequirement.Everything;
                }
            }

            return moreRestrictive;
        }

        public static void MergeRequirementGroups(List<List<RequirementEx>> groups)
        {
            // determine which addresses each group depends on and how many groups depend on each address
            var memoryReferences = new Dictionary<uint, uint>();
            var mergeableGroups = new List<RequirementGroupInfo>();
            foreach (var group in groups.Skip(1)) // ASSERT:groups[0] is core and should be ignored
            {
                var info = new RequirementGroupInfo(group);
                foreach (var requirementEx in group)
                {
                    foreach (var requirement in requirementEx.Requirements)
                    {
                        if (requirement.Left.IsMemoryReference)
                            info.Addresses.Add(requirement.Left.Value);
                        if (requirement.Right.IsMemoryReference)
                            info.Addresses.Add(requirement.Right.Value);
                    }
                }
                foreach (var address in info.Addresses)
                {
                    uint count;
                    memoryReferences.TryGetValue(address, out count);
                    memoryReferences[address] = ++count;
                }
                mergeableGroups.Add(info);
            }

            // groups that only have unique addresses cannot be merged
            for (int i = mergeableGroups.Count - 1; i >= 0; --i)
            {
                var info = mergeableGroups[i];
                if (info.Addresses.Count > 0 && info.Addresses.All(a => memoryReferences[a] == 1))
                    info.IsMergeable = false;
            }

            // if two alt groups are exactly identical, or can otherwise be represented by merging their
            // logic, eliminate the redundant group.
            for (int rightIndex = mergeableGroups.Count - 1; rightIndex > 0; rightIndex--)
            {
                // if all addresses that this group depends on are not used by at least one other group, it can't be merged
                var rightGroup = mergeableGroups[rightIndex];
                if (!rightGroup.IsMergeable)
                    continue;

                for (int leftIndex = rightIndex - 1; leftIndex >= 0; leftIndex--)
                {
                    var leftGroup = mergeableGroups[leftIndex];
                    if (!leftGroup.IsMergeable)
                        continue;

                    var moreRestrictive = MergeRequirementGroups(leftGroup, rightGroup, ConditionalOperation.Or);
                    switch (moreRestrictive)
                    {
                        case MoreRestrictiveRequirement.Same:
                        case MoreRestrictiveRequirement.Left:
                            rightGroup.IsMergeable = false;
                            groups.Remove(rightGroup.Requirements);
                            break;

                        case MoreRestrictiveRequirement.Right:
                            leftGroup.IsMergeable = false;
                            groups.Remove(leftGroup.Requirements);
                            break;

                        case MoreRestrictiveRequirement.Everything:
                            // union of these two groups encompasses all values, replace one with always_true()
                            rightGroup.IsMergeable = false;
                            groups.Remove(rightGroup.Requirements);

                            var index = groups.IndexOf(leftGroup.Requirements);
                            leftGroup.IsMergeable = false;
                            groups.Remove(leftGroup.Requirements);

                            var newGroup = new RequirementEx();
                            newGroup.Requirements.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                            groups.Insert(index, new List<RequirementEx> { newGroup });
                            break;

                        default:
                            break;
                    }

                    if (!rightGroup.IsMergeable)
                        break;
                }
            }

            // if at least two alt groups still exist, check for always_true and always_false placeholders
            if (groups.Count > 2)
                RemoveAlwaysTrueAndAlwaysFalseAlts(groups);
        }

        private static void RemoveAlwaysTrueAndAlwaysFalseAlts(List<List<RequirementEx>> groups)
        {
            bool hasAlwaysTrue = false;

            for (int j = groups.Count - 1; j >= 1; j--)
            {
                if (groups[j].Count == 1)
                {
                    var result = groups[j][0].Evaluate();
                    if (result == false)
                    {
                        if (groups[j][0].Requirements.Any(r => r.Type == RequirementType.Trigger))
                        {
                            // trigger_when(always_false()) can be used in an alt group to display
                            // the challenge icon while a measured value is being incremented in
                            // another alt group.
                            if (groups.Skip(1).Any(g => g.Any(e => e.Requirements.Any(r => r.IsMeasured))))
                                continue;
                        }

                        // an always_false alt group will not affect the logic, remove it
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

            if (hasAlwaysTrue)
            {
                // if a trigger contains an always_true alt group, remove any other alt groups that don't
                // have PauseIf or ResetIf conditions as they are unimportant
                bool alwaysTrueKept = false;
                for (int j = groups.Count - 1; j >= 1; j--)
                {
                    if (groups[j].Count == 1 && groups[j][0].Evaluate() == true)
                    {
                        if (!alwaysTrueKept)
                            alwaysTrueKept = true;
                        else
                            groups.RemoveAt(j);
                        continue;
                    }

                    if (groups[j].All(r => r.Type != RequirementType.ResetIf && r.Type != RequirementType.PauseIf))
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
            else if (groups.Count == 1)
            {
                // if all alt groups were eliminated because they were false, put back a single
                // always_false alt to prevent the achievement from triggering.
                var requirementEx = new RequirementEx();
                requirementEx.Requirements.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
                var group = new List<RequirementEx>();
                group.Add(requirementEx);
                groups.Add(group);
            }
        }
    }
}
