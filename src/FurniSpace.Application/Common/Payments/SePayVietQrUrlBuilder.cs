using System.Globalization;
using FurniSpace.Domain.Entities;
using Microsoft.Extensions.Options;

namespace FurniSpace.Application.Common.Payments;

public sealed class SePayVietQrUrlBuilder
{
    private readonly SePayOptions _options;

    public SePayVietQrUrlBuilder(IOptions<SePayOptions> options)
    {
        _options = options.Value;
    }

    public string Build(Payment payment)
    {
        var query = string.Join(
            "&",
            QueryPair("acc", _options.BankAccountNo),
            QueryPair("bank", _options.BankCode),
            QueryPair("amount", FormatAmount(payment.Amount)),
            QueryPair("des", payment.PaymentCode),
            QueryPair("template", _options.VietQrTemplate),
            QueryPair("showinfo", _options.VietQrShowInfo.ToString().ToLowerInvariant()),
            QueryPair("holder", _options.BankAccountName),
            QueryPair("store", _options.VietQrStoreName));

        var baseUrl = _options.VietQrBaseUrl.TrimEnd('/');
        return $"{baseUrl}?{query}";
    }

    private static string QueryPair(string key, string value)
    {
        return $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }

    private static string FormatAmount(decimal amount)
    {
        return decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture);
    }
}
