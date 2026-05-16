using Vintagestory.API.Common;
using VsslRestriction.Network;

namespace VsslRestriction;

public sealed class VsslRestrictionModSystem : ModSystem
{
    public const string ChannelName = "vsslrestriction.report";
    public const string ReportLogPrefix = "[VSSL-RESTRICTION-REPORT]";

    public override void Start(ICoreAPI api)
    {
        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<ClientModReportPacket>();
    }
}
