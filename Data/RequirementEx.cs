using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Data
{

    public class RequirementEx
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

        public bool? Evaluate()
        {
            if (Requirements.Count == 1)
                return Requirements[0].Evaluate();

            return null;
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

        public static List<RequirementEx> Combine(IEnumerable<Requirement> requirements)
        {
            var group = new List<RequirementEx>();

            bool combiningRequirement = false;
            foreach (var requirement in requirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.AddHits:
                        // an always_false() condition will never generate a hit
                        if (requirement.Evaluate() == false)
                            continue;
                        break;

                    case RequirementType.AndNext:
                        // an always_true() condition will not affect the next condition
                        if (requirement.Evaluate() == true)
                            continue;
                        break;
                }

                if (!combiningRequirement)
                    group.Add(new RequirementEx());

                group.Last().Requirements.Add(requirement);
                combiningRequirement = requirement.IsCombining;
            }

            return group;
        }
    }
}
