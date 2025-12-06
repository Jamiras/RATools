using RATools.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    public class ScriptBuilderContext
    {
        public ScriptBuilderContext() 
        {
            NumberFormat = NumberFormat.Decimal;
            AddressWidth = 6;
            WrapWidth = Int32.MaxValue;
            Indent = 0;
            _aliases = new List<MemoryAccessorAlias>();
        }

        public NumberFormat NumberFormat { get; set; }

        /// <summary>
        /// Gets or sets the number of characters to use for addresses.
        /// </summary>
        public int AddressWidth { get; set; }

        public int WrapWidth { get; set; }

        public int Indent { get; set; }

        public bool IsValue { get; set; }

        public ScriptBuilderContext Clone()
        {
            return new ScriptBuilderContext()
            {
                NumberFormat = NumberFormat,
                Indent = Indent,
                WrapWidth = WrapWidth,
                IsValue = IsValue,
                _aliases = _aliases,
            };
        }

        private StringBuilder _addSources;
        private StringBuilder _subSources;
        private StringBuilder _addHits;
        private StringBuilder _andNext;
        private StringBuilder _resetNextIf;
        private StringBuilder _measuredIf;
        private StringBuilder _remember;
        private Requirement _lastAndNext;
        private int _remainingWidth;

        class MemoryAccessorAliasChain
        {
            public MemoryAccessorAlias Alias { get; set; }
            public MemoryAccessorAliasChain Next { get; set; }
            public Requirement Requirement { get; set; }
        };
        private MemoryAccessorAliasChain _addAddress;

        private List<MemoryAccessorAlias> _aliases;

        public void AddAlias(MemoryAccessorAlias alias)
        {
            _aliases.Add(alias);
        }

        public void AddAliases(IEnumerable<MemoryAccessorAlias> aliases)
        {
            _aliases.AddRange(aliases);
        }

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

        private ScriptBuilderContext CreateNestedContext()
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
            context._aliases = _aliases;
            context._remember = _remember;
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
            var pauseRemembers = new List<RequirementEx>();
            foreach (var group in groups)
            {
                if (group.IsMeasured)
                    measured = group;
                else if (group.Type == RequirementType.MeasuredIf)
                    measuredIfs.Add(group);
                else if (group.Type == RequirementType.PauseIf && group.Requirements.Any(r => r.Type == RequirementType.Remember))
                    pauseRemembers.Add(group);
            }

            if (pauseRemembers.Count > 0)
            {
                bool hasNonPauseRecallBeforeLastPauseRemember = false;
                int lastPauseRememberIndex = groups.IndexOf(pauseRemembers.Last());
                for (int index = 0; index < lastPauseRememberIndex; index++)
                {
                    var group = groups[index];
                    if (group.Type == RequirementType.PauseIf)
                        continue;

                    // found a non-pause Remember, don't need the pause Remember
                    if (group.Requirements.Any(r => r.Type == RequirementType.Remember))
                        break;

                    if (group.Requirements.Any(r => r.Left.Type == FieldType.Recall || r.Right.Type == FieldType.Recall))
                    {
                        // found a Recall without a Remember, it will use the last non-pause Remember
                        hasNonPauseRecallBeforeLastPauseRemember = true;
                        break;
                    }
                }

                if (hasNonPauseRecallBeforeLastPauseRemember)
                {
                    // there's at least one Recall dependant on a Remember from the Pause chain.
                    // move the pause chain to the front of the list.
                    foreach (var group in pauseRemembers)
                        groups.Remove(group);
                    groups.InsertRange(0, pauseRemembers);
                }
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
                var nestedContext = CreateNestedContext();
                nestedContext._addAddress = null;
                nestedContext.WrapWidth = Int32.MaxValue;
                var tallyBuilder = new StringBuilder();
                nestedContext.AppendTally(tallyBuilder, requirementEx.Requirements);
                Append(builder, tallyBuilder);
                _measuredIf = null;
                return;
            }

            var definition = new StringBuilder();

            foreach (var requirement in requirementEx.Requirements)
            {
                // precedence is AddAddress
                //             > AddSource/SubSource
                //             > Remember
                //             > AndNext/OrNext
                //             > ResetNextIf
                //             > AddHits/SubHits
                //             > ResetIf/PauseIf/Measured/MeasuredIf/Trigger
                switch (requirement.Type)
                {
                    case RequirementType.AddAddress:
                        if (requirement.Left.Type == FieldType.Recall)
                        {
                            _addAddress = new MemoryAccessorAliasChain()
                            {
                                Alias = new MemoryAccessorAlias(0),
                                Next = null,
                                Requirement = requirement,
                            };
                            continue;
                        }
                        else if (requirement.Left.IsMemoryReference)
                        {
                            var currentAliases = (_addAddress != null) ? _addAddress.Alias.Children : _aliases;
                            var memoryAccessor = currentAliases.FirstOrDefault(a => a.Address == requirement.Left.Value);
                            if (memoryAccessor == null)
                                memoryAccessor = new MemoryAccessorAlias(requirement.Left.Value);
                            _addAddress = new MemoryAccessorAliasChain()
                            {
                                Alias = memoryAccessor,
                                Next = _addAddress,
                                Requirement = requirement,
                            };
                            continue;
                        }
                        break;

                    case RequirementType.AddSource:
                        if (_addSources == null)
                            _addSources = new StringBuilder();
                        AppendFields(_addSources, requirement);
                        _addSources.Append(" + ");
                        break;

                    case RequirementType.SubSource:
                        if (_subSources == null)
                            _subSources = new StringBuilder();
                        _subSources.Append(" - ");
                        AppendFields(_subSources, requirement);
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

                    case RequirementType.Remember:
                        if (_addSources == null)
                            _addSources = new StringBuilder();
                        AppendFields(_addSources, requirement);
                        _remember = _addSources;
                        _addSources = null;
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
            if (source.Length < WrapWidth)
            {
                // full string barely doesn't fit on separate line
                AppendLine(builder);
                builder.Append(source);
                _remainingWidth = 0;
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

                // if the last parenthesis matches something on this line, keep with with this line
                var count = 0;
                var index = builder.Length;
                while (index > 0 && builder[index - 1] != '\n')
                {
                    --index;
                    if (builder[index] == ')')
                        count++;
                    else if (builder[index] == '(')
                        count--;
                }

                if (count >= 0)
                {
                    // all parens on this line are matched, put the extra paren on a separate line.
                    builder.AppendLine();
                    builder.Append(' ', Indent);
                    builder.Append(')');
                    _remainingWidth = WrapWidth - Indent - 1;
                }
                else
                {
                    // extra paren closes something on this line, keep it on this line.
                    builder.Append(')');
                    _remainingWidth = WrapWidth - (builder.Length - index);
                }

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
                    if (_resetNextIf != null || requirement.HitCount != 0)
                    {
                        var nestedContext = CreateNestedContext();
                        nestedContext._resetNextIf = null;

                        var comparison = new StringBuilder();
                        nestedContext.AppendRepeatedCondition(comparison, requirement);
                        if (requirement.HitCount == 1)
                        {
                            comparison.Remove(0, 5); // "once("
                            comparison.Length--;     // ")"
                        }

                        builder.Append("disable_when(");
                        builder.Append(comparison);

                        if (_resetNextIf != null)
                        {
                            builder.Append(", until=");
                            builder.Append(_resetNextIf);

                            _resetNextIf.Clear();
                        }

                        builder.Append(')');
                        _addAddress = null;
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

                    // if there's no HitTarget and we're in a Value clause, wrap it in a "tally(0, ...)"
                    // note: if we're already in an AddHits chain, there will be an implicity tally
                    if (requirement.HitCount == 0 && IsValue && NullOrEmpty(_addHits) &&
                        requirement.Operator.IsComparison())
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
                if (!NullOrEmpty(_resetNextIf))
                {
                    bool wrapInParenthesis = _lastAndNext?.Type == RequirementType.OrNext;
                    if (wrapInParenthesis)
                        builder.Append('(');

                    AppendCondition(builder, requirement);

                    if (wrapInParenthesis)
                        builder.Append(')');

                    builder.Append(" && never(");
                    builder.Append(RemoveOuterParentheses(_resetNextIf));
                    builder.Append(')');
                    _resetNextIf.Clear();
                }
                else
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

                    if (wrapInParenthesis)
                        builder.Append(')');
                }
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
                if (!NullOrEmpty(_resetNextIf))
                    wrapInParenthesis = _lastAndNext?.Type == RequirementType.OrNext;

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
            if (!NullOrEmpty(_addHits))
            {
                builder.Append(_addHits);
                _addHits.Clear();
            }

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
                        NullOrEmpty(_subSources) && NullOrEmpty(_addSources) && _addAddress == null)
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

                    AppendField(builder, requirement.Left);
                    break;
            }

            // scaling operators need to be appended before chained operations
            AppendFieldModifier(builder, requirement);

            // append chained operations
            if (!NullOrEmpty(_addSources))
            {
                // remove trailing " + 0"
                if (builder.Length > 4 &&
                    builder[builder.Length - 1] == '0' &&
                    builder[builder.Length - 2] == ' ' &&
                    builder[builder.Length - 3] == '+' &&
                    builder[builder.Length - 4] == ' ')
                {
                    // special case - ignore awkward construction used for building chained RequirementViewModel
                    if (_addSources.Length != 7 || _addSources.ToString() != "none + ")
                        builder.Length -= 4;
                }

                if (NullOrEmpty(_subSources))
                    builder.Append(')');
                _addSources.Clear();
            }
            if (!NullOrEmpty(_subSources))
            {
                builder.Append(_subSources);
                builder.Append(')');
                _subSources.Clear();
            }

            // handle comparison operators
            if (requirement.Operator.IsComparison())
            {
                builder.Append(' ');
                builder.Append(requirement.Operator.ToOperatorString());
                builder.Append(' ');

                AppendField(builder, requirement.Right);
            }

            if (suffix != null)
                builder.Append(suffix);

            _addAddress = null;
        }

        private void AppendFields(StringBuilder builder, Requirement requirement)
        {
            AppendField(builder, requirement.Left);
            AppendFieldModifier(builder, requirement);
            _addAddress = null;
        }

        private void AppendField(StringBuilder builder, Field field)
        {
            if (field.Type == FieldType.Recall && _remember != null && _remember.Length > 0)
            {
                builder.Append('(');
                builder.Append(_remember);
                builder.Append(')');
            }
            else if (field.IsMemoryReference)
            {
                var currentAliases = (_addAddress != null) ? _addAddress.Alias.Children : _aliases;
                var memoryAccessor = currentAliases.FirstOrDefault(a => a.Address == field.Value);
                AppendFieldAlias(builder, field, memoryAccessor, _addAddress, null);
            }
            else
            {
                field.AppendString(builder, NumberFormat);
            }
        }

        private void AppendFieldAlias(StringBuilder builder, Field field, MemoryAccessorAlias alias, MemoryAccessorAliasChain parent, Requirement parentRequirement)
        {
            bool needClosingParenthesis = false;
            switch (field.Type)
            {
                case FieldType.PreviousValue:
                    builder.Append("prev(");
                    needClosingParenthesis = true;
                    break;
                case FieldType.PriorValue:
                    builder.Append("prior(");
                    needClosingParenthesis = true;
                    break;
                case FieldType.BinaryCodedDecimal:
                    builder.Append("bcd(");
                    needClosingParenthesis = true;
                    break;
                case FieldType.Invert:
                    builder.Append('~');
                    break;
                case FieldType.Recall:
                    builder.Append(_remember);
                    return;
            }

            var functionName = alias?.GetAlias(field.Size);
            if (!String.IsNullOrEmpty(functionName))
            {
                builder.Append(functionName);
                builder.Append("()");
            }
            else
            {
                builder.Append(Field.GetSizeFunction(field.Size));
                builder.Append('(');

                if (parent != null)
                {
                    AppendFieldAlias(builder, parent.Requirement.Left, parent.Alias, parent.Next, parent.Requirement);
                    if (field.Value != 0)
                        builder.AppendFormat(" + 0x{0:X2}", field.Value);
                    builder.Append(')');
                }
                else
                {
                    builder.Append("0x");
                    builder.Append(FormatAddress(field.Value));
                    builder.Append(')');
                }
            }

            if (parentRequirement != null)
                AppendFieldModifier(builder, parentRequirement);

            if (needClosingParenthesis)
                builder.Append(')');
        }

        public string FormatAddress(UInt32 address)
        {
            switch (AddressWidth)
            {
                case 2:
                    return String.Format("{0:X2}", address);
                case 4:
                    return String.Format("{0:X4}", address);
                default:
                    return String.Format("{0:X6}", address);
            }

        }

        private void AppendFieldModifier(StringBuilder builder, Requirement requirement)
        {
            if (requirement.Operator.IsModifier())
            {
                builder.Append(' ');
                builder.Append(requirement.Operator.ToOperatorString());
                builder.Append(' ');

                if (requirement.Right.Type == FieldType.Value)
                {
                    switch (requirement.Operator)
                    {
                        case RequirementOperator.BitwiseAnd:
                        case RequirementOperator.BitwiseXor:
                            // force right side to be hexadecimal for bitwise operators
                            requirement.Right.AppendString(builder, NumberFormat.Hexadecimal);
                            return;

                        default:
                            // force right side to decimal for single-digit decimal values
                            if (requirement.Right.Type == FieldType.Value && requirement.Right.Value < 10)
                            {
                                requirement.Right.AppendString(builder, NumberFormat.Decimal);
                                return;
                            }
                            break;
                    }
                }

                AppendField(builder, requirement.Right);
            }
        }

        private void AppendAndOrNext(Requirement requirement)
        {
            var nestedContext = CreateNestedContext();
            nestedContext._addHits = null;
            nestedContext._measuredIf = null;
            nestedContext._resetNextIf = null;

            _andNext = new StringBuilder();

            if (!NullOrEmpty(_addSources) || !NullOrEmpty(_subSources) ||
                _addAddress != null)
            {
                _andNext.Append('(');
                nestedContext.AppendRequirement(_andNext, requirement);
                _andNext.Append(')');
                _addAddress = null;
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
            var nestedContext = CreateNestedContext();
            nestedContext._addHits = null;
            nestedContext._measuredIf = null;
            nestedContext._resetNextIf = null;

            var nestedBuilder = new StringBuilder();
            nestedContext.AppendRequirement(nestedBuilder, requirement);
            RemoveOuterParentheses(nestedBuilder);
            builder.Append(nestedBuilder);

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
            var nestedContext = CreateNestedContext();
            nestedContext.Reset();
            nestedContext._measuredIf = _measuredIf;
            nestedContext.Indent += 4;

            var repeated = new StringBuilder();
            nestedContext.AppendRequirementEx(repeated, requirementEx);

            string suffix = "";
            var repeatedString = repeated.ToString();
            var index = repeated.Length;
            var repeatedIndex = repeatedString.IndexOf("repeated(");
            if (repeatedIndex != -1)
                index = repeatedIndex;
            var onceIndex = repeatedString.IndexOf("once(", 0, index);
            if (onceIndex != -1)
                index = onceIndex;
            var disableWhenIndex = repeatedString.IndexOf("disable_when(", 0, index);
            if (disableWhenIndex != -1)
                index = disableWhenIndex;

            if (index == repeatedIndex)
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
            else if (index == onceIndex)
            {
                // replace the "once(" with "tally(1, "
                builder.Append(repeatedString, 0, onceIndex);
                builder.Append("tally(1, ");

                index = onceIndex + 5;
            }
            else if (index == disableWhenIndex)
            {
                builder.Append(repeatedString, 0, disableWhenIndex);
                index = disableWhenIndex + 13;

                if (index == repeatedIndex)
                {
                    // replace the "disable_when(repeated(N, " with "disable_when(tally(N, "
                    builder.Append("disable_when(tally(");
                    index += 9;
                    var comma = repeatedString.IndexOf(',', index);
                    builder.Append(repeatedString, index, comma - index);
                    index = comma + 2; // assume ", "
                    builder.Append(", ");
                }
                else
                {
                    // replace the "disable_when(" with "unless(tally(1, "
                    builder.Append("disable_when(tally(1, ");
                    suffix = ")";
                }
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
                        if (requirementEx.Requirements.Count(r => !r.Type.IsScalable()) > 1 &&
                            requirementEx.Requirements.All(r => r.HitCount != 0 || r.Type.IsScalable()))
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
                Indent -= 4;
                builder.AppendLine();
                builder.Append(' ', Indent);
            }

            builder.Append(remaining);
            builder.Append(suffix);

            _remainingWidth = WrapWidth - Indent - remaining.Length;
        }
    }
}
