using DocManagement.Application.Dtos;
using DocManagement.Application.Services;
using DocManagement.Domain;
using DocManagement.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocManagement.Api.Controllers;

[ApiController]
[Route("api/documents/{id:guid}/preview")]
public class PreviewsController(PreviewService previewService) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitingConstants.PreviewsPolicy)]
    public async Task<ActionResult<PreviewResponse>> Create(Guid id, CancellationToken cancellationToken)
    {
        var preview = await previewService.CreateAsync(id, cancellationToken);
        if (preview is null)
        {
            return NotFound();
        }

        return preview.Status == PreviewStatus.Ready
            ? Ok(preview)
            : AcceptedAtAction(nameof(Get), new { id }, preview);
    }

    [HttpGet]
    public async Task<ActionResult<PreviewResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var preview = await previewService.GetAsync(id, cancellationToken);
        return preview is null ? NotFound() : Ok(preview);
    }
}
