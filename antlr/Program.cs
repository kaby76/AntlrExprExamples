using System.IO;
using Antlr4.Runtime;

namespace antlr
{
    public class Program
    {
        static void Main(string[] args)
        {
            var file_name = args[0];
            var input = File.OpenText(file_name);
            var str = new AntlrInputStream(input);
            var lexer = new ANTLRv4Lexer(str);
            var tokens = new CommonTokenStream(lexer);
            var parser = new ANTLRv4Parser(tokens);
            var listener = new ErrorListener<IToken>(parser, lexer, tokens);
            parser.AddErrorListener(listener);
            var tree = parser.grammarSpec();
            if (listener.had_error)
            {
                System.Console.WriteLine("error in parse.");
            }
            else
            {
                System.Console.WriteLine("parse completed.");
                System.Console.WriteLine(tokens.OutputTokens());
                System.Console.WriteLine(tree.OutputTree(tokens));
            }
        }
}
}
