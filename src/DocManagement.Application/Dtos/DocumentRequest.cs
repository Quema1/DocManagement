using System.ComponentModel.DataAnnotations;
using DocManagement.Domain.Constants;

namespace DocManagement.Application.Dtos;

public class DocumentRequest
{
	[Required]
	[StringLength(DocumentConstants.NameMaxLength)]
	public required string Name { get; init; }

	[StringLength(DocumentConstants.DescriptionMaxLength)]
    public string? Description { get; init; }
}
