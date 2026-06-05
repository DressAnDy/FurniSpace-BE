using FurniSpace.Application.Common;
using FurniSpace.Application.DTOs;
using FurniSpace.Application.DTOs.Identity;

namespace FurniSpace.Application.Interfaces.Identity;

public interface IIdentityService
{
    Task<ServiceResult> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthResponseDto>> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ResendVerificationOtpAsync(ResendVerificationOtpRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult<CurrentUserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<CurrentUserDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<ServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
}
