namespace NetFront.Messages;

public class UserResponseMessage
{
    private const int TOTAL_FRAME_COUNT = 3;
    //private const int INDEX_OF_RSP_HEADER_FRAME = 0;
    //private const int INDEX_OF_RSP_INFO_FRAME = 1;
    //private const int INDEX_OF_RSP_DATA_FRAME = 2;



    public static bool IsValid(List<byte[]> msg)
    {
        if (msg.Count == TOTAL_FRAME_COUNT)
            return true;
        return false;
    }
}
