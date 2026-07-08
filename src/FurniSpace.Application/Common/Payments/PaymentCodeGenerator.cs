using System.Security.Cryptography;

namespace FurniSpace.Application.Common.Payments;

public static class PaymentCodeGenerator
{
    public static string Generate(string prefix, int randomDigits)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Payment code prefix is required.", nameof(prefix));
        }

        if (randomDigits is < 8 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(randomDigits), "Random digits must be between 8 and 10.");
        }

        Span<char> suffix = stackalloc char[randomDigits];
        for (var i = 0; i < randomDigits; i++)
        {
            suffix[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return string.Concat(prefix, new string(suffix));
    }
}
