namespace NetFront.ClientApi;

enum ConnectionStatusEnum : byte
{
    INIT = 0,
    IN_PROCESS = 1,
    OK = 2
}

public enum LoginStatusEnum : byte
{
    INIT = 0,
    READY = 1,
    IN_PROCESS = 2,
    OK = 3
}

enum ClientApiFunctionEnum : byte
{
    LOGIN = 1,
    LOGOUT = 2,
    SUB_PUBLIC = 3,
    SUB_PRIVATE = 5,
    GET_PUBLIC = 6,
    UNSUB_PUBLIC = 8,
    UNSUB_PRIVATE = 9,
    REQ_ROUTE = 10,
    DESTROY = 11,
    DISCONNECT = 99
}
