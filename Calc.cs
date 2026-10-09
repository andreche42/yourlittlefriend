using System.Globalization;

namespace YourLittleFriend;

// calcolatore di espressioni: + - * / % ^ ! parentesi, costanti (pi, e, tau, ans) e funzioni (sqrt, sin, cos, ln, log, abs, round...)
public static class Calc
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static double Eval(string text, double ans, bool degrees)
    {
        var s = text.Replace(',', '.').Replace('×', '*').Replace('÷', '/').Replace('−', '-').Replace("π", "pi").Replace("√", "sqrt").Replace("**", "^");
        var p = new Parser(s, ans, degrees);
        double v = p.Expr();
        if (!p.AtEnd) throw new FormatException("syntax");
        if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArithmeticException("math");
        return v;
    }

    public static string Format(double v)
    {
        if (v == 0) return "0";
        double a = Math.Abs(v);
        if (a >= 1e15 || a < 1e-9) return v.ToString("0.#########E+0", Inv);
        return Math.Round(v, 10).ToString("0.##########", Inv);
    }

    // 104, 67, 69 e 420 fanno ridere l'omino
    public static bool IsSpecial(double v)
    {
        double r = Math.Round(v);
        return Math.Abs(v - r) < 1e-9 && r is 104 or 67 or 69 or 420;
    }

    sealed class Parser
    {
        readonly string s;
        readonly double ans;
        readonly bool deg;
        int i;

        public Parser(string s, double ans, bool deg) { this.s = s; this.ans = ans; this.deg = deg; }

        public bool AtEnd { get { SkipWs(); return i >= s.Length; } }
        void SkipWs() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        char Peek() { SkipWs(); return i < s.Length ? s[i] : '\0'; }
        bool Eat(char c) { if (Peek() != c) return false; i++; return true; }

        public double Expr()
        {
            double v = Term();
            while (true)
            {
                if (Eat('+')) v += Term();
                else if (Eat('-')) v -= Term();
                else return v;
            }
        }

        double Term()
        {
            double v = Unary();
            while (true)
            {
                if (Eat('*')) v *= Unary();
                else if (Eat('/')) { double d = Unary(); if (d == 0) throw new DivideByZeroException(); v /= d; }
                else if (Eat('%')) { double d = Unary(); if (d == 0) throw new DivideByZeroException(); v %= d; }
                else
                {
                    char c = Peek();   // moltiplicazione implicita: 2(3+4), 2pi
                    if (c == '(' || char.IsLetter(c)) v *= Unary();
                    else return v;
                }
            }
        }

        double Unary()
        {
            if (Eat('-')) return -Unary();
            if (Eat('+')) return Unary();
            return Power();
        }

        double Power()
        {
            double b = Postfix();
            return Eat('^') ? Math.Pow(b, Unary()) : b;   // l'esponente può avere il segno e si legge da destra: 2^3^2
        }

        double Postfix()
        {
            double v = Primary();
            while (Eat('!')) v = Fact(v);
            return v;
        }

        double Primary()
        {
            char c = Peek();
            if (c == '\0') throw new FormatException("end");
            if (c == '(')
            {
                i++;
                double v = Expr();
                if (!Eat(')')) throw new FormatException(")");
                return v;
            }
            if (char.IsDigit(c) || c == '.') return Number();
            if (char.IsLetter(c))
            {
                int st = i;
                while (i < s.Length && char.IsLetter(s[i])) i++;
                if (s[st..i].Equals("log", StringComparison.OrdinalIgnoreCase)) while (i < s.Length && char.IsDigit(s[i])) i++;   // log2, log10
                string id = s[st..i].ToLowerInvariant();
                if (Peek() != '(') return Const(id);
                i++;
                double a = Expr();
                if (!Eat(')')) throw new FormatException(")");
                return Func(id, a);
            }
            throw new FormatException("char");
        }

        double Number()
        {
            int st = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))   // 2e3, ma "2e" resta 2 per e
            {
                int j = i + 1;
                if (j < s.Length && (s[j] == '+' || s[j] == '-')) j++;
                if (j < s.Length && char.IsDigit(s[j]))
                {
                    while (j < s.Length && char.IsDigit(s[j])) j++;
                    i = j;
                }
            }
            if (!double.TryParse(s[st..i], NumberStyles.Float, Inv, out var v)) throw new FormatException("num");
            return v;
        }

        double Const(string id) => id switch
        {
            "pi" => Math.PI,
            "e" => Math.E,
            "tau" => Math.Tau,
            "ans" => ans,
            _ => throw new FormatException(id)
        };

        double Func(string f, double a)
        {
            double Rad(double x) => deg ? x * Math.PI / 180 : x;
            double Back(double x) => deg ? x * 180 / Math.PI : x;
            return f switch
            {
                "sqrt" => a >= 0 ? Math.Sqrt(a) : throw new ArithmeticException(f),
                "cbrt" => Math.Cbrt(a),
                "sin" => Math.Sin(Rad(a)),
                "cos" => Math.Cos(Rad(a)),
                "tan" => Math.Tan(Rad(a)),
                "asin" => Back(Math.Asin(a)),
                "acos" => Back(Math.Acos(a)),
                "atan" => Back(Math.Atan(a)),
                "ln" => a > 0 ? Math.Log(a) : throw new ArithmeticException(f),
                "log" or "log10" => a > 0 ? Math.Log10(a) : throw new ArithmeticException(f),
                "log2" => a > 0 ? Math.Log2(a) : throw new ArithmeticException(f),
                "exp" => Math.Exp(a),
                "abs" => Math.Abs(a),
                "round" => Math.Round(a, MidpointRounding.AwayFromZero),
                "floor" => Math.Floor(a),
                "ceil" => Math.Ceiling(a),
                "fact" => Fact(a),
                _ => throw new FormatException(f)
            };
        }

        static double Fact(double v)
        {
            if (v < 0 || v != Math.Floor(v) || v > 170) throw new ArithmeticException("fact");
            double r = 1;
            for (int k = 2; k <= (int)v; k++) r *= k;
            return r;
        }
    }
}
