using VSSL.Common.Helpers;
using VSSL.Domains.Models;

namespace VSSL.Tests;

/// <summary>
/// </summary>
public class JsonHelperTests
{
    [Fact]
    public void TestDeserialize()
    {
        var jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "activity-logs.json");
        var jsonStr = File.ReadAllText(jsonFilePath);
        var activityLogs = JsonHelper.Deserialize<List<ActivityLog>>(jsonStr) ?? [];
        Assert.NotEmpty(activityLogs);
    }
}
