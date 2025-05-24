namespace NetFront.Messages;

public class HeartbeatSubMessage
{
    public static bool IsValid(List<byte[]> msg)
    {
        if (msg.Count == 1)
            return true;
        return false;
    }
}
