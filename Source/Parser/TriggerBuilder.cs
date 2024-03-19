using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    [DebuggerDisplay("Core:{_core.Count} Alts:{_alts.Count}")]
    public class TriggerBuilder
    {
        public TriggerBuilder()
        {
            _core = new List<Requirement>();
            _alts = new List<ICollection<Requirement>>();
        }

        public TriggerBuilder(IEnumerable<Requirement> requirements)
            : this()
        {
            _core.AddRange(requirements);
        }

        public TriggerBuilder(IEnumerable<Requirement> coreRequirements, IEnumerable<IEnumerable<Requirement>> alternateRequirements)
            : this()
        {
            _core.AddRange(coreRequirements);

            foreach (var alt in alternateRequirements)
                _alts.Add(new List<Requirement>(alt));
        }

        public TriggerBuilder(Trigger source)
            : this()
        {
            _core.AddRange(source.Core.Requirements);

            foreach (var alt in source.Alts)
                _alts.Add(new List<Requirement>(alt.Requirements));
        }

        /// <summary>
        /// Gets the core reuirements group.
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
        private readonly List<ICollection<Requirement>> _alts;

        internal bool IsDumped { get; set; }

        internal static Trigger BuildTrigger(ITriggerExpression triggerExpression, out ErrorExpression error)
        {
            // TODO: modify TriggerBuilderContext to support Alts
            var builder = new AchievementBuilder();
            var context = new AchievementBuilderContext(builder);

            error = triggerExpression.BuildTrigger(context);
            if (error != null)
                return null;

            builder.Optimize();
            return builder.ToTrigger();
        }

        /// <summary>
        /// Constructs a <see cref="Trigger"/> from the current state of the builder.
        /// </summary>
        public Trigger ToTrigger()
        {
            var alts = new Requirement[_alts.Count][];
            for (int i = 0; i < _alts.Count; i++)
                alts[i] = _alts[i].ToArray();

            return new Trigger(_core.ToArray(), alts);
        }

        /// <summary>
        /// Populates the core and alt requirements from a serialized requirement string.
        /// </summary>
        public void ParseRequirements(Tokenizer tokenizer)
        {
            var trigger = Trigger.Deserialize(tokenizer);
            _core.AddRange(trigger.Core.Requirements);

            foreach (var alt in trigger.Alts)
                _alts.Add(new List<Requirement>(alt.Requirements));
        }

        /// <summary>
        /// Creates a serialized requirements string from the core and alt groups.
        /// </summary>
        public string SerializeRequirements(SerializationContext serializationContext)
        {
            var trigger = new Trigger(_core, _alts);
            return trigger.Serialize(serializationContext);
        }

        internal string RequirementsDebugString
        {
            get { return ToScript(new ScriptBuilderContext()); }
        }

        /// <summary>
        /// Gets the requirements formatted as a human-readable string.
        /// </summary>
        public string ToScript(ScriptBuilderContext context)
        {
            var builder = new StringBuilder();
            context.AppendRequirements(builder, CoreRequirements);

            if (AlternateRequirements.Count > 0)
            {
                if (CoreRequirements.Count > 0)
                    builder.Append(" && (");

                foreach (var altGroup in AlternateRequirements)
                {
                    if (altGroup.Count > 1)
                        builder.Append('(');

                    context.AppendRequirements(builder, altGroup);

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

        public bool AreRequirementsSame(TriggerBuilder right)
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

            // optimize
            var error = RequirementsOptimizer.Optimize(groups, forSubclause);
            if (error != null)
                return error;

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

            // success!
            return null;
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
                        int j = RequirementsOptimizer.FindResetNextIfStart(_core, i);

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

            // merge the alts into the core group as an OrNext chain. only one AndNext chain can be generated
            // by the alt groups or the logic cannot be represented using only AndNext and OrNext conditions
            ICollection<Requirement> andNextAlt = null;
            foreach (var alt in _alts)
            {
                if (alt.Last().Type != RequirementType.None)
                    return new ErrorExpression(BehavioralRequirementExpression.GetFunctionName(alt.Last().Type) + " not allowed in subclause");

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