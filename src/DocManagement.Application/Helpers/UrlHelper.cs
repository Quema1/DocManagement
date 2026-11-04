using System.Security.Cryptography;
using System.Text;
using DocManagement.Domain;
using Microsoft.Extensions.Options;
using DocManagement.Application.Options;
using DocManagement.Domain.Constants;

namespace DocManagement.Application.Helpers;

public class UrlHelper(IOptions<PreviewOptions> options)
{
    public string Generate(Document document)
    {
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/previews/{ComputeContentHash(document)}.png";
    }

    private static string ComputeContentHash(Document document)
    {
        var content = $"{document.Id}\u001F{document.Name}\u001F{document.Description}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(hash)[..PreviewConstants.ContentHashLength];
    }
}
