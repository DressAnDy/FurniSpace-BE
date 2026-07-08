using System.Security.Cryptography;

namespace FurniSpace.Application.Common.Payments;

public static class TransactionCodeGenerator
{
    private const string Prefix = "TXN";

    public static string Generate()
    {
        var suffix = RandomNumberGenerator.GetInt32(0, 100_000_000).ToString("D8");
        return $"{Prefix}{suffix}";
    }
}
