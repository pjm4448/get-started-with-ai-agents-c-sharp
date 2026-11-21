using System.Security.Cryptography;

namespace AiAgents.Setup;

public static class SearchCorpus
{
    public static async Task<VerifiedProducts> LoadAsync(
        string markdownManifest, string pdfManifest, CancellationToken cancellationToken)
    {
        var markdown = await ProductManifest.LoadAsync(markdownManifest, cancellationToken);
        var pdf = await ProductManifest.LoadAsync(pdfManifest, cancellationToken);
        if (markdown.Files.Any(file => !file.Name.EndsWith(".md", StringComparison.Ordinal)) ||
            pdf.Files.Any(file => !file.Name.EndsWith(".pdf", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The Search corpus requires separate Markdown and PDF manifests.");
        }

        var files = markdown.Files.Concat(pdf.Files).ToArray();
        if (files.Select(file => file.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Length)
        {
            throw new InvalidDataException("Search corpus filenames must be unique across both manifests.");
        }

        var manifestHashes = Convert.FromHexString(markdown.ManifestHash)
            .Concat(Convert.FromHexString(pdf.ManifestHash)).ToArray();
        return new VerifiedProducts(Convert.ToHexString(SHA256.HashData(manifestHashes)), files);
    }
}
