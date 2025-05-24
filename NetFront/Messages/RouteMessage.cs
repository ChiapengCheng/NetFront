using NetFront.Frames;

namespace NetFront.Messages;

public class RouteMessage
{
    private const int TOTAL_FRAME_COUNT = 2;
    private const int INDEX_OF_ROUTE_HEADER_FRAME = 0;
    private const int INDEX_OF_USER_RSP_HEADER_FRAME = 1;

    public static bool IsValid(List<byte[]> msg, out RouteHeaderFrameField routeHeader, out UserResponseHeaderFrameField rspHeader)
    {
        if (msg.Count == TOTAL_FRAME_COUNT)
        {
            if (RouteHeaderFrame.TryParse(msg[INDEX_OF_ROUTE_HEADER_FRAME], out routeHeader))
                if (UserResponseHeaderFrame.TryParse(msg[INDEX_OF_USER_RSP_HEADER_FRAME], out rspHeader))
                    return true;
        }
        routeHeader = new RouteHeaderFrameField();
        rspHeader = new UserResponseHeaderFrameField();
        return false;
    }
}
