namespace RATools.Parser.Expressions
{
    /// <summary>
    /// The supported expression types.
    /// </summary>
    public enum ExpressionType
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        None = 0,

        /// <summary>
        /// A variable definition.
        /// </summary>
        Variable,

        /// <summary>
        /// An integer constant.
        /// </summary>
        IntegerConstant,

        /// <summary>
        /// A string constant.
        /// </summary>
        StringConstant,

        /// <summary>
        /// A boolean constant.
        /// </summary>
        BooleanConstant,

        /// <summary>
        /// A floating point constant.
        /// </summary>
        FloatConstant,

        /// <summary>
        /// A function call.
        /// </summary>
        FunctionCall,

        /// <summary>
        /// A mathematic equation.
        /// </summary>
        Mathematic,

        /// <summary>
        /// A comparison.
        /// </summary>
        Comparison,

        /// <summary>
        /// The conditional
        /// </summary>
        Conditional,

        /// <summary>
        /// An assignment.
        /// </summary>
        Assignment,

        /// <summary>
        /// A function definition.
        /// </summary>
        FunctionDefinition,

        /// <summary>
        /// A return statement.
        /// </summary>
        Return,

        /// <summary>
        /// A dictionary.
        /// </summary>
        Dictionary,

        /// <summary>
        /// An array.
        /// </summary>
        Array,

        /// <summary>
        /// A for loop.
        /// </summary>
        For,

        /// <summary>
        /// An if statement.
        /// </summary>
        If,

        /// <summary>
        /// A comment.
        /// </summary>
        Comment,

        /// <summary>
        /// A keyword.
        /// </summary>
        Keyword,

        /// <summary>
        /// An error.
        /// </summary>
        Error,

        /// <summary>
        /// A reference to a variable.
        /// </summary>
        VariableReference,

        /// <summary>
        /// A memory accessor.
        /// </summary>
        MemoryAccessor,

        /// <summary>
        /// A comparison of MemoryValues with a possible hit target.
        /// </summary>
        Requirement,

        /// <summary>
        /// A rich presence macro parameter.
        /// </summary>
        RichPresenceMacro,
    }

    internal static class ExpressionTypeExtension
    {
        public static string ToLowerString(this ExpressionType expressionType)
        {
            switch (expressionType)
            {
                case ExpressionType.Array: return "array";
                case ExpressionType.Assignment: return "assignment";
                case ExpressionType.BooleanConstant: return "boolean";
                case ExpressionType.Comment: return "comment";
                case ExpressionType.Comparison: return "comparison";
                case ExpressionType.Conditional: return "conditional expression";
                case ExpressionType.Dictionary: return "dictionary";
                case ExpressionType.Error: return "error";
                case ExpressionType.FloatConstant: return "decimal number";
                case ExpressionType.For: return "for expression";
                case ExpressionType.FunctionCall: return "function call";
                case ExpressionType.FunctionDefinition: return "function definition";
                case ExpressionType.If: return "if statement";
                case ExpressionType.IntegerConstant: return "integer";
                case ExpressionType.Keyword: return "keyword";
                case ExpressionType.Mathematic: return "mathematic expression";
                case ExpressionType.MemoryAccessor: return "memory accessor";
                case ExpressionType.Requirement: return "requirement";
                case ExpressionType.Return: return "return statement";
                case ExpressionType.StringConstant: return "string";
                case ExpressionType.Variable: return "variable";
                case ExpressionType.VariableReference: return "variable reference";
                default:
                    return expressionType.ToString().ToLower();
            }
        }

        public static string ToArticleString(this ExpressionType expressionType)
        {
            var lowerString = ToLowerString(expressionType);
            switch (lowerString[0])
            {
                case 'a':
                case 'e':
                case 'i':
                case 'o':
                case 'u':
                    return "an " + lowerString;

                default:
                    return "a " + lowerString;
            }
        }
    }
}
