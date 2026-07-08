using FurniSpace.Application.Interfaces.Identity;

namespace FurniSpace.Application.Common.Identity;

public sealed record IdentityVerificationStores(
    IPasswordResetStore PasswordResetStore,
    IEmailOtpStore EmailOtpStore,
    IRefreshTokenStore RefreshTokens);
