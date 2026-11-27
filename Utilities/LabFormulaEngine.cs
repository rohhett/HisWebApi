using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HISWEBAPI.Utilities
{
    /// <summary>
    /// Lab formula and result-validation engine (2026-11-27, Formula Master redesign).
    /// The same language and rules are implemented in the frontend in src/utils/labFormulaEngine.ts —
    /// change both together.
    ///
    /// Expression language:
    ///   numbers · obs(&lt;ObservationId&gt;) · AGE (completed years), AGE_MONTHS, AGE_DAYS
    ///   + - * / ^ ( ) , unary minus
    ///   min(a,b,..) max(a,b,..) pow(a,b) sqrt(x) abs(x) log(x) (base 10) ln(x) exp(x) round(x[,d])
    /// A formula gives no value (null) when an observation it uses is blank / not a number, or the maths is invalid.
    /// </summary>
    public static class LabFormulaEngine
    {
        public static readonly string[] CheckTypes = { "SUM", "DIFFERENCE", "PRODUCT", "RATIO", "AVERAGE", "MIN", "MAX", "EACH", "EXPRESSION" };
        public static readonly string[] Operators = { "EQ", "NE", "GT", "GE", "LT", "LE", "BETWEEN", "NOTBETWEEN" };

        private static readonly Dictionary<string, (int Min, int Max)> Functions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["min"] = (1, 50), ["max"] = (1, 50), ["pow"] = (2, 2), ["sqrt"] = (1, 1), ["abs"] = (1, 1),
            ["log"] = (1, 1), ["ln"] = (1, 1), ["exp"] = (1, 1), ["round"] = (1, 2)
        };

        private static readonly HashSet<string> Variables = new(StringComparer.OrdinalIgnoreCase) { "AGE", "AGE_MONTHS", "AGE_DAYS" };

        public sealed class EvalContext
        {
            public Func<int, double?> Observation { get; init; } = _ => null;
            public double? AgeYears { get; init; }
            public double? AgeMonths { get; init; }
            public double? AgeDays { get; init; }
        }

        // ---------------- AST ----------------
        public abstract class Node { }
        private sealed class NumNode : Node { public double V; }
        private sealed class ObsNode : Node { public int Id; }
        private sealed class VarNode : Node { public string Name = ""; }
        private sealed class NegNode : Node { public Node A = null!; }
        private sealed class BinNode : Node { public char Op; public Node A = null!, B = null!; }
        private sealed class CallNode : Node { public string Fn = ""; public List<Node> Args = new(); }

        // ---------------- tokenizer + parser ----------------
        private sealed class Parser
        {
            private readonly List<(char Kind, string Text)> _t = new();   // N number, I identifier, S symbol
            private int _p;
            public readonly HashSet<int> ObservationIds = new();

            public Parser(string s)
            {
                int i = 0;
                while (i < s.Length)
                {
                    char c = s[i];
                    if (char.IsWhiteSpace(c)) { i++; continue; }
                    if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                    {
                        int st = i; bool dot = false;
                        while (i < s.Length && (char.IsDigit(s[i]) || (s[i] == '.' && !dot))) { if (s[i] == '.') dot = true; i++; }
                        _t.Add(('N', s[st..i]));
                        continue;
                    }
                    if (char.IsLetter(c) || c == '_')
                    {
                        int st = i;
                        while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                        _t.Add(('I', s[st..i]));
                        continue;
                    }
                    if ("+-*/^(),".IndexOf(c) >= 0) { _t.Add(('S', c.ToString())); i++; continue; }
                    throw new FormatException($"Character '{c}' is not allowed in a formula");
                }
            }

            private bool IsSym(string s) => _p < _t.Count && _t[_p].Kind == 'S' && _t[_p].Text == s;
            private void Expect(string s) { if (!IsSym(s)) throw new FormatException($"'{s}' expected"); _p++; }

            public Node ParseAll()
            {
                if (_t.Count == 0) throw new FormatException("Formula is empty");
                var n = Expr();
                if (_p < _t.Count) throw new FormatException($"Unexpected '{_t[_p].Text}'");
                return n;
            }

            private Node Expr()
            {
                var a = Term();
                while (IsSym("+") || IsSym("-")) { char op = _t[_p++].Text[0]; a = new BinNode { Op = op, A = a, B = Term() }; }
                return a;
            }

            private Node Term()
            {
                var a = Unary();
                while (IsSym("*") || IsSym("/")) { char op = _t[_p++].Text[0]; a = new BinNode { Op = op, A = a, B = Unary() }; }
                return a;
            }

            private Node Unary()
            {
                if (IsSym("-")) { _p++; return new NegNode { A = Unary() }; }
                if (IsSym("+")) { _p++; return Unary(); }
                return Power();
            }

            private Node Power()
            {
                var a = Primary();
                if (IsSym("^")) { _p++; return new BinNode { Op = '^', A = a, B = Unary() }; }   // right associative
                return a;
            }

            private Node Primary()
            {
                if (_p >= _t.Count) throw new FormatException("Formula is incomplete");
                var (kind, text) = _t[_p];
                if (kind == 'N')
                {
                    _p++;
                    return new NumNode { V = double.Parse(text, CultureInfo.InvariantCulture) };
                }
                if (kind == 'S' && text == "(")
                {
                    _p++;
                    var e = Expr();
                    Expect(")");
                    return e;
                }
                if (kind == 'I')
                {
                    _p++;
                    if (text.Equals("obs", StringComparison.OrdinalIgnoreCase))
                    {
                        Expect("(");
                        if (_p >= _t.Count || _t[_p].Kind != 'N' || !int.TryParse(_t[_p].Text, out int id) || id <= 0)
                            throw new FormatException("obs( ) needs an observation id");
                        _p++;
                        Expect(")");
                        ObservationIds.Add(id);
                        return new ObsNode { Id = id };
                    }
                    if (Functions.TryGetValue(text, out var arity))
                    {
                        Expect("(");
                        var call = new CallNode { Fn = text.ToLowerInvariant() };
                        if (!IsSym(")"))
                        {
                            call.Args.Add(Expr());
                            while (IsSym(",")) { _p++; call.Args.Add(Expr()); }
                        }
                        Expect(")");
                        if (call.Args.Count < arity.Min || call.Args.Count > arity.Max)
                            throw new FormatException($"{call.Fn}( ) takes {(arity.Min == arity.Max ? arity.Min.ToString() : $"{arity.Min} or more")} value(s)");
                        return call;
                    }
                    if (Variables.Contains(text)) return new VarNode { Name = text.ToUpperInvariant() };
                    throw new FormatException($"'{text}' is not a known variable or function");
                }
                throw new FormatException($"Unexpected '{text}'");
            }
        }

        /// <summary>Parses an expression. Returns false with a message when it is not valid.</summary>
        public static bool TryParse(string? expression, out Node? ast, out HashSet<int> observationIds, out string error)
        {
            ast = null;
            observationIds = new HashSet<int>();
            error = "";
            try
            {
                var p = new Parser(expression ?? "");
                ast = p.ParseAll();
                observationIds = p.ObservationIds;
                return true;
            }
            catch (FormatException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static double? Evaluate(Node ast, EvalContext ctx)
        {
            double? r = Eval(ast, ctx);
            return r.HasValue && double.IsFinite(r.Value) ? r : null;
        }

        private static double? Eval(Node n, EvalContext ctx)
        {
            switch (n)
            {
                case NumNode x: return x.V;
                case ObsNode x: return ctx.Observation(x.Id);
                case VarNode x:
                    return x.Name switch { "AGE" => ctx.AgeYears, "AGE_MONTHS" => ctx.AgeMonths, "AGE_DAYS" => ctx.AgeDays, _ => null };
                case NegNode x: return -Eval(x.A, ctx);
                case BinNode x:
                {
                    var a = Eval(x.A, ctx); var b = Eval(x.B, ctx);
                    if (a == null || b == null) return null;
                    return x.Op switch
                    {
                        '+' => a + b, '-' => a - b, '*' => a * b,
                        '/' => b == 0 ? null : a / b,
                        '^' => Math.Pow(a.Value, b.Value),
                        _ => null
                    };
                }
                case CallNode x:
                {
                    var args = x.Args.Select(a => Eval(a, ctx)).ToList();
                    if (args.Any(a => a == null)) return null;
                    var v = args.Select(a => a!.Value).ToList();
                    return x.Fn switch
                    {
                        "min" => v.Min(), "max" => v.Max(), "pow" => Math.Pow(v[0], v[1]),
                        "sqrt" => v[0] < 0 ? null : Math.Sqrt(v[0]), "abs" => Math.Abs(v[0]),
                        "log" => v[0] <= 0 ? null : Math.Log10(v[0]), "ln" => v[0] <= 0 ? null : Math.Log(v[0]),
                        "exp" => Math.Exp(v[0]),
                        "round" => Math.Round(v[0], v.Count > 1 ? Math.Clamp((int)v[1], 0, 10) : 0, MidpointRounding.AwayFromZero),
                        _ => null
                    };
                }
            }
            return null;
        }

        /// <summary>A result text as a number; blank or text (e.g. "Positive") gives null.</summary>
        public static double? ToNumber(string? value)
        {
            var s = (value ?? "").Replace(",", "").Trim();
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d) ? d : null;
        }

        // ---------------- result validations ----------------
        public sealed class ValidationRule
        {
            public int ValidationId { get; set; }
            public string ValidationName { get; set; } = "";
            public string CheckType { get; set; } = "SUM";
            public string Expression { get; set; } = "";
            public string Operator { get; set; } = "EQ";
            public decimal Value1 { get; set; }
            public decimal? Value2 { get; set; }
            public decimal Tolerance { get; set; }
            public int ActionType { get; set; } = 1;
            public int SkipIfBlank { get; set; } = 1;
            public string ErrorMessage { get; set; } = "";
            public List<(int ObservationId, string ObservationName)> Observations { get; set; } = new();
        }

        public sealed class ValidationOutcome
        {
            public bool Skipped { get; set; }
            public bool Passed { get; set; }
            public double? Actual { get; set; }
            public string Message { get; set; } = "";
        }

        public static ValidationOutcome CheckValidation(ValidationRule rule, EvalContext ctx)
        {
            var outcome = new ValidationOutcome();
            var values = rule.Observations.Select(o => (o.ObservationId, o.ObservationName, Value: ctx.Observation(o.ObservationId))).ToList();
            bool anyBlank = values.Any(v => v.Value == null);
            var entered = values.Where(v => v.Value != null).ToList();

            if (rule.CheckType == "EXPRESSION")
            {
                if (!TryParse(rule.Expression, out var ast, out _, out _) || ast == null) { outcome.Skipped = true; return outcome; }
                outcome.Actual = Evaluate(ast, ctx);
                if (outcome.Actual == null) { outcome.Skipped = true; return outcome; }
                outcome.Passed = Compare(outcome.Actual.Value, rule);
            }
            else if ((anyBlank && rule.SkipIfBlank == 1) || entered.Count == 0)
            {
                outcome.Skipped = true;
                return outcome;
            }
            else if (rule.CheckType == "EACH")
            {
                var failed = entered.Where(v => !Compare(v.Value!.Value, rule)).ToList();
                outcome.Passed = failed.Count == 0;
                if (!outcome.Passed) outcome.Actual = failed[0].Value;
            }
            else
            {
                var v = entered.Select(e => e.Value!.Value).ToList();
                double? actual = rule.CheckType switch
                {
                    "SUM" => v.Sum(),
                    "DIFFERENCE" => v[0] - v.Skip(1).Sum(),
                    "PRODUCT" => v.Aggregate(1d, (a, b) => a * b),
                    "RATIO" => v.Skip(1).Aggregate((double?)v[0], (a, b) => b == 0 || a == null ? null : a / b),
                    "AVERAGE" => v.Average(),
                    "MIN" => v.Min(),
                    "MAX" => v.Max(),
                    _ => null
                };
                if (actual == null || !double.IsFinite(actual.Value)) { outcome.Skipped = true; return outcome; }
                outcome.Actual = Math.Round(actual.Value, 4);
                outcome.Passed = Compare(outcome.Actual.Value, rule);
            }

            if (!outcome.Passed) outcome.Message = BuildMessage(rule, outcome.Actual);
            return outcome;
        }

        private static bool Compare(double actual, ValidationRule rule)
        {
            const double eps = 1e-9;
            double v1 = (double)rule.Value1, v2 = (double)(rule.Value2 ?? rule.Value1), tol = Math.Abs((double)rule.Tolerance);
            double lo = Math.Min(v1, v2), hi = Math.Max(v1, v2);
            return rule.Operator switch
            {
                "EQ" => Math.Abs(actual - v1) <= tol + eps,
                "NE" => Math.Abs(actual - v1) > tol + eps,
                "GT" => actual > v1 + eps,
                "GE" => actual >= v1 - eps,
                "LT" => actual < v1 - eps,
                "LE" => actual <= v1 + eps,
                "BETWEEN" => actual >= lo - eps && actual <= hi + eps,
                "NOTBETWEEN" => actual < lo - eps || actual > hi + eps,
                _ => true
            };
        }

        private static string Num(decimal d) => d.ToString("0.####", CultureInfo.InvariantCulture);

        public static string ConditionText(ValidationRule rule) => rule.Operator switch
        {
            "EQ" => $"equal to {Num(rule.Value1)}" + (rule.Tolerance != 0 ? $" (± {Num(rule.Tolerance)})" : ""),
            "NE" => $"not equal to {Num(rule.Value1)}",
            "GT" => $"greater than {Num(rule.Value1)}",
            "GE" => $"at least {Num(rule.Value1)}",
            "LT" => $"less than {Num(rule.Value1)}",
            "LE" => $"at most {Num(rule.Value1)}",
            "BETWEEN" => $"between {Num(rule.Value1)} and {Num(rule.Value2 ?? rule.Value1)}",
            "NOTBETWEEN" => $"outside {Num(rule.Value1)} to {Num(rule.Value2 ?? rule.Value1)}",
            _ => ""
        };

        private static string BuildMessage(ValidationRule rule, double? actual)
        {
            if (!string.IsNullOrWhiteSpace(rule.ErrorMessage)) return rule.ErrorMessage.Trim();
            var names = rule.Observations.Select(o => o.ObservationName).ToList();
            string subject = rule.CheckType switch
            {
                "SUM" => "Sum of " + string.Join(" + ", names),
                "DIFFERENCE" => string.Join(" - ", names),
                "PRODUCT" => string.Join(" × ", names),
                "RATIO" => string.Join(" ÷ ", names),
                "AVERAGE" => "Average of " + string.Join(", ", names),
                "MIN" => "Lowest of " + string.Join(", ", names),
                "MAX" => "Highest of " + string.Join(", ", names),
                "EACH" => string.Join(", ", names),
                _ => rule.ValidationName
            };
            string found = actual.HasValue ? $" (found {actual.Value.ToString("0.####", CultureInfo.InvariantCulture)})" : "";
            return $"{rule.ValidationName}: {subject} must be {ConditionText(rule)}{found}";
        }
    }
}
