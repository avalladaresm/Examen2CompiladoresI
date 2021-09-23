namespace DotNetWeb.Core.Expressions
{
    public abstract class Expression
    {
        public Type type { get; set; }
        public Token Token { get; }

        public Expression(Token token, Type type)
        {
            Token = token;
            this.type = type;
        }

        public abstract string Generate();
    }
}
