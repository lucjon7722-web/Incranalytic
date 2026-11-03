#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Incranalytic.Core
{
    // IFCLOUD executes bounded numeric statements, never host-language or operating-system code.
    public sealed class IfcloudProgram
    {
        sealed class Token
        {
            public string Text; public int Line, Column;
            public Token(string text, int line, int column) { Text = text; Line = line; Column = column; }
        }
        sealed class Expr
        {
            public string Op; public Expr A, B; public bool Number, Variable;
            public ScientificNumber Eval(Dictionary<string, ScientificNumber> env)
            {
                if (Number) return ScientificNumber.Parse(Op);
                if (Variable) { ScientificNumber n; if (!env.TryGetValue(Op, out n)) throw new FormatException("Variable '" + Op + "' has no value on this branch."); return n; }
                var a = A.Eval(env);
                bool truth = !a.Coefficient.IsZero;
                if (Op == "!") return Bool(!truth);
                if (Op == "neg") return -a;
                if (Op == "abs") return a.Coefficient.Sign < 0 ? -a : a;
                if (Op == "&&" && !truth) return Bool(false);
                if (Op == "||" && truth) return Bool(true);
                var b = B.Eval(env);
                switch (Op)
                {
                    case "+": return a + b; case "-": return a - b; case "*": return a * b; case "/": return a / b;
                    case "^": return ScientificNumber.Pow(a, b);
                    case ">": return Bool(a.CompareTo(b) > 0); case "<": return Bool(a.CompareTo(b) < 0);
                    case ">=": return Bool(a.CompareTo(b) >= 0); case "<=": return Bool(a.CompareTo(b) <= 0);
                    case "==": return Bool(a.CompareTo(b) == 0); case "!=": return Bool(a.CompareTo(b) != 0);
                    case "&&": return Bool(!b.Coefficient.IsZero); case "||": return Bool(!b.Coefficient.IsZero);
                    case "min": return a.CompareTo(b) <= 0 ? a : b; case "max": return a.CompareTo(b) >= 0 ? a : b;
                }
                throw new FormatException("Unknown operator: " + Op);
            }
            static ScientificNumber Bool(bool b) { return ScientificNumber.Parse(b ? "1" : "0"); }
            public override string ToString() { if (Number || Variable) return Op; if (B == null) return Op + "(" + A + ")"; return "(" + A + " " + Op + " " + B + ")"; }
        }
        sealed class Statement
        {
            public string Name; public Expr Expression; public List<Statement> Yes, No; public int Line;
            public void Run(Dictionary<string, ScientificNumber> env, List<string> trace)
            {
                var n = Expression.Eval(env);
                if (Yes != null)
                {
                    bool taken = !n.Coefficient.IsZero;
                    trace.Add("Line " + Line + ": if " + Expression + " → " + (taken ? "true" : "false"));
                    foreach (var s in taken ? Yes : No) s.Run(env, trace);
                }
                else { env[Name] = n; trace.Add("Line " + Line + ": " + Name + " = " + Expression + " → " + n); }
            }
        }
        readonly List<Token> tokens = new List<Token>();
        readonly List<Statement> statements = new List<Statement>();
        readonly HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> inputs = new List<string>(), outputs = new List<string>();
        int position, nesting, expressionDepth;
        public IReadOnlyList<string> Inputs { get { return inputs; } }
        public IReadOnlyList<string> Outputs { get { return outputs; } }
        Token Current { get { return tokens[position]; } }
        public static IfcloudProgram Parse(string code)
        {
            if (code == null || code.Length > 32000) throw new FormatException("IFCLOUD module exceeds 32,000 characters.");
            var p = new IfcloudProgram(); p.Lex(code);
            while (p.Current.Text == "input" || p.Current.Text == "output")
            {
                bool input = p.Take("input"); if (!input) p.Require("output");
                string name = p.Identifier(); p.Declare(name); p.Require(";"); (input ? p.inputs : p.outputs).Add(name);
            }
            if (p.outputs.Count == 0 || p.outputs.Count > 16 || p.inputs.Count > 16) throw p.Error("Declare 1–16 output ports and at most 16 input ports at the top.");
            while (p.Current.Text != "<end>") p.statements.Add(p.ParseStatement());
            return p;
        }
        public Dictionary<string, ScientificNumber> Execute(IDictionary<string, ScientificNumber> inputValues, List<string> trace)
        {
            var env = new Dictionary<string, ScientificNumber>(StringComparer.Ordinal);
            foreach (var name in inputs)
            {
                ScientificNumber n; if (!inputValues.TryGetValue(name, out n)) throw new FormatException("Connect input '" + name + "'.");
                env.Add(name, n); trace.Add("Input " + name + " = " + n);
            }
            foreach (var statement in statements) statement.Run(env, trace);
            var result = new Dictionary<string, ScientificNumber>(StringComparer.Ordinal);
            foreach (var name in outputs) { ScientificNumber n; if (!env.TryGetValue(name, out n)) throw new FormatException("Output '" + name + "' was not assigned on the executed branch."); result.Add(name, n); }
            return result;
        }
        void Lex(string code)
        {
            int i = 0, line = 1, column = 1;
            while (i < code.Length)
            {
                char c = code[i]; if (char.IsWhiteSpace(c)) { if (c == '\n') { line++; column = 1; } else column++; i++; continue; }
                if (c == '/' && i + 1 < code.Length && code[i + 1] == '/') { while (i < code.Length && code[i] != '\n') { i++; column++; } continue; }
                int start = i, col = column;
                if (char.IsLetter(c) || c == '_') { i++; while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++; }
                else if (char.IsDigit(c) || c == '.' && i + 1 < code.Length && char.IsDigit(code[i + 1]))
                {
                    var m = Regex.Match(code.Substring(i), @"^(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?"); i += m.Length;
                }
                else if (i + 1 < code.Length && new[] { ">=", "<=", "==", "!=", "&&", "||" }.Contains(code.Substring(i, 2))) i += 2;
                else if ("+-*/^><!=;(){},".Contains(c)) i++;
                else throw new FormatException("IFCLOUD line " + line + ", column " + column + ": unsupported character '" + c + "'.");
                tokens.Add(new Token(code.Substring(start, i - start), line, col)); column += i - start;
                if (tokens.Count > 4096) throw new FormatException("IFCLOUD module exceeds 4,096 tokens.");
            }
            tokens.Add(new Token("<end>", line, column));
        }
        Statement ParseStatement()
        {
            int line = Current.Line;
            if (Take("if"))
            {
                Require("("); var condition = ParseExpr(0); Require(")"); var yes = Block();
                var no = new List<Statement>(); if (Take("else")) { if (Current.Text == "if") no.Add(ParseStatement()); else no = Block(); }
                return new Statement { Expression = condition, Yes = yes, No = no, Line = line };
            }
            bool local = Take("let"); string name = Identifier();
            if (local) Declare(name); else if (!declared.Contains(name) || inputs.Contains(name)) throw Error("Assign to a declared output or let variable; input ports are read-only.");
            Require("="); var expr = ParseExpr(0); Require(";"); return new Statement { Name = name, Expression = expr, Line = line };
        }
        List<Statement> Block()
        {
            if (++nesting > 32) throw Error("Block nesting exceeds 32.");
            Require("{"); var block = new List<Statement>(); while (Current.Text != "}" && Current.Text != "<end>") block.Add(ParseStatement()); Require("}"); nesting--; return block;
        }
        Expr ParseExpr(int min)
        {
            if (++expressionDepth > 64) throw Error("Expression nesting exceeds 64.");
            Expr left;
            if (Take("-")) left = new Expr { Op = "neg", A = ParseExpr(7) };
            else if (Take("!")) left = new Expr { Op = "!", A = ParseExpr(7) };
            else if (Take("+")) left = ParseExpr(7);
            else if (Take("(")) { left = ParseExpr(0); Require(")"); }
            else if (Current.Text == "true" || Current.Text == "false") { left = new Expr { Number = true, Op = Current.Text == "true" ? "1" : "0" }; position++; }
            else if (char.IsDigit(Current.Text[0]) || Current.Text[0] == '.') { string n = Current.Text; position++; left = new Expr { Number = true, Op = n.StartsWith(".") ? "0" + n : n }; ScientificNumber.Parse(left.Op); }
            else
            {
                string name = Identifier();
                if (Take("("))
                {
                    if (name != "abs" && name != "min" && name != "max") throw Error("Functions: abs(x), min(a,b), max(a,b).");
                    var a = ParseExpr(0); Expr b = null; if (name != "abs") { Require(","); b = ParseExpr(0); } Require(")"); left = new Expr { Op = name, A = a, B = b };
                }
                else { if (!declared.Contains(name)) throw Error("Declare '" + name + "' as an input, output, or let variable before use."); left = new Expr { Op = name, Variable = true }; }
            }
            while (true)
            {
                string op = Current.Text; int rank = op == "||" ? 1 : op == "&&" ? 2 : op == "==" || op == "!=" ? 3 : op == ">" || op == "<" || op == ">=" || op == "<=" ? 4 : op == "+" || op == "-" ? 5 : op == "*" || op == "/" ? 6 : op == "^" ? 7 : -1;
                if (rank < min) break; position++; left = new Expr { Op = op, A = left, B = ParseExpr(op == "^" ? rank : rank + 1) };
            }
            expressionDepth--; return left;
        }
        bool Take(string s) { if (Current.Text != s) return false; position++; return true; }
        void Require(string s) { if (!Take(s)) throw Error("Expected '" + s + "'."); }
        string Identifier()
        {
            string s = Current.Text;
            if (s.Length > 64 || !Regex.IsMatch(s, "^[A-Za-z_][A-Za-z0-9_]*$") || new[] { "input", "output", "let", "if", "else", "true", "false", "while", "for", "return" }.Contains(s)) throw Error("Expected a symbol name (letters, digits, underscore).");
            position++; return s;
        }
        void Declare(string s) { if (!declared.Add(s)) throw Error("Duplicate symbol: " + s); }
        FormatException Error(string message) { return new FormatException("IFCLOUD line " + Current.Line + ", column " + Current.Column + ": " + message); }
    }
}
