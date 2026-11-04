using DocManagement.Application.Dtos;
using DocManagement.Application.Services;
using Microsoft.AspNetCore.Mvc;
using DocManagement.Domain.Constants;

namespace DocManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController(DocumentService documentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> GetAll(
        [FromQuery] int page = PaginationConstants.DefaultPage,
        [FromQuery] int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        return Ok(await documentService.GetListAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var document = await documentService.GetByIdAsync(id, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpPost]
    public async Task<ActionResult<DocumentResponse>> Create(DocumentRequest request, CancellationToken cancellationToken)
    {
        return await documentService.CreateAsync(request, cancellationToken);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DocumentResponse>> Update(Guid id, DocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await documentService.UpdateAsync(id, request, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await documentService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
