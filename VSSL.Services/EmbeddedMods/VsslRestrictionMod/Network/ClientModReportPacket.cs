using ProtoBuf;

namespace VsslRestriction.Network;

[ProtoContract]
public sealed class ClientModReportPacket
{
    [ProtoMember(1)] public string PlayerUid { get; set; } = string.Empty;

    [ProtoMember(2)] public string PlayerName { get; set; } = string.Empty;

    [ProtoMember(3)] public long CreatedAtUnixSeconds { get; set; }

    [ProtoMember(4)] public List<ClientModReportEntry> Mods { get; set; } = [];
}

[ProtoContract]
public sealed class ClientModReportEntry
{
    [ProtoMember(1)] public string ModId { get; set; } = string.Empty;

    [ProtoMember(2)] public string Version { get; set; } = string.Empty;

    [ProtoMember(3)] public string NetworkVersion { get; set; } = string.Empty;

    [ProtoMember(4)] public string Side { get; set; } = string.Empty;
}
