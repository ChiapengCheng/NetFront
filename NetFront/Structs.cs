namespace NetFront;

public enum MessageTypeEnum : byte
{
    FRONT_UNSUB = 0,
    USER_UNSUB = 45,
    WELCOME = 33,
    HEARTBEAT = 34,
    HEARTBEAT_SUB = 35,
    USER_REQ = 36,
    USER_RSP = 37,
    ROUTE = 38,
    PUBLIC_COMMAND = 40,
    PUBLIC = 42,
    PRIVATE = 43,
    NONE = 255
}

public struct FrontSettings
{
    public string SYSTEM_ID { get; set; }
    public int HEARTBEAT_INTERVAL { get; set; }
    public string CLIENT_ADDRESS { get; set; }
    public string SYSTEM_ADDRESS { get; set; }
    public string SHOW_CLINET_ADDRESS { get; set; }
    public string SHOW_SYSTEM_ADDRESS { get; set; }
    public UserField[] USERS { get; set; }
}

public struct UserField
{
    public string UserID { get; set; }
    public string Password { get; set; }
    public byte UserStatus { get; set; }
    public byte UserRole { get; set; }
    public int ConnectionLimit { get; set; }
}

public struct HeartbeatMessageFiled
{
    public byte MsgType { get; set; }
    public string SystemID { get; set; }
    public long Ticks { get; set; }
}

public struct RspInfoFrameField
{
    public long Time;
    public int ErrorCode;
    public string ErrorMsg;
    public bool IsLast;
}

public enum ErrorCodeEnum : int
{
    //GW
    OK = 0,
    //REQ_DATA_ERROR = 1,
    USER_ID_NOT_EXISTS = 2,
    WRONG_PASSWORD = 3,
    STATUS_CHECK_FAIL = 4,
    SESSION_EXISTS = 5,
    OVER_CONNECTION_LIMIT = 6,
    NO_CACHE_DATA = 7,
    SESSION_NOT_EXISTS = 8,
    USER_ROLE_CHECK_FAIL = 9,
    SET_USER_ERROR = 10,
    //API
    DISCONNECT_FROM_EVENT_PRX = 1000,
    CONNECTION_STATUS_ERROR = 1001,
    LOGIN_STATUS_ERROR = 1002,
    //REQUEST_DATA_ERROR = 1003,
    TOPIC_CHECK_ERROR = 1004,
    //ROUTE_DATA_ERROR = 1005,
    FUNCTION_ARGS_ERROR = 1006,
    //WRAPPTER
    //NOT_LOGIN = -1,
    //NOT_READY = -2,
    //NOT_MATCH = -3
}

public enum UserRequestFunctionEnum : byte
{
    LOGIN = 0,
    LOGOUT = 1,
    SUB_PUBLIC = 2,
    SUB_PRIVATE = 3,
    SUB_ROUTE = 4,
    GET_PUBLIC = 5,
    REQ_ROUTE = 6,
    RSP_ROUTE = 7,
    RTN_PRIVATE = 8,
    SET_USER = 10,
    NONE = 255
}

public enum UserStatusEnum : byte
{
    INACTIVE = 0,
    ACTIVE = 1
}

public struct WelcomeMessageField
{
    public byte MsgType { get; set; }
    public string SystemID { get; set; }
    public string Address { get; set; }
}

public struct ChannelStatisticsField
{
    public long RcvMsgCount { get; set; }
    public long SendMsgCount { get; set; }
    public long RcvMsgBytes { get; set; }
    public long SendMsgBytes { get; set; }
}

public struct UserResponseHeaderFrameField
{
    public byte MsgType { get; set; }
    public string UserID { get; set; }
    public string SessionID { get; set; }
    public int RequestID { get; set; }
    public byte FunctionID { get; set; }
}

public enum UserRoleEnum : byte
{
    CLINET = 0,
    ADMIN = 1,
    SYSTEM = 2
}

public struct PublicCommandHeaderFrameField
{
    public byte MsgType { get; set; }
    public byte CommandType { get; set; }
    public byte[] ArgData { get; set; }
}

public enum PublicCommandTypeEnum : byte
{
    PUB = 10,
    SPUB = 11,
    //RPUB = 12,
    //APUB = 13,
    SET = 14,
    //APPEND = 15,
    //SETRANGE = 16,
    NONE = 255
}

public struct ReqUserLoginField
{
    public string Password { get; set; }
    public string Data { get; set; }
}

public struct UserRequestHeaderFrameField
{
    public byte MsgType { get; set; }
    public string UserID { get; set; }
    public string SessionID { get; set; }
    public int RequestID { get; set; }
    public byte FunctionID { get; set; }
}

public struct RouteHeaderFrameField
{
    public byte MsgType { get; set; }
    public int FunctionCode { get; set; }
    public string UserID { get; set; }
    public byte[] MsgData { get; set; }
}

public struct PrivateMessageField
{
    public byte MsgType { get; set; }
    public int FunctionCode { get; set; }
    public string UserID { get; set; }
    public byte[] MsgData { get; set; }
}