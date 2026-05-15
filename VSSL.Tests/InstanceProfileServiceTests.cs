using System.Reflection;
using System.Text.Json;
using VSSL.Domains.Models;
using VSSL.Services;

namespace VSSL.Tests;

public class InstanceProfileServiceTests
{
    [Fact]
    public void GetProfiles_Should_DeduplicateProfilesById_AndPreferNonRecoveredName()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "vssl-tests",
            $"profile-dedup-{Guid.NewGuid():N}");

        try
        {
            SetWorkspaceRoot(workspaceRoot);
            EnsureWorkspace();

            var profileId = "dup-profile-id";
            var profileDirectory = Path.Combine(workspaceRoot, "data", profileId);
            Directory.CreateDirectory(profileDirectory);

            var oldRecord = new InstanceProfile
            {
                Id = profileId,
                Name = "Recovered-abcdef",
                Version = "1.22.2",
                DirectoryPath = profileDirectory,
                ActiveSaveFile = Path.Combine(profileDirectory, "Saves", "default.vcdbs"),
                SaveDirectory = Path.Combine(profileDirectory, "Saves"),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastUpdatedUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            };

            var preferredRecord = new InstanceProfile
            {
                Id = profileId,
                Name = "MyServer",
                Version = "1.22.2",
                DirectoryPath = profileDirectory,
                ActiveSaveFile = Path.Combine(profileDirectory, "Saves", "default.vcdbs"),
                SaveDirectory = Path.Combine(profileDirectory, "Saves"),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                LastUpdatedUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
            };

            var index = new InstanceProfileIndex
            {
                Profiles = new List<InstanceProfile>
                {
                    oldRecord,
                    preferredRecord
                }
            };

            var indexPath = Path.Combine(workspaceRoot, "profiles.json");
            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(indexPath, json);

            var service = new InstanceProfileService();
            var profiles = service.GetProfiles();

            Assert.Single(profiles);
            Assert.Equal(profileId, profiles[0].Id);
            Assert.Equal("MyServer", profiles[0].Name);

            var afterJson = File.ReadAllText(indexPath);
            var afterIndex = JsonSerializer.Deserialize<InstanceProfileIndex>(afterJson);
            Assert.NotNull(afterIndex);
            Assert.Single(
                afterIndex!.Profiles,
                x => x.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            SetWorkspaceRoot(null);

            try
            {
                if (Directory.Exists(workspaceRoot))
                    Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    [Fact]
    public void GetProfiles_Should_NotCreateRecoveredProfile_WhenDirectoryAlreadyBoundToLegacyId()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "vssl-tests",
            $"profile-recover-boundary-{Guid.NewGuid():N}");

        try
        {
            SetWorkspaceRoot(workspaceRoot);
            EnsureWorkspace();

            var profileIdFromDirectory = "ceee05e6efc54f1d9a81a11111111111";
            var profileDirectory = Path.Combine(workspaceRoot, "data", profileIdFromDirectory);
            Directory.CreateDirectory(profileDirectory);

            var legacyRecord = new InstanceProfile
            {
                Id = "legacy-random-id",
                Name = "MyServer",
                Version = "1.22.2",
                DirectoryPath = profileDirectory,
                ActiveSaveFile = Path.Combine(profileDirectory, "Saves", "default.vcdbs"),
                SaveDirectory = Path.Combine(profileDirectory, "Saves"),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastUpdatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
            };

            var index = new InstanceProfileIndex
            {
                Profiles = new List<InstanceProfile> { legacyRecord }
            };

            var indexPath = Path.Combine(workspaceRoot, "profiles.json");
            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(indexPath, json);

            var service = new InstanceProfileService();
            var profiles = service.GetProfiles();

            Assert.Single(profiles);
            Assert.Equal(profileIdFromDirectory, profiles[0].Id);
            Assert.Equal("MyServer", profiles[0].Name);
            Assert.DoesNotContain(
                profiles,
                profile => profile.Name.StartsWith("Recovered-", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            SetWorkspaceRoot(null);

            try
            {
                if (Directory.Exists(workspaceRoot))
                    Directory.Delete(workspaceRoot, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static void SetWorkspaceRoot(string? workspaceRoot)
    {
        var helperType = typeof(InstanceProfileService).Assembly.GetType("VSSL.Services.WorkspacePathHelper")
                         ?? throw new InvalidOperationException("WorkspacePathHelper type not found.");
        var setRootMethod = helperType.GetMethod(
            "SetWorkspaceRoot",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                           ?? throw new InvalidOperationException("SetWorkspaceRoot method not found.");
        setRootMethod.Invoke(null, new object?[] { workspaceRoot });
    }

    private static void EnsureWorkspace()
    {
        var helperType = typeof(InstanceProfileService).Assembly.GetType("VSSL.Services.WorkspacePathHelper")
                         ?? throw new InvalidOperationException("WorkspacePathHelper type not found.");
        var ensureMethod = helperType.GetMethod(
            "EnsureWorkspace",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                          ?? throw new InvalidOperationException("EnsureWorkspace method not found.");
        ensureMethod.Invoke(null, null);
    }
}
