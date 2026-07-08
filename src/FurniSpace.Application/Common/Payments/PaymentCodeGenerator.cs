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

        var maxValue = (int)Math.Pow(10, randomDigits);
        var suffix = RandomNumberGenerator.GetInt32(0, maxValue).ToString($"D{randomDigits}");
        return $"{prefix}{suffix}";
    }
}
