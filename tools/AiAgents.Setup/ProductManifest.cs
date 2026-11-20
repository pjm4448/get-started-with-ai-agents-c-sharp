using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAgents.Setup;

public sealed record ProductDocument(string Name, string Sha256);

public sealed record ProductManifest(int SchemaVersion, ProductDocument[] Files)
{
    internal static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static async Task<VerifiedProducts> LoadAsync(string path, CancellationToken cancellationToken)
    {
        byte[] manifestBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var manifest = JsonSerializer.Deserialize<ProductManifest>(manifestBytes, JsonOptions);
        if (manifest is not { SchemaVersion: 1, Files.Length: > 0 and <= 20 })
        {
            throw new InvalidDataException("A version 1 product manifest with 1 to 20 files is required.");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        List<VerifiedProduct> products = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.Name) ||
                file.Name.Length > 128 ||
                !file.Name.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.') ||
                file.Name is "." or ".." || !names.Add(file.Name) ||
                Path.GetExtension(file.Name) is not (".md" or ".pdf") ||
                file.Sha256 is not { Length: 64 } || !file.Sha256.All(char.IsAsciiHexDigit))
            {
                throw new InvalidDataException("Manifest filenames must be unique Markdown/PDF leaf names with SHA-256 hashes.");
            }

            var info = new FileInfo(Path.Combine(directory, file.Name));
            if (!info.Exists || info.Length is <= 0 or > 1_048_576 ||
                info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException($"The approved file {file.Name} must be a regular file of 1 byte to 1 MiB.");
            }

            byte[] content = await File.ReadAllBytesAsync(info.FullName, cancellationToken);
            if (!Convert.ToHexString(SHA256.HashData(content)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Hash mismatch for approved file {file.Name}. Review the change before updating its manifest.");
            }

            products.Add(new VerifiedProduct(file.Name, content));
        }

        byte[] canonicalManifest = JsonSerializer.SerializeToUtf8Bytes(manifest);
        return new VerifiedProducts(Convert.ToHexString(SHA256.HashData(canonicalManifest)), products);
    }
}

public sealed record VerifiedProduct(string Name, byte[] Content);

public sealed record VerifiedProducts(string ManifestHash, IReadOnlyList<VerifiedProduct> Files);
