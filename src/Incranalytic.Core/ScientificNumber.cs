#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Incranalytic.Core
{
    // Decimal scientific arithmetic. Precision truncates toward zero after each operation.
    public sealed class ScientificNumber
    {
        public static int Precision = 34;
        public readonly BigInteger Coefficient;
        public readonly BigInteger Scale;
        public ScientificNumber(BigInteger coefficient, BigInteger scale)
        {
            if (Precision < 1 || Precision > 1000) throw new ArgumentOutOfRangeException("Precision", "Choose 1–1000 significant digits.");
            if (coefficient.IsZero) { Coefficient = BigInteger.Zero; Scale = BigInteger.Zero; return; }
            int excess = BigInteger.Abs(coefficient).ToString().Length - Precision;
            if (excess > 0) { coefficient /= BigInteger.Pow(10, excess); scale += excess; }
            while (coefficient % 10 == 0) { coefficient /= 10; scale++; }
            Coefficient = coefficient; Scale = scale;
        }
        public static ScientificNumber Parse(string text)
        {
            if (text == null || text.Length > 5000) throw new FormatException("Number is too long; use scientific notation.");
            Match m = Regex.Match(text.Trim(), @"^([+-]?)(\d+)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (!m.Success) throw new FormatException("Use a decimal or scientific number, for example 1.25e1000.");
            BigInteger c = BigInteger.Parse(m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value, CultureInfo.InvariantCulture);
            BigInteger e = m.Groups[4].Success ? BigInteger.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : BigInteger.Zero;
            return new ScientificNumber(c, e - m.Groups[3].Value.Length);
        }
        public static ScientificNumber Zero { get { return new ScientificNumber(0, 0); } }
        public static ScientificNumber operator +(ScientificNumber a, ScientificNumber b)
        {
            if (a.Coefficient.IsZero) return b; if (b.Coefficient.IsZero) return a;
            BigInteger diff = a.Scale - b.Scale;
            if (diff > Precision + 2) return a;
            if (diff < -Precision - 2) return b;
            if (diff >= 0) return new ScientificNumber(a.Coefficient * BigInteger.Pow(10, (int)diff) + b.Coefficient, b.Scale);
            return new ScientificNumber(a.Coefficient + b.Coefficient * BigInteger.Pow(10, (int)-diff), a.Scale);
        }
        public static ScientificNumber operator -(ScientificNumber a) { return new ScientificNumber(-a.Coefficient, a.Scale); }
        public static ScientificNumber operator -(ScientificNumber a, ScientificNumber b) { return a + -b; }
        public static ScientificNumber operator *(ScientificNumber a, ScientificNumber b) { return new ScientificNumber(a.Coefficient * b.Coefficient, a.Scale + b.Scale); }
        public static ScientificNumber operator /(ScientificNumber a, ScientificNumber b)
        {
            if (b.Coefficient.IsZero) throw new ArithmeticException("Division by zero.");
            int padding = Precision + BigInteger.Abs(b.Coefficient).ToString().Length + 2;
            return new ScientificNumber(a.Coefficient * BigInteger.Pow(10, padding) / b.Coefficient, a.Scale - b.Scale - padding);
        }
        public static ScientificNumber Pow(ScientificNumber a, ScientificNumber exponent)
        {
            if (exponent.Scale < 0 || exponent.Scale > 6) throw new ArithmeticException("Powers currently require an integer exponent between -100000 and 100000.");
            BigInteger n = exponent.Coefficient * BigInteger.Pow(10, (int)exponent.Scale);
            if (BigInteger.Abs(n) > 100000) throw new ArithmeticException("Power exponent exceeds the 100000 calculation limit. Enter a large number directly using e notation.");
            bool negative = n.Sign < 0; int power = (int)BigInteger.Abs(n);
            ScientificNumber result = Parse("1");
            while (power > 0) { if ((power & 1) != 0) result = result * a; power >>= 1; if (power > 0) a = a * a; }
            return negative ? Parse("1") / result : result;
        }
        public int CompareTo(ScientificNumber other) { return (this - other).Coefficient.Sign; }
        public double PlotLog()
        {
            if (Coefficient.Sign <= 0) return double.NaN;
            double scale = (double)Scale;
            if (Math.Abs(scale) > 1000000) return double.NaN;
            return BigInteger.Log10(Coefficient) + scale;
        }
        public override string ToString()
        {
            if (Coefficient.IsZero) return "0e0";
            string digits = BigInteger.Abs(Coefficient).ToString(CultureInfo.InvariantCulture);
            return (Coefficient.Sign < 0 ? "-" : "") + digits.Substring(0, 1) + (digits.Length > 1 ? "." + digits.Substring(1) : "") + "e" + (Scale + digits.Length - 1).ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class ScientificExpression
    {
        readonly string text; readonly IDictionary<string, ScientificNumber> values; int pos, depth;
        ScientificExpression(string text, IDictionary<string, ScientificNumber> values) { this.text = text; this.values = values; }
        public static ScientificNumber Evaluate(string text, IDictionary<string, ScientificNumber> values)
        {
            if (text.Length > 2000) throw new FormatException("Formula exceeds 2000 characters.");
            var p = new ScientificExpression(text, values); var result = p.Expr(0); p.Space();
            if (p.pos != text.Length) throw new FormatException("Unexpected character at position " + (p.pos + 1));
            return result;
        }
        void Space() { while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++; }
        ScientificNumber Expr(int minimum)
        {
            if (++depth > 64) throw new FormatException("Formula nesting exceeds 64.");
            var left = Primary();
            while (true)
            {
                Space(); char op = pos < text.Length ? text[pos] : '\0';
                int rank = op == '+' || op == '-' ? 1 : op == '*' || op == '/' ? 2 : op == '^' ? 3 : -1;
                if (rank < minimum) break;
                pos++; var right = Expr(op == '^' ? rank : rank + 1);
                switch (op) { case '+': left = left + right; break; case '-': left = left - right; break; case '*': left = left * right; break; case '/': left = left / right; break; case '^': left = ScientificNumber.Pow(left, right); break; }
            }
            depth--; return left;
        }
        ScientificNumber Primary()
        {
            Space(); if (pos >= text.Length) throw new FormatException("Expected a number, symbol, or parenthesis.");
            char c = text[pos];
            if (c == '+' || c == '-') { pos++; var n = Expr(3); return c == '-' ? -n : n; }
            if (c == '(') { pos++; var n = Expr(0); Require(')'); return n; }
            if (char.IsDigit(c))
            {
                var m = Regex.Match(text.Substring(pos), @"^\d+(?:\.\d*)?(?:[eE][+-]?\d+)?"); pos += m.Length; return ScientificNumber.Parse(m.Value);
            }
            if (char.IsLetter(c) || c == '_')
            {
                int start = pos++; while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_')) pos++;
                string name = text.Substring(start, pos - start); Space();
                if (pos < text.Length && text[pos] == '(')
                {
                    pos++; var a = Expr(0); Space(); ScientificNumber b = null;
                    if (pos < text.Length && text[pos] == ',') { pos++; b = Expr(0); } Require(')');
                    if (name == "abs" && b == null) return a.Coefficient.Sign < 0 ? -a : a;
                    if (name == "min" && b != null) return a.CompareTo(b) <= 0 ? a : b;
                    if (name == "max" && b != null) return a.CompareTo(b) >= 0 ? a : b;
                    throw new FormatException("Supported functions: abs(x), min(a,b), max(a,b).");
                }
                ScientificNumber value; if (!values.TryGetValue(name, out value)) throw new FormatException("Define symbol '" + name + "' in Values."); return value;
            }
            throw new FormatException("Unexpected character: " + c);
        }
        void Require(char c) { Space(); if (pos >= text.Length || text[pos++] != c) throw new FormatException("Expected '" + c + "'."); }
    }
}
