namespace FurniSpace.Application.Common.Identity;

public static class PasswordPolicy
{
    public static string? Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
        {
            return "Password must be between 8 and 128 characters.";
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            return "Password must contain uppercase, lowercase, and numeric characters.";
        }

        return null;
    }
}
