using System.Text;

namespace AiAgents.Web.Components.Chat;

internal static class FileCitationMarkerFormatter
{
    private const char MarkerStart = '\uE200';
    private const char MarkerEnd = '\uE201';

    public static string Format(string text, bool isStreaming)
    {
        var result = new StringBuilder(text.Length);
        int position = 0;
        while (position < text.Length)
        {
            int start = text.IndexOf(MarkerStart, position);
            if (start < 0)
            {
                result.Append(text.AsSpan(position));
                break;
            }

            result.Append(text.AsSpan(position, start - position));
            var status = ReadMarker(text.AsSpan(start), out int length);
            if (status == MarkerStatus.Complete)
            {
                position = start + length;
            }
            else if (status == MarkerStatus.Incomplete && isStreaming)
            {
                break;
            }
            else
            {
                result.Append(MarkerStart);
                position = start + 1;
            }
        }

        return result.ToString();
    }

    private static MarkerStatus ReadMarker(ReadOnlySpan<char> text, out int length)
    {
        length = 0;
        int position = 0;
        var status = ReadLiteral(text, ref position, "\uE200filecite\uE202turn");
        if (status != MarkerStatus.Complete)
        {
            return status;
        }

        status = ReadDigits(text, ref position);
        if (status != MarkerStatus.Complete)
        {
            return status;
        }

        status = ReadLiteral(text, ref position, "file");
        if (status != MarkerStatus.Complete)
        {
            return status;
        }

        status = ReadDigits(text, ref position);
        if (status != MarkerStatus.Complete)
        {
            return status;
        }

        // The observed marker lacked its closing delimiter; require a text boundary in that case.
        if (position < text.Length && text[position] == MarkerEnd)
        {
            position++;
        }
        else if (position < text.Length && !char.IsWhiteSpace(text[position]) &&
            !char.IsPunctuation(text[position]) && text[position] != MarkerStart)
        {
            return MarkerStatus.Invalid;
        }

        length = position;
        return MarkerStatus.Complete;
    }

    private static MarkerStatus ReadLiteral(ReadOnlySpan<char> text, ref int position, string literal)
    {
        foreach (char expected in literal)
        {
            if (position == text.Length)
            {
                return MarkerStatus.Incomplete;
            }

            if (text[position++] != expected)
            {
                return MarkerStatus.Invalid;
            }
        }

        return MarkerStatus.Complete;
    }

    private static MarkerStatus ReadDigits(ReadOnlySpan<char> text, ref int position)
    {
        if (position == text.Length)
        {
            return MarkerStatus.Incomplete;
        }

        int start = position;
        while (position < text.Length && text[position] is >= '0' and <= '9')
        {
            position++;
        }

        return position == start ? MarkerStatus.Invalid : MarkerStatus.Complete;
    }

    private enum MarkerStatus
    {
        Complete,
        Incomplete,
        Invalid
    }
}
