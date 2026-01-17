using Server.Core;
using Server.Data;

namespace Server.Game.Job;

// HandoverCompleteJob.cs
public class HandoverCompleteJob : IJob
{
    public int PlayerId { get; set; }
    public ServerConfig.ZoneInfo TargetZone { get; set; }
    public string TransferToken { get; set; }

    public void Execute()
    {
        GameRoom.Instance.FinishHandover(PlayerId, TargetZone, TransferToken);
    }
}