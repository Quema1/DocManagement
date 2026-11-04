using System.ComponentModel.DataAnnotations;
using DocManagement.Domain.Constants;

namespace DocManagement.Application.Dtos;

public class TokenRequest
{
    [Required]
    [StringLength(AuthConstants.UserNameMaxLength)]
    public required string UserName { get; init; }
}
