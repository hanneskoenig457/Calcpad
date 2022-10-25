﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Calcpad.Core
{
    public class ExpressionParser
    {
        private enum Keywords
        {
            None,
            Hide,
            Show,
            Pre,
            Post,
            Val,
            Equ,
            Noc,
            Deg,
            Rad,
            Gra,
            Repeat,
            Loop,
            Break,
            Continue,
            If,
            ElseIf,
            Else,
            EndIf,
            Local,
            Global,
            Round
        }
        private readonly List<string> _inputFields = new();
        private int _currentField;
        private int _isVal;
        private MathParser _parser;
        private static readonly string[] NewLines = { "\r\n", "\r", "\n" };
        public Settings Settings { get; set; }
        public string HtmlResult { get; private set; }
        public static bool IsUs
        {
            get => Unit.IsUs;
            set => Unit.IsUs = value;
        }

        public ExpressionParser()
        {
            Settings = new Settings();
        }

        public void ClearInputFields()
        {
            _inputFields.Clear();
            _currentField = 0;
        }

        public void SetInputField(string value) => _inputFields.Add(value);

        public string GetInputField()
        {
            if (!_inputFields.Any())
                return "?";

            if (_currentField >= _inputFields.Count)
                _currentField = 0;

            return _inputFields[_currentField++];
        }

        public void Parse(string sourceCode, bool calculate = true) =>
            Parse(sourceCode.Split(NewLines, StringSplitOptions.None), calculate);

        private static Keywords GetKeyword(string s)
        {
            if (s[1] == 'i')
            {
                if (s[2] == 'f')
                    return Keywords.If;
                else
                    return Keywords.None;
            }
            var n = s.Length;
            if (n < 4)
                return Keywords.None;
            var c1 = s[1];
            var c2 = s[2];
            var c3 = s[3];
            if (c1 == 'e')
            {
                if (c2 == 'l')
                {
                    if (c3   == 's' && n > 4 &&
                        s[4] == 'e')
                    {
                        if (n > 7 && 
                            s[5] == ' ' && 
                            s[6] == 'i' &&
                            s[7] == 'f')
                            return Keywords.ElseIf;

                        return Keywords.Else;
                    }
                }
                else if (c2 == 'n')
                {
                    if (c3   == 'd' && n > 6 &&
                        s[4] == ' ' && 
                        s[5] == 'i' &&
                        s[6] == 'f')
                        return Keywords.EndIf;
                }
                else if (c2 == 'q' &&
                         c3 == 'u')
                    return Keywords.Equ;
            }
            else if (c1 == 'p')
            {
                if (c2 == 'r')
                {
                    if(c3 == 'e')
                        return Keywords.Pre;
                }
                else if (c2   == 'o' && 
                         c3   == 's' && n > 4 &&
                         s[4] == 't')
                        return Keywords.Post;
            }
            else if (c1 == 'g')
            {
                if (c2 == 'r')
                {
                    if (c3 == 'a')
                        return Keywords.Gra;
                }
                else if (c2   == 'l' &&
                         c3   == 'o' && n > 6 &&
                         s[4] == 'b' &&
                         s[5] == 'a' &&
                         s[6] == 'l')
                         return Keywords.Global;
            }
            else if (c1 == 'r')
            {
                if (c2 == 'a')
                {
                    if (c3 == 'd')
                        return Keywords.Rad;
                }
                else if (c2 == 'o')
                {
                    if (c3 == 'u' && n > 5 &&
                         s[4] == 'n' &&
                         s[5] == 'd')
                        return Keywords.Round;
                }
                else if (c2   == 'e' &&
                         c3   == 'p' && n > 6 &&
                         s[4] == 'e' &&
                         s[5] == 'a' &&
                         s[6] == 't')
                         return Keywords.Repeat;
            }
            else if (c1 == 'l' &&
                     c2 == 'o')
            {
                if (c3 == 'o')
                {
                    if (n > 4 &&
                        s[4] == 'p')
                        return Keywords.Loop;
                }
                else if (c3 == 'c' && n > 5 &&
                         s[4] == 'a' &&
                         s[5] == 'l')
                    return Keywords.Local;
            }
            else
            {
                if (s.StartsWith("#val", StringComparison.Ordinal))
                    return Keywords.Val;
                if (s.StartsWith("#noc", StringComparison.Ordinal))
                    return Keywords.Noc;
                if (s.StartsWith("#hide", StringComparison.Ordinal))
                    return Keywords.Hide;
                if (s.StartsWith("#show", StringComparison.Ordinal))
                    return Keywords.Show;
                if (s.StartsWith("#deg", StringComparison.Ordinal))
                    return Keywords.Deg;
                if (s.StartsWith("#break", StringComparison.Ordinal))
                    return Keywords.Break;
                if (s.StartsWith("#continue", StringComparison.Ordinal))
                    return Keywords.Continue;
            }

            return Keywords.None;
        }

        public void Cancel() => _parser?.Cancel();

        private void Parse(string[] expressions, bool calculate = true)
        {
            const int RemoveCondition = Keywords.EndIf - Keywords.If;
            var stringBuilder = new StringBuilder(expressions.Length * 80);
            var condition = new ConditionParser();
            _parser = new MathParser(Settings.Math)
            {
                GetInputField = GetInputField
            };
            _isVal = 0;
            var loops = new Stack<Loop>();
            var isVisible = true;
            _parser.IsEnabled = calculate;
            _parser.SetVariable("Units", new Value(UnitsFactor()));
            var line = 0;
            var len = expressions.Length - 1;
            var s = string.Empty;
            try
            {
                for (var i = 0; i < len; ++i)
                {
                    line = i + 1;
                    var id = loops.Any() && loops.Peek().Iteration != 1 ? "" : $" id=\"line{line}\"";
                    if (_parser.IsCanceled)
                        break;

                    s = expressions[i].Trim();
                    if (string.IsNullOrEmpty(s))
                    {
                        if (isVisible)
                            stringBuilder.AppendLine($"<p{id}>&nbsp;</p>");

                        continue;
                    }
                    var lowerCase = s.ToLowerInvariant();
                    var keyword = Keywords.None;
                    if (s[0] == '#')
                    {
                        var isKeyWord = true;
                        keyword = GetKeyword(lowerCase);
                        if (keyword == Keywords.Hide)
                            isVisible = false;
                        else if (keyword == Keywords.Show)
                            isVisible = true;
                        else if (keyword == Keywords.Pre)
                            isVisible = !calculate;
                        else if (keyword == Keywords.Post)
                            isVisible = calculate;
                        else if (keyword == Keywords.Val)
                            _isVal = 1;
                        else if (keyword == Keywords.Equ)
                            _isVal = 0;
                        else if (keyword == Keywords.Noc)
                            _isVal = -1;
                        else if (keyword == Keywords.Deg)
                            _parser.Degrees = 0;
                        else if (keyword == Keywords.Rad)
                            _parser.Degrees = 1;
                        else if (keyword == Keywords.Gra)
                            _parser.Degrees = 2;
                        else if (keyword == Keywords.Round)
                        {
                            var expression = string.Empty;
                            if (s.Length > 6)
                            {
                                expression = s[6..].Trim();
                                if (int.TryParse(expression, out int n))
                                    Settings.Math.Decimals = n;
                                else
                                {
                                    try
                                    {
                                        _parser.Parse(expression);
                                        _parser.Calculate();
                                        Settings.Math.Decimals = (int)Math.Round(_parser.Real);
                                    }
                                    catch (MathParser.MathParserException ex)
                                    {
                                        AppendError(ex.Message);
                                    }
                                }
                            }
                        }
                        else if (keyword == Keywords.Repeat)
                        {
                            var expression = string.Empty;
                            if (s.Length > 7)
                                expression = s[7..].Trim();

                            if (calculate)
                            {
                                if (condition.IsSatisfied)
                                {
                                    var count = 0;
                                    if (!string.IsNullOrWhiteSpace(expression))
                                    {
                                        try
                                        {
                                            _parser.Parse(expression);
                                            _parser.Calculate();
                                            if (_parser.Real > int.MaxValue)
#if BG
                                                AppendError($"Броят на итерациите е по-голям от максималния {int.MaxValue}.</p>");
#else
                                                AppendError($"Number of iterations exceeds the maximum {int.MaxValue}.</p>");
#endif
                                            else
                                                count = (int)Math.Round(_parser.Real);
                                        }
                                        catch (MathParser.MathParserException ex)
                                        {
                                            AppendError(ex.Message);
                                        }
                                    }
                                    else
                                        count = -1;

                                    loops.Push(new Loop(i, count, condition.Id));
                                }
                            }
                            else if (isVisible)
                            {
                                if (string.IsNullOrWhiteSpace(expression))
                                    stringBuilder.Append($"<p{id} class=\"cond\">#repeat</p><div class=\"indent\">");
                                else
                                {
                                    try
                                    {
                                        _parser.Parse(expression);
                                        stringBuilder.Append($"<p{id}><span class=\"cond\">#repeat</span> {_parser.ToHtml()}</p><div class=\"indent\">");
                                    }
                                    catch (MathParser.MathParserException ex)
                                    {
                                        AppendError(ex.Message);
                                    }
                                }
                            }
                        }
                        else if (keyword == Keywords.Loop)
                        {
                            if (calculate)
                            {
                                if (condition.IsSatisfied)
                                {
                                    if (!loops.Any())
#if BG
                                        AppendError("\"#loop\" без съответен \"#repeat\".");
#else                                    
                                        AppendError("\"#loop\" without a corresponding \"#repeat\".");
#endif                                    
                                    else if (loops.Peek().Id != condition.Id)
#if BG
                                        AppendError("Преплитане на \"#if - #end if\" и \"#repeat - #loop\" блокове.");
#else
                                        AppendError("Entangled \"#if - #end if\" and \"#repeat - #loop\" blocks.");
#endif
                                    else if (!loops.Peek().Iterate(ref i))
                                        loops.Pop();
                                }
                            }
                            else if (isVisible)
                            {
                                stringBuilder.Append($"</div><p{id} class=\"cond\">#loop</p>");
                            }
                        }
                        else if (keyword == Keywords.Break)
                        {
                            if (calculate)
                            {
                                if (condition.IsSatisfied)
                                {
                                    if (loops.Any())
                                        loops.Peek().Break();
                                    else
                                        break;
                                }
                            }
                            else if (isVisible)
                                stringBuilder.Append($"<p{id} class=\"cond\">#break</p>");
                        }
                        else if (keyword == Keywords.Continue)
                        {
                            if (calculate)
                            {
                                if (condition.IsSatisfied)
                                {
                                    if (!loops.Any())
#if BG
                                    AppendError("\"##continue\" без съответен \"#repeat\".");
#else
                                        AppendError("\"##continue\" without a corresponding \"#repeat\".");
#endif
                                    else
                                    {
                                        var loop = loops.Peek();
                                        while (condition.Id > loop.Id)
                                            condition.SetCondition(RemoveCondition);
                                        loop.Iterate(ref i);
                                    }
                                        
                                }
                            }
                            else if (isVisible)
                                stringBuilder.Append($"<p{id} class=\"cond\">#continue</p>");
                        }
                        else if (keyword != Keywords.Global && keyword != Keywords.Local)
                            isKeyWord = false;

                        if (isKeyWord)
                            continue;
                    }
                    if (lowerCase.StartsWith("$plot", StringComparison.Ordinal) || 
                        lowerCase.StartsWith("$map", StringComparison.Ordinal))
                    {
                        if (isVisible && (condition.IsSatisfied || !calculate))
                        {
                            PlotParser plotParser;
                            if (lowerCase.StartsWith("$p", StringComparison.Ordinal))
                                plotParser = new ChartParser(_parser, Settings.Plot);
                            else
                                plotParser = new MapParser(_parser, Settings.Plot);

                            try
                            {
                                _parser.IsPlotting = true;
                                s = plotParser.Parse(s, calculate);
                                stringBuilder.Append(InsertAttribute(s, id));
                                _parser.IsPlotting = false;
                            }
                            catch (MathParser.MathParserException ex)
                            {
                                AppendError(ex.Message);
                            }
                        }
                    }
                    else
                    {
                        condition.SetCondition(keyword - Keywords.If);
                        if (condition.IsSatisfied && !(loops.Any() && loops.Peek().IsBroken) || !calculate)
                        {
                            var kwdLength = condition.KeyWordLength;
                            if (kwdLength == s.Length)
                            {
                                if (condition.IsUnchecked)
#if BG
                                    throw new MathParser.MathParserException("Условието не може да бъде празно.");
#else
                                    throw new MathParser.MathParserException("Condition cannot be empty.");
#endif
                                if (isVisible && !calculate)
                                {
                                    if (keyword == Keywords.Else)
                                        stringBuilder.Append($"</div><p{id}>{condition.ToHtml()}</p><div class = \"indent\">");
                                    else
                                        stringBuilder.Append($"</div><p{id}>{condition.ToHtml()}</p>");
                                }
                            }
                            else if (kwdLength > 0 && condition.IsFound && condition.IsUnchecked && calculate)
                                condition.Check(0.0);
                            else
                            {
                                var tokens = GetInput(s, kwdLength);
                                var lineType = TokenTypes.Text;
                                if (tokens.Any())
                                    lineType = tokens[0].Type;
                                var isOutput = isVisible && (!calculate || kwdLength == 0);
                                if (isOutput)
                                {
                                    if (keyword == Keywords.ElseIf || keyword == Keywords.EndIf)
                                        stringBuilder.Append("</div>");

                                    if (lineType == TokenTypes.Heading)
                                        stringBuilder.Append($"<h3{id}>");
                                    else if (lineType == TokenTypes.Html)
                                        tokens[0] = new Token(InsertAttribute(tokens[0].Value, id), TokenTypes.Html);
                                    else
                                        stringBuilder.Append($"<p{id}>");

                                    if (kwdLength > 0 && !calculate)
                                        stringBuilder.Append(condition.ToHtml());
                                }

                                foreach (var token in tokens)
                                {
                                    if (token.Type == TokenTypes.Expression)
                                    {
                                        try
                                        {
                                            _parser.Parse(token.Value);
                                            if (calculate && _isVal > -1)
                                                _parser.Calculate();

                                            if (isOutput)
                                            {
                                                if (_isVal == 1 & calculate)
                                                    stringBuilder.Append(Complex.Format(_parser.Result, Settings.Math.Decimals, OutputWriter.OutputFormat.Html));
                                                else
                                                {
                                                    if (Settings.Math.FormatEquations)
                                                        stringBuilder.Append($"<span class=\"eq\" data-xml=\'{_parser.ToXml()}\'>{_parser.ToHtml()}</span>");
                                                    else
                                                        stringBuilder.Append($"<span class=\"eq\">{_parser.ToHtml()}</span>");

                                                }
                                            }
                                        }
                                        catch (MathParser.MathParserException ex)
                                        {
                                            string errText;
                                            if (!calculate && token.Value.Contains('?', StringComparison.Ordinal))
                                                errText = token.Value.Replace("?", "<input type=\"text\" size=\"2\" name=\"Var\">");
                                            else
                                                errText = token.Value;
#if BG
                                            errText = $"Грешка в \"{errText}\" на ред {LineHtml(line)}: {ex.Message}";
#else      
                                            errText = $"Error in \"{errText}\" on line {LineHtml(line)}: {ex.Message}";
#endif
                                            stringBuilder.Append(ErrHtml(errText));
                                        }
                                    }
                                    else if (isVisible)
                                        stringBuilder.Append(token.Value);
                                }
                                if (isOutput)
                                {
                                    if (lineType == TokenTypes.Heading)
                                        stringBuilder.Append("</h3>");
                                    else if (lineType != TokenTypes.Html)
                                        stringBuilder.Append("</p>");

                                    if (keyword == Keywords.If || keyword == Keywords.ElseIf)
                                        stringBuilder.Append("<div class = \"indent\">");

                                    stringBuilder.AppendLine();
                                }
                                if (condition.IsUnchecked)
                                {
                                    if (calculate)
                                        condition.Check(_parser.Result);
                                    else
                                        condition.Check();
                                }
                            }
                        }
                        else if (calculate)
                            PurgeObsoleteInput(s);
                    }
                }
                ApplyUnits(stringBuilder, calculate);
                if (condition.Id > 0 && line == len)
#if BG
                    stringBuilder.Append(ErrHtml($"Грешка: Условният \"#if\" блок не е затворен. Липсва \"#end if\"."));
#else
                    stringBuilder.Append(ErrHtml($"Error: \"#if\" block not closed. Missing \"#end if\"."));
#endif
                if (loops.Any())
#if BG
                    stringBuilder.Append(ErrHtml($"Грешка: Блокът за цикъл \"#repeat\" не е затворен. Липсва \"#loop\"."));
#else
                    stringBuilder.Append(ErrHtml($"<p class=\"err\">Error: \"#repeat\" block not closed. Missing \"#loop\"."));
#endif
            }
            catch (MathParser.MathParserException ex)
            {
                AppendError(ex.Message);
            }
            catch (Exception ex)
            {
#if BG
                stringBuilder.Append(ErrHtml($"Неочаквана грешка: {ex.Message} Моля проверете коректността на израза."));
#else
                stringBuilder.Append(ErrHtml($"Unexpected error: {ex.Message} Please check the expression consistency."));
#endif
            }
            finally
            {
                HtmlResult = stringBuilder.ToString();
                _parser = null;
            }
            void AppendError(string text) =>
#if BG
                stringBuilder.Append(ErrHtml($"Грешка в \"{s}\" на ред {LineHtml(line)}: {text}</p>"));
#else
                stringBuilder.Append(ErrHtml($"Error in \"{s}\" on line {LineHtml(line)}: {text}</p>"));
#endif
            static string ErrHtml(string text) => $"<p class=\"err\">{text}</p>";
            static string LineHtml(int line) => $"[<a href=\"#0\" data-text=\"{line}\">{line}</a>]";
        }

        private static string InsertAttribute(string s, string attr)
        {
            if (s.Length > 2 && s[0] == '<' && char.IsLetter(s[1]))
            {
                var i = s.IndexOf('>');
                if (i > 1)
                {
                    var j = i;
                    while (j > 1)
                    {
                        --j;
                        if (s[j] != ' ')
                        {
                            if (s[j] == '/')
                                i = j;

                            break;
                        }
                    };
                    return s[..i] + attr + s[i..];
                }
            }
            return s;
        }

        private void ApplyUnits(StringBuilder sb, bool calculate)
        {
            string unitsHtml;
            if (calculate)
                unitsHtml = Settings.Units;
            else
                unitsHtml = "<span class=\"Units\">" + Settings.Units + "</span>";

            long len = sb.Length;
            sb.Replace("%u", unitsHtml);
            if (calculate || sb.Length == len)
                return;

            sb.Insert(0, "<select id=\"Units\" name=\"Units\"><option value=\"m\"> m </option><option value=\"cm\"> cm </option><option value=\"mm\"> mm </option></select>");
        }

        private double UnitsFactor()
        {
            return Settings.Units switch
            {
                "mm" => 1000,
                "cm" => 100,
                "m" => 1,
                _ => 0
            };
        }

        private void PurgeObsoleteInput(string s)
        {
            var isExpression = true;
            for (int i = 0, len = s.Length; i < len; ++i)
            {
                var c = s[i];
                if (c == '\'' || c == '\"')
                    isExpression = !isExpression;
                else if (c == '?' && isExpression)
                    GetInputField();
            }
        }

        private List<Token> GetInput(string s, int startIndex)
        {
            var tokens = new List<Token>();
            var stringBuilder = new StringBuilder();
            var currentSeparator = ' ';
            for (int i = startIndex, len = s.Length; i < len; ++i)
            {
                var c = s[i];
                if (c == '\'' || c == '\"')
                {
                    if (currentSeparator == ' ' || currentSeparator == c)
                    {
                        if (stringBuilder.Length != 0)
                        {
                            AddToken(tokens, stringBuilder.ToString(), currentSeparator);
                            stringBuilder.Clear();
                        }
                        if (currentSeparator == c)
                            currentSeparator = ' ';
                        else
                            currentSeparator = c;
                    }
                    else if (currentSeparator != ' ')
                        stringBuilder.Append(c);
                }
                else
                    stringBuilder.Append(c);
            }
            if (stringBuilder.Length != 0)
                AddToken(tokens, stringBuilder.ToString(), currentSeparator);

            return tokens;
        }

        private void AddToken(List<Token> tokens, string value, char separator)
        {
            var tokenValue = value;
            var tokenType = GetTokenType(separator);

            if (tokenType == TokenTypes.Expression)
            {
                if (string.IsNullOrWhiteSpace(tokenValue))
                    return;
            }
            else if (_isVal < 1)
            {
                if (!tokens.Any())
                    tokenValue += ' ';
                else
                    tokenValue = ' ' + tokenValue + ' ';
            }

            var token = new Token(tokenValue, tokenType);
            if (token.Type == TokenTypes.Text)
            {
                tokenValue = tokenValue.TrimStart();
                if (tokenValue.Length > 0 && tokenValue[0] == '<')
                    token.Type = TokenTypes.Html;
            }
            tokens.Add(token);
        }

        private static TokenTypes GetTokenType(char separator)
        {
            return separator switch
            {
                ' ' => TokenTypes.Expression,
                '\"' => TokenTypes.Heading,
                '\'' => TokenTypes.Text,
                _ => TokenTypes.Error,
            };
        }

        private struct Token
        {
            internal string Value { get; }
            internal TokenTypes Type;
            internal Token(string value, TokenTypes type)
            {
                Value = value;
                Type = type;
            }
        }

        private enum TokenTypes
        {
            Expression,
            Heading,
            Text,
            Html,
            Error
        }

        private class ConditionParser
        {
            private enum Types
            {
                None,
                If,
                ElseIf,
                Else,
                EndIf
            }
            private readonly struct Item
            {
                internal bool Value { get; }
                internal Types Type { get; }
                internal Item(bool value, Types type)
                {
                    Type = type;
                    Value = value;
                }
            }

            private int _count;
            private string _keyword;
            private int _keywordLength;
            private readonly Item[] _conditions = new Item[20];
            private Types Type => _conditions[Id].Type;
            internal int Id { get; private set; }
            internal bool IsUnchecked { get; private set; }
            internal bool IsSatisfied => _conditions[_count].Value;
            internal bool IsFound { get; private set; }
            internal int KeyWordLength => _keywordLength;

            internal ConditionParser()
            {
                _conditions[0] = new Item(true, Types.None);
                _keyword = string.Empty;
            }
            private void Add(bool value)
            {
                ++Id;
                _conditions[Id] = new Item(value, Types.If);
                if (IsSatisfied)
                {
                    ++_count;
                    IsFound = false;
                }
            }

            private void Remove()
            {
                --Id;
                if (_count > Id)
                {
                    --_count;
                    IsFound = true;

                }
            }

            private void Change(bool value, Types type)
            {
                _conditions[Id] = new Item(value, type);
            }

            internal void SetCondition(int index)
            {
                if (index < 0 || index >= (int)Types.EndIf)
                {
                    if (_keywordLength > 0)
                    {
                        _keywordLength = 0;
                        _keyword = string.Empty;
                    }
                    return;
                }

                var type = (Types)(index + 1);
                _keywordLength = GetKeywordLength(type);
                _keyword = GetKeyword(type);
                IsUnchecked = type == Types.If || type == Types.ElseIf;
                if (type > Types.If && _count == 0)
#if BG
                    throw new MathParser.MathParserException("Условният блок не е инициализиран с \"#if\".");
#else                    
                    throw new MathParser.MathParserException("Condition block not initialized with \"#if\".");
#endif
                if (Type == Types.Else)
                {
                    if (type == Types.Else)
#if BG
                        throw new MathParser.MathParserException("Може да има само едно \"#else\" в условен блок.");
#else                         
                        throw new MathParser.MathParserException("Duplicate \"#else\" in condition block.");
#endif
                    if (type == Types.ElseIf)
#if BG
                        throw new MathParser.MathParserException("Не може да има \"#else if\" след \"#else\" в условен блок.");
#else                             
                        throw new MathParser.MathParserException("\"#else if\" is not allowed after \"#else\" in condition block.");
#endif
                }
                switch (type)
                {
                    case Types.If:
                        Add(true);
                        break;
                    case Types.ElseIf:
                        Change(true, Types.If);
                        break;
                    case Types.Else:
                        Change(!IsFound, type);
                        break;
                    case Types.EndIf:
                        Remove();
                        break;
                }
            }

            internal void Check(Complex value)
            {
                if (!value.IsReal)
#if BG                    
                    throw new MathParser.MathParserException("Условието не може да бъде комплексно число.");
#else                    
                    throw new MathParser.MathParserException("Condition cannot evaluate to a complex number.");
#endif
                var d = value.Re;
                if (double.IsNaN(d) || double.IsInfinity(d))
#if BG
                    throw new MathParser.MathParserException($"Невалиден резултат от проверка на условие: {d}.");
#else
                    throw new MathParser.MathParserException($"Condition result is invalid: {d}.");
#endif
                var result = Math.Abs(d) > 1e-12;
                if (result)
                    IsFound = true;
                Change(result, Type);
                IsUnchecked = false;
            }

            internal void Check() => IsUnchecked = false;

            public override string ToString() => _keyword;

            internal string ToHtml()
            {
                if (string.IsNullOrEmpty(_keyword))
                    return _keyword;
                return "<span class=\"cond\">" + _keyword + "</span>";
            }

            private static int GetKeywordLength(Types type)
            {
                return type switch
                {
                    Types.If => 3,
                    Types.ElseIf => 8,
                    Types.Else => 5,
                    Types.EndIf => 7,
                    _ => 0,
                };
            }
            private static string GetKeyword(Types type)
            {
                return type switch
                {
                    Types.If => "#if ",
                    Types.ElseIf => "#else if ",
                    Types.Else => "#else",
                    Types.EndIf => "#end if",
                    _ => string.Empty,
                };
            }
        }

        private class Loop
        {
            private readonly int _startLine;
            private int _iteration;
            internal int Id { get; }
            internal int Iteration => _iteration;
            internal Loop(int startLine, int count, int id)
            {
                _startLine = startLine;
                if (count < 0)
                    count = 100000;

                _iteration = count;
                Id = id;
            }

            internal bool Iterate(ref int currentLine)
            {
                if (_iteration <= 1)
                    return false;

                currentLine = _startLine;
                --_iteration;
                return true;
            }

            internal void Break() => _iteration = 0;

            internal bool IsBroken => _iteration == 0;  
        }
    }
}
