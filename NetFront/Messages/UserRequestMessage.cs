namespace NetFront.Messages;

public class UserRequestMessage
{
    public static bool IsValid(List<byte[]> msg)
    {
        if (msg.Count >= 2)
            return true;
        return false;
    }
}
