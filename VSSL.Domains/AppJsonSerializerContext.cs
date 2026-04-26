using System.Text.Json.Serialization;
using VSSL.Common.Converters;
using VSSL.Domains.Enums;
using VSSL.Domains.Models;

namespace VSSL.Domains;

[JsonSourceGenerationOptions(WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    Converters =
    [
        typeof(JsonDateTimeOffsetConverter),
        // Define converter for each enums
        typeof(JsonStringEnumConverter<ViewName>)
    ],
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(MenuItem))]
[JsonSerializable(typeof(List<MenuItem>))]
[JsonSerializable(typeof(ActivityLog))]
[JsonSerializable(typeof(List<ActivityLog>))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
