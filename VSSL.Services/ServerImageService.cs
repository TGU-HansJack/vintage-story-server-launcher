using VSSL.Abstractions.Services;
using VSSL.Domains.Models;

namespace VSSL.Services;

public class ServerImageService : IServerImageService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif",
        ".bmp"
    };

    public string GetImageRootPath(InstanceProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.DirectoryPath))
        {
            throw new InvalidOperationException("档案目录不能为空。");
        }

        return Path.Combine(Path.GetFullPath(profile.DirectoryPath), "OpenServerQuery");
    }

    public Task<IReadOnlyList<ServerImageFileInfo>> LoadServerImagesAsync(
        InstanceProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var root = GetImageRootPath(profile);
        var result = new List<ServerImageFileInfo>();

        if (!Directory.Exists(root))
        {
            return Task.FromResult<IReadOnlyList<ServerImageFileInfo>>(result);
        }

        var coverFiles = Directory.EnumerateFiles(root, "cover.*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImageFile)
            .OrderBy(GetCoverSortKey)
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Take(1);

        foreach (var file in coverFiles)
        {
            var info = BuildInfo(file, ServerImageKind.Cover, root);
            if (info is not null)
            {
                result.Add(info);
            }
        }

        var showcaseRoot = Path.Combine(root, "showcase");
        if (Directory.Exists(showcaseRoot))
        {
            foreach (var file in Directory.EnumerateFiles(showcaseRoot, "*.*", SearchOption.TopDirectoryOnly)
                         .Where(IsSupportedImageFile)
                         .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                var info = BuildInfo(file, ServerImageKind.Showcase, root);
                if (info is not null)
                {
                    result.Add(info);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<ServerImageFileInfo>>(result);
    }

    public async Task<ServerImageFileInfo> ImportImageAsync(
        InstanceProfile profile,
        string sourcePath,
        ServerImageKind kind,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("图片路径不能为空。");
        }

        var sourceFullPath = Path.GetFullPath(sourcePath.Trim());
        if (!File.Exists(sourceFullPath))
        {
            throw new InvalidOperationException("图片文件不存在。");
        }

        if (!IsSupportedImageFile(sourceFullPath))
        {
            throw new InvalidOperationException("仅支持 png/jpg/jpeg/webp/gif/bmp 图片。");
        }

        var root = GetImageRootPath(profile);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "showcase"));

        string destinationPath;
        if (kind == ServerImageKind.Cover)
        {
            var coverDirectory = root;
            var sourceBytes = await File.ReadAllBytesAsync(sourceFullPath, cancellationToken);
            DeleteExistingCoverFiles(coverDirectory, sourceFullPath);
            destinationPath = Path.Combine(coverDirectory, "cover" + Path.GetExtension(sourceFullPath).ToLowerInvariant());
            await File.WriteAllBytesAsync(destinationPath, sourceBytes, cancellationToken);
        }
        else
        {
            var showcaseRoot = Path.Combine(root, "showcase");
            var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(sourceFullPath));
            var ext = Path.GetExtension(sourceFullPath).ToLowerInvariant();
            destinationPath = GetUniqueShowcasePath(showcaseRoot, baseName, ext);
            await using var sourceStream = File.Open(sourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var destinationStream = File.Open(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        }

        return BuildInfo(destinationPath, kind, root)
               ?? throw new InvalidOperationException("图片导入失败。");
    }

    public Task DeleteImageAsync(
        InstanceProfile profile,
        ServerImageFileInfo image,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(image.FullPath))
        {
            return Task.CompletedTask;
        }

        var root = GetImageRootPath(profile);
        var fullPath = Path.GetFullPath(image.FullPath);
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("只能删除当前档案目录中的图片。");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private static ServerImageFileInfo? BuildInfo(string fullPath, ServerImageKind kind, string rootPath)
    {
        var file = new FileInfo(fullPath);
        if (!file.Exists || !IsSupportedImageFile(file.FullName))
        {
            return null;
        }

        return new ServerImageFileInfo
        {
            Kind = kind,
            FullPath = file.FullName,
            RelativePath = Path.GetRelativePath(rootPath, file.FullName).Replace('\\', '/'),
            FileName = file.Name,
            SizeBytes = file.Length,
            LastWriteUtc = file.LastWriteTimeUtc
        };
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        var ext = Path.GetExtension(filePath) ?? string.Empty;
        return SupportedExtensions.Contains(ext);
    }

    private static int GetCoverSortKey(string path)
    {
        var ext = Path.GetExtension(path) ?? string.Empty;
        return ext.ToLowerInvariant() switch
        {
            ".png" => 0,
            ".jpg" => 1,
            ".jpeg" => 2,
            ".webp" => 3,
            ".gif" => 4,
            ".bmp" => 5,
            _ => 100
        };
    }

    private static void DeleteExistingCoverFiles(string coverDirectory, string? preservePath = null)
    {
        foreach (var file in Directory.EnumerateFiles(coverDirectory, "cover.*", SearchOption.TopDirectoryOnly)
                     .Where(IsSupportedImageFile))
        {
            if (!string.IsNullOrWhiteSpace(preservePath) &&
                Path.GetFullPath(file).Equals(Path.GetFullPath(preservePath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Delete(file);
        }
    }

    private static string GetUniqueShowcasePath(string showcaseRoot, string baseName, string ext)
    {
        var safeBaseName = string.IsNullOrWhiteSpace(baseName) ? "showcase" : baseName;
        var candidate = Path.Combine(showcaseRoot, $"{safeBaseName}{ext}");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var i = 1; i < 10000; i++)
        {
            candidate = Path.Combine(showcaseRoot, $"{safeBaseName}-{i:000}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(showcaseRoot, $"{safeBaseName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{ext}");
    }

    private static string SanitizeFileName(string value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "showcase" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join('_', raw.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "showcase" : sanitized;
    }
}
