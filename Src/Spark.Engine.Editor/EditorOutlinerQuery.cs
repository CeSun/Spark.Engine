using System.Text;

namespace Spark.Engine.Editor;

/// <summary>Outliner 可检索节点的规范化字段集合。</summary>
public sealed record EditorOutlinerSearchRecord(
    string Label,
    string Type,
    string Folder,
    string Id,
    string Socket,
    IReadOnlyList<string> Components);

/// <summary>
/// UE 风格 Outliner 查询：空格分隔为 AND，- 排除，+ 完整词，双引号完整短语，
/// 并支持 label/type/folder/id/socket/component 字段。
/// </summary>
public sealed class EditorOutlinerQuery
{
    private readonly IReadOnlyList<Term> _terms;

    private EditorOutlinerQuery(string text, IReadOnlyList<Term> terms)
    {
        Text = text;
        _terms = terms;
    }

    public string Text { get; }
    public bool IsEmpty => _terms.Count == 0;

    public static EditorOutlinerQuery Parse(string? text)
    {
        var normalized = text?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            return new EditorOutlinerQuery(string.Empty, Array.Empty<Term>());
        return new EditorOutlinerQuery(normalized, Tokenize(normalized).Select(ParseTerm)
            .Where(term => term.Value.Length != 0).ToArray());
    }

    public bool Matches(EditorOutlinerSearchRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        foreach (var term in _terms)
        {
            var matched = MatchTerm(record, term);
            if (term.Negated ? matched : !matched)
                return false;
        }
        return true;
    }

    private static bool MatchTerm(EditorOutlinerSearchRecord record, Term term)
    {
        IEnumerable<string> values = term.Field switch
        {
            "label" => [record.Label],
            "type" => [record.Type],
            "folder" => [record.Folder],
            "id" => [record.Id],
            "socket" => [record.Socket],
            "component" => record.Components,
            _ => [record.Label, record.Type, record.Folder, record.Id, record.Socket],
        };
        return values.Any(value => term.ExactWord
            ? ContainsWholeWord(value, term.Value)
            : value.Contains(term.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsWholeWord(string source, string value)
    {
        if (value.Length == 0)
            return true;
        for (var index = 0; index <= source.Length - value.Length; index++)
        {
            if (!source.AsSpan(index, value.Length).Equals(value, StringComparison.OrdinalIgnoreCase))
                continue;
            var before = index == 0 || !char.IsLetterOrDigit(source[index - 1]);
            var end = index + value.Length;
            var after = end == source.Length || !char.IsLetterOrDigit(source[end]);
            if (before && after)
                return true;
        }
        return false;
    }

    private static Term ParseTerm(Token token)
    {
        var value = token.Value;
        var negated = value.StartsWith('-');
        if (negated)
            value = value[1..];
        var exactWord = value.StartsWith('+');
        if (exactWord)
            value = value[1..];
        string? field = null;
        var separator = value.IndexOf(':');
        if (separator > 0)
        {
            var candidate = value[..separator].ToLowerInvariant();
            if (candidate is "label" or "type" or "folder" or "id" or "socket" or "component")
            {
                field = candidate;
                value = value[(separator + 1)..];
            }
        }
        return new Term(value, field, negated, exactWord || token.Quoted);
    }

    private static IEnumerable<Token> Tokenize(string text)
    {
        var value = new StringBuilder();
        var quoted = false;
        var tokenQuoted = false;
        foreach (var character in text)
        {
            if (character == '"')
            {
                quoted = !quoted;
                tokenQuoted = true;
                continue;
            }
            if (char.IsWhiteSpace(character) && !quoted)
            {
                if (value.Length != 0)
                {
                    yield return new Token(value.ToString(), tokenQuoted);
                    value.Clear();
                    tokenQuoted = false;
                }
                continue;
            }
            value.Append(character);
        }
        if (value.Length != 0)
            yield return new Token(value.ToString(), tokenQuoted);
    }

    private sealed record Term(string Value, string? Field, bool Negated, bool ExactWord);
    private readonly record struct Token(string Value, bool Quoted);
}
