namespace NetFront.SystemApi;

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

enum SystemApiFunctionEnum : byte
{
    LOGIN = 1,
    LOGOUT = 2,
    RTN_PRIVATE = 8,
    SUB_ROUTE = 9,
    RSP_ROUTE = 10,
    DESTROY = 11,
    SET_USER = 20,
    DISCONNECT = 99
}
