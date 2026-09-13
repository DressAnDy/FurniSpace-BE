using System.Globalization;
using System.Security.Cryptography;

namespace FurniSpace.Application.Common.Payments;

public static class PayOsOrderCodeGenerator
{
    public static long Generate()
    {
        var datePart = long.Parse(DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) * 10000L;
        var suffix = RandomNumberGenerator.GetInt32(0, 10_000);
        return datePart + suffix;
    }
}
