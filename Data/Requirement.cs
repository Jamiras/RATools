using System.Text;

namespace RATools.Data
{
    public class Requirement
    {
        public Field Left { get; set; }
        public Field Right { get; set; }
        public RequirementType Type { get; set; }
        public RequirementOperator Operator { get; set; }
        public ushort HitCount { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();

            if (HitCount == 1)
                builder.Append("once(");
            else if (HitCount > 0)
                builder.AppendFormat("repeated({0}, ", HitCount);

            switch (Type)
            {
                case RequirementType.ResetIf:
                    builder.Append("never(");
                    break;

                case RequirementType.PauseIf:
                    builder.Append("unless(");
                    break;
            }

            builder.Append(Left.ToString());

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
            }

            if (Operator != RequirementOperator.None)
                builder.Append(Right.ToString());

            if (Type != RequirementType.None)
                builder.Append(')');

            if (HitCount != 0)
                builder.Append(')');

            return builder.ToString();
        }

        public override bool Equals(object obj)
        {
            var that = obj as Requirement;
            if (ReferenceEquals(that, null))
                return false;

            if (that.Type != this.Type || that.Operator != this.Operator || that.HitCount != this.HitCount)
                return false;

            return (that.Left == this.Left && that.Right == this.Right);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(Requirement left, Requirement right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(Requirement left, Requirement right)
        {
            if (ReferenceEquals(left, right))
                return false;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return true;

            return !left.Equals(right);
        }
    }

    public enum RequirementOperator
    {
        None = 0,
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    public enum RequirementType
    {
        None = 0,
        ResetIf,
        PauseIf,
    }
}
