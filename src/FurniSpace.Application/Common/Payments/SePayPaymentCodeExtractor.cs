using System;
using System.Text.RegularExpressions;

namespace FurniSpace.Application.Common.Payments;

public static class SePayPaymentCodeExtractor
{
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);

    public static string? Extract(string? primaryCode, string? content, string? rawBody, string paymentCodePattern)
    {
        var regex = CreateRegex(paymentCodePattern);

        if (!string.IsNullOrWhiteSpace(primaryCode))
        {
            var trimmed = primaryCode.Trim();
            if (regex.IsMatch(trimmed))
            {
                return trimmed;
            }
        }

        var fromContent = FindFirstMatch(content, regex);
        if (fromContent is not null)
        {
            return fromContent;
        }

        return FindFirstMatch(rawBody, regex);
    }

    private static Regex CreateRegex(string paymentCodePattern)
    {
        return new Regex(
            paymentCodePattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            RegexMatchTimeout);
    }

    private static string? FindFirstMatch(string? value, Regex regex)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = regex.Match(value);
        return match.Success ? match.Value : null;
    }
}
