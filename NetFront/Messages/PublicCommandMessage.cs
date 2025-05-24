using NetFront.Frames;

namespace NetFront.Messages;

public class PublicCommandMessage
{
    private const int INDEX_OF_HEADER_FRAME = 0;
    public static bool IsValid(List<byte[]> msg, out byte commandType)
    {
        if (msg.Count > 0)
        {
            return PublicCommandHeaderFrame.TryGetCommandType(msg[INDEX_OF_HEADER_FRAME], out commandType);
        }
        commandType = (byte)PublicCommandTypeEnum.NONE;
        return false;
    }

    public static bool TryGetTopic_SPUB(Span<byte> commandHeaderFrame, Span<byte> dataFrame, out byte[] topic)
    {
        if (PublicCommandHeaderFrame.TryGetArgData_SPUB(commandHeaderFrame, out int topicLength))
        {
            if (topicLength != 0 && dataFrame.Length > topicLength)
            {
                topic = dataFrame[..topicLength].ToArray();
                return true;
            }
        }
        topic = [];
        return false;
    }


}
