using RATools.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RATools.Parser
{
    public class ScriptBuilderContext
    {
        public ScriptBuilderContext() 
        {
            NumberFormat = NumberFormat.Decimal;
            WrapWidth = Int32.MaxValue;
            Indent = 0;
        }

        public NumberFormat NumberFormat { get; set; }

        public int WrapWidth { get; set; }

        public int Indent { get; set; }

        private StringBuilder _addSources;
        private StringBuilder _subSources;
        private StringBuilder _addHits;
        private StringBuilder _andNext;
        private StringBuilder _addAddress;
        private StringBuilder _resetNextIf;
        private StringBuilder _measuredIf;
        private Requirement _lastAndNext;
        private int _remainingWidth;

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("ScriptBuilderContext(");
            builder.Append(NumberFormat.ToString());

            if (WrapWidth != Int32.MaxValue)
                builder.AppendFormat(", {0} cols", WrapWidth);
            if (Indent != 0)
                builder.AppendFormat(", {0} indent", Indent);

            builder.Append(')');
            return builder.ToString();
        }

        internal void Reset()
        {
            _addSources = null;
            _subSources = null;
            _addHits = null;
            _andNext = null;
            _addAddress = null;
            _resetNextIf = null;
            _measuredIf = null;
            _lastAndNext = null;
            _remainingWidth = WrapWidth - Indent;
        }

        private ScriptBuilderContext CreatedNestedContext()
        {
            var context = new ScriptBuilderContext();
            context.NumberFormat = NumberFormat;
            context._addSources = _addSources;
            context._subSources = _subSources;
            context._addHits = _addHits;
            context._andNext = _andNext;
            context._addAddress = _addAddress;
            context._resetNextIf = _resetNextIf;
            context._measuredIf = _measuredIf;
            return context;
        }

        private static bool NullOrEmpty(StringBuilder builder)
        {
            return builder == null || builder.Length == 0;
        }

        public void AppendRequirements(StringBuilder builder, IEnumerable<Requirement> requirements)
        {
            Reset();

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

            if (measuredIfs.Count > 0 && measured != null)
            {
                // if both a Measured and MeasuredIf exist, merge the MeasuredIf into the Measured group so
                // it can be converted to a 'when' parameter of the measured() call
                if (_measuredIf == null)
                    _measuredIf = new StringBuilder();

                foreach (var measuredIf in measuredIfs)
                {
                    groups.Remove(measuredIf);

                    var index = _measuredIf.Length;
                    if (index > 0)
                    {
                        _measuredIf.Append(" && ");
                        index += 4;
                    }

                    AppendRequirementEx(_measuredIf, measuredIf);

                    // remove "measured_if(" and ")" - they're not needed when used as a when clause
                    _measuredIf.Length--;
                    _measuredIf.Remove(index, 12);
                }
            }

            bool needsAmpersand = false;
            foreach (var group in groups)
            {
                if (needsAmpersand)
                {
                    builder.Append(" && ");
                    _remainingWidth -= 4;
                }
                else
                {
                    needsAmpersand = true;
                }

                AppendRequirementEx(builder, group);
            }
        }

        private void AppendRequirementEx(StringBuilder builder, RequirementEx requirementEx)
        {
            // special handling for tally
            if (requirementEx.Requirements.Last().HitCount > 0 &&
                requirementEx.Requirements.Any(r => r.Type == RequirementType.AddHits))
            {
                var nestedContext = new ScriptBuilderContext { NumberFormat = NumberFormat };
                var tallyBuilder = new StringBuilder();
                nestedContext.AppendTally(tallyBuilder, requirementEx.Requirements);
                Append(builder, tallyBuilder);
                return;
            }

            var definition = new StringBuilder();

            foreach (var requirement in requirementEx.Requirements)
            {
                // precedence is AddAddress
                //             > AddSource/SubSource
                //             > AndNext/OrNext
                //             > ResetNextIf
                //             > AddHits/SubHits
                //             > ResetIf/PauseIf/Measured/MeasuredIf/Trigger
                switch (requirement.Type)
                {
                    case RequirementType.AddAddress:
                        if (_addAddress == null)
                            _addAddress = new StringBuilder();
                        AppendField(_addAddress, requirement);
                        _addAddress.Append(" + ");
                        break;

                    case RequirementType.AddSource:
                        if (_addSources == null)
                            _addSources = new StringBuilder();
                        AppendField(_addSources, requirement);
                        _addSources.Append(" + ");
                        break;

                    case RequirementType.SubSource:
                        if (_subSources == null)
                            _subSources = new StringBuilder();
                        _subSources.Append(" - ");
                        AppendField(_subSources, requirement);
                        break;

                    case RequirementType.AndNext:
                    case RequirementType.OrNext:
                        AppendAndOrNext(requirement);
                        break;

                    case RequirementType.AddHits:
                    case RequirementType.SubHits:
                        if (_addHits == null)
                            _addHits = new StringBuilder();
                        AppendModifyHits(_addHits, requirement);
                        _addHits.Append(", ");
                        break;

                    case RequirementType.ResetNextIf:
                        if (_resetNextIf == null)
                            _resetNextIf = new StringBuilder();
                        AppendModifyHits(_resetNextIf, requirement);
                        break;

                    case RequirementType.MeasuredIf:
                        // MeasuredIf should get merged into the when clause of a measured call,
                        // but if we see one, we need to flag it somehow.
                        // measured_if is not a valid function and this will generate an error!
                        if (definition.Length > 0)
                            definition.Append(" && ");

                        definition.Append("measured_if(");
                        AppendRepeatedCondition(definition, requirement);
                        definition.Append(')');
                        break;

                    default:
                        if (definition.Length > 0)
                            definition.Append(" && ");

                        AppendRequirement(definition, requirement);
                        break;
                }
            }

            Append(builder, definition);
        }

        private void Append(StringBuilder builder, StringBuilder source)
        {
            if (source.Length <= _remainingWidth)
            {
                // full string fits on current line
                builder.Append(source);
                _remainingWidth -= source.Length;
                return;
            }

            // keep logical joiners (" && " and " || ") on current line
            if (source.Length > 3 && source[0] == ' ' && source[3] == ' ' && source[1] == source[2])
            {
                builder.Append(source, 0, 3);
                source.Remove(0, 4);
            }

            var availableWidth = WrapWidth - Indent;
            if (source.Length <= availableWidth)
            {
                // full string fits on separate line
                AppendLine(builder);
                builder.Append(source);
                _remainingWidth -= source.Length;
                return;
            }

            // full string does not fit on separate line, try to split it up.
            if (_remainingWidth < availableWidth)
                AppendLine(builder);

            if (source[source.Length - 1] == ')')
            {
                source.Length--; // ignore last paren

                // function call or logical grouping. start on new line, indent intermediate
                // lines, put close paren on last line
                AppendPartialLine(builder, source, FindSplitIndex(source));

                Indent += 4;
                while (source.Length > 0)
                {
                    builder.AppendLine();
                    builder.Append(' ', Indent);
                    AppendPartialLine(builder, source, FindSplitIndex(source));
                }
                Indent -= 4;

                builder.AppendLine();
                builder.Append(' ', Indent);
                builder.Append(')');
                _remainingWidth = WrapWidth - Indent - 1;
                return;
            }

            while (source.Length > availableWidth)
            {
                AppendPartialLine(builder, source, FindSplitIndex(source));
                builder.AppendLine();
                builder.Append(' ', Indent);
            }

            builder.Append(source);
            _remainingWidth = availableWidth - source.Length;
        }

        private void AppendLine(StringBuilder builder)
        {
            if (builder.Length > 0)
            {
                while (builder.Length > 0 && builder[builder.Length - 1] == ' ')
                    builder.Length--;

                builder.AppendLine();
                builder.Append(' ', Indent);
            }

            _remainingWidth = WrapWidth - Indent;
        }

        private int FindSplitIndex(StringBuilder source)
        {
            if (source.Length <= _remainingWidth)
                return source.Length;

            // prefer to split on comma, then on logical separators, then on whitespace

            // first, see if whitespace exists
            var index = _remainingWidth;
            while (index > 0 && source[index] != ' ')
                index--;

            if (index == 0)
            {
                // no whitespace found going backwards, search forwards and split there
                while (index < source.Length && source[index] != ' ')
                    index++;

                return index;
            }

            // look backwards from the whitespace for a comma or logical separator
            var lastSpace = index;
            var lastLogical = Int32.MaxValue;
            while (index > 2)
            {
                var c = source[index];
                if (c == ',')
                    return index + 1;

                if (lastLogical == Int32.MaxValue && (c == '&' || c == '|') && c == source[index - 1])
                    lastLogical = index + 1;

                index--;
            }

            if (lastLogical != Int32.MaxValue)
                return lastLogical;

            if (source.Length - lastSpace < 8)
                return source.Length;

            return lastSpace;
        }

        private static void AppendPartialLine(StringBuilder builder, StringBuilder source, int index)
        {
            builder.Append(source, 0, index);

            while (index < source.Length && source[index] == ' ')
                index++;

            source.Remove(0, index);
        }

        public void AppendRequirement(StringBuilder builder, Requirement requirement)
        {
            switch (requirement.Type)
            {
                case RequirementType.ResetIf:
                    builder.Append("never(");
                    AppendRepeatedCondition(builder, requirement);
                    builder.Append(')');
                    break;

                case RequirementType.PauseIf:
                    if (_resetNextIf != null)
                    {
                        var nestedContext = CreatedNestedContext();
                        nestedContext._resetNextIf = null;

                        var comparison = new StringBuilder();
                        nestedContext.AppendRepeatedCondition(comparison, requirement);
                        if (requirement.HitCount == 1)
                        {
                            comparison.Remove(0, 5); // "once("
                            comparison.Length--;     // ")"
                        }

                        builder.Append("disable_when(");
                        builder.Append(comparison.ToString());
                        builder.Append(", until=");
                        builder.Append(_resetNextIf);
                        builder.Append(')');

                        _resetNextIf.Clear();
                    }
                    else
                    {
                        builder.Append("unless(");
                        AppendRepeatedCondition(builder, requirement);
                        builder.Append(')');
                    }
                    break;

                case RequirementType.Measured:
                case RequirementType.MeasuredPercent:
                    builder.Append("measured(");

                    // if there's no HitTarget and there's an AndNext or OrNext clause, assume we're counting
                    // complex conditions for a Value clause and wrap it in a "tally(0, ...)"
                    if (requirement.HitCount == 0 && !NullOrEmpty(_andNext))
                    {
                        var measuredClause = new StringBuilder();
                        AppendRepeatedCondition(measuredClause, requirement);

                        builder.Append("tally(0, ");
                        builder.Append(RemoveOuterParentheses(measuredClause));
                        builder.Append(')');
                    }
                    else
                    {
                        AppendRepeatedCondition(builder, requirement);
                    }

                    if (_measuredIf != null)
                    {
                        builder.Append(", when=");
                        builder.Append(_measuredIf);
                    }

                    if (requirement.Type == RequirementType.MeasuredPercent)
                        builder.Append(", format=\"percent\"");

                    builder.Append(')');
                    break;

                case RequirementType.Trigger:
                    builder.Append("trigger_when(");
                    AppendRepeatedCondition(builder, requirement);
                    builder.Append(')');
                    break;

                default:
                    AppendRepeatedCondition(builder, requirement);
                    break;
            }
        }

        private void AppendRepeatedCondition(StringBuilder builder, Requirement requirement)
        {
            if (requirement.HitCount == 0 && NullOrEmpty(_addHits))
            {
                bool wrapInParenthesis = false;

                if (!NullOrEmpty(_andNext) && _andNext[0] != '(')
                {
                    var andNext = _andNext.ToString();
                    if (andNext.Contains(" && ") || andNext.Contains(" || "))
                        wrapInParenthesis = true;
                }

                if (wrapInParenthesis)
                    builder.Append('(');

                AppendCondition(builder, requirement);

                if (!NullOrEmpty(_resetNextIf))
                {
                    builder.Append(" && never(");
                    builder.Append(RemoveOuterParentheses(_resetNextIf));
                    builder.Append(')');
                    _resetNextIf.Clear();
                }

                if (wrapInParenthesis)
                    builder.Append(')');
            }
            else
            {
                if (requirement.HitCount == 1)
                    builder.Append("once(");
                else if (!NullOrEmpty(_addHits))
                    builder.AppendFormat("tally({0}, ", requirement.HitCount);
                else
                    builder.AppendFormat("repeated({0}, ", requirement.HitCount);

                bool wrapInParenthesis = false;
                if (!NullOrEmpty(_resetNextIf) && !NullOrEmpty(_andNext))
                {
                    var andNext = _andNext.ToString();
                    if (andNext.LastIndexOf(" || ") > andNext.LastIndexOf(')'))
                        wrapInParenthesis = true;
                }

                if (wrapInParenthesis)
                    builder.Append('(');

                AppendCondition(builder, requirement);

                if (!NullOrEmpty(_resetNextIf))
                {
                    if (wrapInParenthesis)
                        builder.Append(')');

                    builder.Append(" && never(");
                    builder.Append(RemoveOuterParentheses(_resetNextIf));
                    builder.Append(')');
                    _resetNextIf.Clear();
                }

                builder.Append(')');
            }
        }

        private static StringBuilder RemoveOuterParentheses(StringBuilder input)
        {
            while (input.Length > 2 && input[0] == '(' && input[input.Length - 1] == ')')
            {
                input.Length--;
                input.Remove(0, 1);
            }

            return input;
        }

        internal void AppendCondition(StringBuilder builder, Requirement requirement)
        {
            if (!NullOrEmpty(_andNext))
            {
                builder.Append(_andNext);
                _andNext.Clear();
                _lastAndNext = null;
            }

            if (!NullOrEmpty(_addSources))
            {
                builder.Append('(');
                builder.Append(_addSources);
            }
            else if (!NullOrEmpty(_subSources))
            {
                builder.Append('(');
            }
            else if (!NullOrEmpty(_addHits))
            {
                builder.Append(_addHits);
                _addHits.Clear();
            }

            string suffix = null;
            switch (requirement.Type)
            {
                case RequirementType.AddSource:
                    requirement.Left.AppendString(builder, NumberFormat);
                    suffix = " + ";
                    break;

                case RequirementType.SubSource:
                    builder.Append(" - ");
                    requirement.Left.AppendString(builder, NumberFormat);
                    break;

                default:
                    if (requirement.Operator != RequirementOperator.None &&
                        NullOrEmpty(_subSources) && NullOrEmpty(_addSources))
                    {
                        var result = requirement.Evaluate();
                        if (result == null && requirement.HitCount > 0)
                        {
                            // a HitCount may make an always true condition initially false,
                            // so Evaluate() will return null. Try again without the HitCount
                            // as we're just trying to evaluate the logic portion here.
                            var clone = new Requirement
                            {
                                Left = requirement.Left,
                                Operator = requirement.Operator,
                                Right = requirement.Right
                            };
                            result = clone.Evaluate();
                        }

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

                    requirement.Left.AppendString(builder, NumberFormat, _addAddress?.ToString());
                    break;
            }

            // scaling operators need to be appended before chained operations
            AppendFieldModifier(builder, requirement);

            // append chained operations
            if (!NullOrEmpty(_subSources))
            {
                builder.Append(_subSources);
                builder.Append(')');
                _subSources.Clear();
            }
            else if (!NullOrEmpty(_addSources))
            {
                builder.Append(')');
                _addSources.Clear();
            }

            // handle comparison operators
            switch (requirement.Operator)
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
                // handled by AppendFieldModifier above, treat like none

                case RequirementOperator.None:
                    if (suffix != null)
                        builder.Append(suffix);
                    return;
            }

            requirement.Right.AppendString(builder, NumberFormat, _addAddress?.ToString());

            if (suffix != null)
                builder.Append(suffix);

            _addAddress?.Clear();
        }

        private void AppendField(StringBuilder builder, Requirement requirement)
        {
            if (!NullOrEmpty(_addAddress))
            {
                var addAddressString = _addAddress.ToString();
                _addAddress.Clear();

                if (!ReferenceEquals(_addAddress, builder))
                    builder.Append('(');

                requirement.Left.AppendString(builder, NumberFormat, addAddressString);
                AppendFieldModifier(builder, requirement);

                if (!ReferenceEquals(_addAddress, builder))
                    builder.Append(')');
            }
            else
            {
                requirement.Left.AppendString(builder, NumberFormat);
                AppendFieldModifier(builder, requirement);
            }
        }

        private void AppendFieldModifier(StringBuilder builder, Requirement requirement)
        {
            switch (requirement.Operator)
            {
                case RequirementOperator.Multiply:
                    builder.Append(" * ");
                    requirement.Right.AppendString(builder, NumberFormat, _addAddress?.ToString());
                    break;

                case RequirementOperator.Divide:
                    builder.Append(" / ");
                    requirement.Right.AppendString(builder, NumberFormat, _addAddress?.ToString());
                    break;

                case RequirementOperator.BitwiseAnd:
                    builder.Append(" & ");
                    requirement.Right.AppendString(builder, NumberFormat.Hexadecimal, _addAddress?.ToString());
                    break;
            }
        }

        private void AppendAndOrNext(Requirement requirement)
        {
            var nestedContext = CreatedNestedContext();
            nestedContext._addHits = null;
            nestedContext._measuredIf = null;
            nestedContext._resetNextIf = null;

            _andNext = new StringBuilder();

            if (!NullOrEmpty(_addSources) || !NullOrEmpty(_subSources) ||
                !NullOrEmpty(_addAddress))
            {
                _andNext.Append('(');
                nestedContext.AppendRequirement(_andNext, requirement);
                _andNext.Append(')');
            }
            else
            {
                nestedContext.AppendRequirement(_andNext, requirement);
            }

            if (_lastAndNext != null)
            {
                if (_lastAndNext.Type == requirement.Type)
                {
                    RemoveOuterParentheses(_andNext);
                }
                else if (_andNext[0] != '(' || _andNext[_andNext.Length - 1] != ')')
                {
                    _andNext.Insert(0, '(');
                    _andNext.Append(')');
                }
            }

            _lastAndNext = requirement;

            if (requirement.Type == RequirementType.OrNext)
                _andNext.Append(" || ");
            else
                _andNext.Append(" && ");
        }

        private void AppendModifyHits(StringBuilder builder, Requirement requirement)
        {
            var nestedContext = CreatedNestedContext();
            nestedContext._addHits = null;
            nestedContext._measuredIf = null;
            nestedContext._resetNextIf = null;

            nestedContext.AppendRequirement(builder, requirement);

            _lastAndNext = null;
        }

        private void AppendTally(StringBuilder builder, IEnumerable<Requirement> requirements)
        {
            // find the last subclause
            var requirementEx = new RequirementEx();
            foreach (var requirement in requirements)
            {
                if (requirement.Type == RequirementType.AddHits || requirement.Type == RequirementType.SubHits)
                    requirementEx.Requirements.Clear();
                else
                    requirementEx.Requirements.Add(requirement);
            }

            // the final clause will get generated as a "repeated" because we've ignored the AddHits subclauses
            var nestedContext = CreatedNestedContext();
            nestedContext.Reset();
            nestedContext._measuredIf = _measuredIf;
            nestedContext.Indent += 4;

            var repeated = new StringBuilder();
            nestedContext.AppendRequirementEx(repeated, requirementEx);

            var repeatedString = repeated.ToString();
            var repeatedIndex = repeatedString.IndexOf("repeated(");
            var onceIndex = repeatedString.IndexOf("once(");
            var index = 0;
            if (repeatedIndex >= 0 && (onceIndex == -1 || repeatedIndex < onceIndex))
            {
                // replace the "repeated(" with "tally("
                builder.Append(repeatedString, 0, repeatedIndex);
                builder.Append("tally(");

                repeatedIndex += 9;
                while (Char.IsDigit(repeatedString[repeatedIndex]))
                    builder.Append(repeatedString[repeatedIndex++]);
                builder.Append(", ");

                index = repeatedIndex + 2;
            }
            else if (onceIndex >= 0)
            {
                // replace the "once(" with "tally(1, "
                builder.Append(repeatedString, 0, onceIndex);
                builder.Append("tally(1, ");

                index = onceIndex + 5;
            }

            // append the AddHits subclauses
            if (WrapWidth != Int32.MaxValue)
                Indent += 4;

            requirementEx.Requirements.Clear();
            foreach (var requirement in requirements)
            {
                if (requirement.Type == RequirementType.AddHits || requirement.Type == RequirementType.SubHits)
                {
                    // create a copy of the AddHits requirement without the Type
                    requirementEx.Requirements.Add(new Requirement
                    {
                        Left = requirement.Left,
                        Operator = requirement.Operator,
                        Right = requirement.Right,
                        HitCount = requirement.HitCount
                    });

                    if (WrapWidth != Int32.MaxValue)
                    {
                        builder.AppendLine();
                        builder.Append(' ', Indent);
                    }

                    if (requirement.Type == RequirementType.AddHits)
                    {
                        bool hasAlwaysFalse = false;
                        if (requirementEx.Requirements.Count(r => !r.IsScalable) > 1 &&
                            requirementEx.Requirements.All(r => r.HitCount != 0 || r.IsScalable))
                        {
                            requirementEx.Requirements.Last().Type = RequirementType.OrNext;
                            requirementEx.Requirements.Add(Requirement.CreateAlwaysFalseRequirement());
                            hasAlwaysFalse = true;
                        }

                        nestedContext.AppendRequirementEx(builder, requirementEx);

                        if (hasAlwaysFalse)
                            builder.Replace(" || always_false()", "");
                    }
                    else
                    {
                        builder.Append("deduct(");
                        nestedContext.AppendRequirementEx(builder, requirementEx);
                        builder.Append(')');
                    }
                    builder.Append(", ");

                    requirementEx.Requirements.Clear();
                }
                else
                {
                    requirementEx.Requirements.Add(requirement);
                }
            }

            // always_false() final clause separates tally target from individual condition targets
            // it can be safely removed. any other final clause must be preserved
            var remaining = repeatedString.Substring(index);
            if (remaining.StartsWith("always_false()"))
            {
                builder.Length -= 2; // remove ", "
                remaining = remaining.Substring(14);
            }

            if (WrapWidth != Int32.MaxValue)
            {
                builder.AppendLine();
                builder.Append(' ', Indent);
                Indent -= 4;
            }

            builder.Append(remaining);
            _remainingWidth = WrapWidth - Indent - remaining.Length;
        }
    }
}
