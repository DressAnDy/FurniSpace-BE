#nullable enable

using System.Security.Claims;
using FurniSpace.API.Base;
using FurniSpace.Application.DTOs.ProjectReviews;
using FurniSpace.Application.Interfaces.ProjectReviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurniSpace.API.Controllers.Projects;

[Authorize]
[Route("project-reviews/{reviewId:guid}/public-consent")]
public sealed class ProjectReviewPublicConsentController : BaseApiController
{
    private readonly IProjectReviewConsentService _reviewConsent;

    public ProjectReviewPublicConsentController(IProjectReviewConsentService reviewConsent)
    {
        _reviewConsent = reviewConsent;
    }

    [Authorize(Roles = "CUSTOMER")]
    [HttpPatch]
    public async Task<IActionResult> Update(
        Guid reviewId,
        [FromBody] UpdateProjectReviewPublicConsentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var result = await _reviewConsent.UpdatePublicConsentAsync(reviewId, currentUserId, request, cancellationToken);
        return ToActionResult(result);
    }

    private bool TryGetCurrentUserId(out Guid currentUserId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out currentUserId);
    }
}
