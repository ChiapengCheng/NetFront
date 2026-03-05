using NetFront.Frames;
using NetFront.Messages;
using NetFront.Transport;
using NetFront.UserManagement;

namespace NetFront.SystemApi;

public class SystemApiContext : IDisposable
{
    private readonly string INTERNAL_PROCESS_ADDRESS = $"inproc://{Guid.NewGuid():N}";
    private readonly string INTERNAL_PUBLIC_ADDRESS = $"inproc://{Guid.NewGuid():N}";
    private const int HEARTBEAT_CHECK_TIMER_INTERVAL = 30000;
    private const int SYSTEM_WAIT_INTERVAL = 500;
    private ConnectionStatusEnum _conStatus;
    private LoginStatusEnum _loginStatus;
    private InprocChannel _process = default!;
    private InprocChannel _publicMain = default!;
    private InprocChannel _publicSub = default!;
    private int _requestId;
    private CancellationTokenSource _runCts = new();

    public LoginStatusEnum LoginStatus
    {
        get { return _loginStatus; }
    }

    public SystemApiContext()
    {
        Init();
    }

    ~SystemApiContext()
    {
        this.Dispose();
    }

    public void Dispose()
    {
        _runCts.Cancel();
        GC.SuppressFinalize(this);
    }

    private void RunCore(string address)
    {
        Task.Factory.StartNew(async () =>
        {
            var ct = _runCts.Token;
            using var publicSocket = new InprocChannel();
            using var processSocket = new InprocChannel();
            using var systemSocket = new TcpSubClient();
            var systemChannel = new SystemChannel(systemSocket, address, 0, 0);
            var processChannel = new ProcesslChannel(processSocket, INTERNAL_PROCESS_ADDRESS, 0, 0);
            var publicChannel = new PublicChannel(publicSocket, INTERNAL_PUBLIC_ADDRESS, 0, 0);

            var hbTimeFlag = 0L;
            var checkTimeFlag = 0L;
            var header = new UserRequestHeaderFrame();
            var userTopics = new SortedSet<string>();

            List<byte[]> sysMsg = [];
            systemSocket.ReceiveReady += () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    if (systemSocket.TryReceiveMultipartBytes(ref sysMsg))
                    {
                        if (systemChannel.TryGetMsgType(sysMsg, out var type))
                        {
                            switch (type)
                            {
                                case (byte)MessageTypeEnum.USER_RSP:
                                    if (UserResponseMessage.IsValid(sysMsg))
                                    {
                                        if (UserResponseHeaderFrame.TryParse(sysMsg[0], out UserResponseHeaderFrameField rspHeader))
                                        {
                                            if (RspInfoFrame.TryParse(sysMsg[1], out RspInfoFrameField rspInfo))
                                            {
                                                switch (rspHeader.FunctionID)
                                                {
                                                    case (byte)UserRequestFunctionEnum.LOGIN:
                                                        ProcessRspLogin(sysMsg, rspInfo);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.LOGOUT:
                                                        ProcessRspLogout(systemChannel, rspInfo, userTopics);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.SET_USER:
                                                        ProcessRspSetUser(sysMsg, rspInfo, rspHeader.RequestID);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.RTN_PRIVATE:
                                                        ProcessRspRtnPrivate(sysMsg, rspInfo, rspHeader.RequestID);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.SUB_ROUTE:
                                                        ProcessRspSubRoute(sysMsg, rspInfo, rspHeader.RequestID);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.RSP_ROUTE:
                                                        ProcessRspRspRoute(sysMsg, rspInfo, rspHeader.RequestID);
                                                        break;
                                                    default:
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case (byte)MessageTypeEnum.WELCOME:
                                    if (WelcomeMessage.TryParse(sysMsg[0], out WelcomeMessageField wc))
                                    {
                                        switch (_conStatus)
                                        {
                                            case ConnectionStatusEnum.IN_PROCESS:
                                                SetConnStatus(ConnectionStatusEnum.OK);
                                                systemChannel.SendHeartbeatSubMessage();
                                                Task.Factory.StartNew(() =>
                                                {
                                                    OnFrontConnected(wc);
                                                });
                                                break;
                                        }
                                    }
                                    break;
                                case (byte)MessageTypeEnum.HEARTBEAT:
                                    hbTimeFlag++;
                                    if (HeartbeatMessage.TryParse(sysMsg[0], out HeartbeatMessageFiled hb))
                                        OnHeartbeatReceived(hb);
                                    break;
                                case (byte)MessageTypeEnum.ROUTE:
                                    if (RouteMessage.IsValid(sysMsg, out RouteHeaderFrameField routeHeader, out UserResponseHeaderFrameField userRspHeader))
                                        OnRtnRoute(routeHeader, userRspHeader, sysMsg[1]);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            };

            List<byte[]> procMsg = [];
            processSocket.ReceiveReady += () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    if (processSocket.TryReceiveMultipartBytes(ref procMsg))
                    {
                        if (procMsg.Count > 0 && procMsg[0].Length > 0)
                        {
                            switch (procMsg[0][0])
                            {
                                case (byte)SystemApiFunctionEnum.LOGIN:
                                    ReqLogin(systemChannel, header, procMsg, userTopics);
                                    break;
                                case (byte)SystemApiFunctionEnum.LOGOUT:
                                    ReqLogout(systemChannel, header, procMsg);
                                    break;
                                case (byte)SystemApiFunctionEnum.SET_USER:
                                    ReqSetUser(systemChannel, header, procMsg);
                                    break;
                                case (byte)SystemApiFunctionEnum.RTN_PRIVATE:
                                    ReqRtnPrivate(systemChannel, header, procMsg);
                                    break;
                                case (byte)SystemApiFunctionEnum.SUB_ROUTE:
                                    ReqSubRoute(systemChannel, header, procMsg);
                                    break;
                                case (byte)SystemApiFunctionEnum.RSP_ROUTE:
                                    ReqRspRoute(systemChannel, header, procMsg);
                                    break;
                                case (byte)SystemApiFunctionEnum.DESTROY:
                                    ReqDestroy(systemChannel, header);
                                    break;
                                case (byte)SystemApiFunctionEnum.DISCONNECT:
                                    _runCts.Cancel();
                                    Init();
                                    OnFrontDisconnected((int)ErrorCodeEnum.DISCONNECT_FROM_EVENT_PRX);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            };

            List<byte[]> pubMsg = [];
            publicSocket.ReceiveReady += () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    if (publicSocket.TryReceiveMultipartBytes(ref pubMsg))
                    {
                        systemSocket.SendMultipart(pubMsg);
                    }
                }
            };

            _ = Task.Run(async () =>
            {
                var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(HEARTBEAT_CHECK_TIMER_INTERVAL));
                while (await timer.WaitForNextTickAsync(ct))
                {
                    switch (_conStatus)
                    {
                        case ConnectionStatusEnum.OK:
                            if (hbTimeFlag != 0)
                            {
                                if (checkTimeFlag == hbTimeFlag)
                                {
                                    _runCts.Cancel();
                                    Init();
                                    OnFrontDisconnected((int)ErrorCodeEnum.DISCONNECT_FROM_EVENT_PRX);
                                }
                                else
                                    checkTimeFlag = hbTimeFlag;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }, ct);

            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        });
    }

    #region Status Control
    private void Init()
    {
        _runCts = new CancellationTokenSource();
        SetConnStatus(ConnectionStatusEnum.INIT);
    }

    private void SetConnStatus(ConnectionStatusEnum status)
    {
        _conStatus = status;
        switch (status)
        {
            case ConnectionStatusEnum.INIT:
            case ConnectionStatusEnum.IN_PROCESS:
                SetLoginStatus(LoginStatusEnum.INIT);
                break;
            case ConnectionStatusEnum.OK:
                SetLoginStatus(LoginStatusEnum.READY);
                break;
            default:
                break;
        }
    }

    private void SetLoginStatus(LoginStatusEnum status)
    {
        _loginStatus = status;
        switch (status)
        {
            case LoginStatusEnum.READY:
            case LoginStatusEnum.INIT:
            case LoginStatusEnum.IN_PROCESS:
            case LoginStatusEnum.OK:
            default:
                break;
        }
    }
    #endregion

    #region Public Functions
    public int Connect(string ip, int port)
    {
        var address = $"tcp://{ip}:{port}";
        return Connect(address);
    }

    public int Connect(string address)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.INIT:
                if (!string.IsNullOrEmpty(address))
                {
                    SetConnStatus(ConnectionStatusEnum.IN_PROCESS);
                    RunCore(address);
                    Thread.Sleep(SYSTEM_WAIT_INTERVAL);
                    _process = new InprocChannel();
                    _process.Connect(INTERNAL_PROCESS_ADDRESS);
                    _publicMain = new InprocChannel();
                    _publicMain.Connect(INTERNAL_PUBLIC_ADDRESS);
                    _publicSub = new InprocChannel();
                    _publicSub.Connect(INTERNAL_PUBLIC_ADDRESS);
                    return (int)ErrorCodeEnum.OK;
                }
                else
                    return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int Disconnect()
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                _process.SendFrame([(byte)SystemApiFunctionEnum.DISCONNECT]);
                return (int)ErrorCodeEnum.OK;
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int Login(string userID, string password)
    {
        if (Global.USER_ID.Check(userID) && Global.PASSWORD.Check(password))
        {
            switch (_conStatus)
            {
                case ConnectionStatusEnum.OK:
                    switch (_loginStatus)
                    {
                        case LoginStatusEnum.READY:
                            SetLoginStatus(LoginStatusEnum.IN_PROCESS);
                            Thread.Sleep(SYSTEM_WAIT_INTERVAL);
                            _requestId = 0;
                            _process.SendMultipart(
                                [(byte)SystemApiFunctionEnum.LOGIN],
                                Global.REQUEST_ID.ToBytes(_requestId),
                                Global.USER_ID.ToBytes(userID),
                                Global.PASSWORD.ToBytes(password));
                            return (int)ErrorCodeEnum.OK;
                        default:
                            return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                    }
                default:
                    return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
            }
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    public int Logout()
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        _process.SendMultipart(
                            [(byte)SystemApiFunctionEnum.LOGOUT],
                            Global.REQUEST_ID.ToBytes(++_requestId));
                        return (int)ErrorCodeEnum.OK;
                    default:
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int Destroy()
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        _process.SendFrame([(byte)SystemApiFunctionEnum.DESTROY]);
                        return (int)ErrorCodeEnum.OK;
                    default:
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int SetUser(string userID, string password, UserStatusEnum status, UserRoleEnum role, int connectionLimit)
    {
        if (Global.USER_ID.Check(userID) && Global.PASSWORD.Check(password))
        {
            switch (_conStatus)
            {
                case ConnectionStatusEnum.OK:
                    switch (_loginStatus)
                    {
                        case LoginStatusEnum.OK:
                            _process.SendMultipart(
                                [(byte)SystemApiFunctionEnum.SET_USER],
                                Global.REQUEST_ID.ToBytes(++_requestId),
                                User.GetBytes(new UserField()
                                {
                                    UserID = userID,
                                    Password = password,
                                    UserStatus = (byte)status,
                                    UserRole = (byte)role,
                                    ConnectionLimit = connectionLimit
                                }));
                            return (int)ErrorCodeEnum.OK;
                        default:
                            return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                    }
                default:
                    return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
            }
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    public int RtnPrivate(int functionCode, string userID, string data)
    {
        if (Global.USER_ID.Check(userID))
        {
            switch (_conStatus)
            {
                case ConnectionStatusEnum.OK:
                    switch (_loginStatus)
                    {
                        case LoginStatusEnum.OK:
                            _process.SendMultipart(
                                [(byte)SystemApiFunctionEnum.RTN_PRIVATE],
                                Global.REQUEST_ID.ToBytes(++_requestId),
                                PrivateMessage.GetBytes(functionCode, userID, data));
                            return (int)ErrorCodeEnum.OK;
                        default:
                            return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                    }
                default:
                    return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
            }
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    public int SubRoute(int functionCode)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        _process.SendMultipart(
                            [(byte)SystemApiFunctionEnum.SUB_ROUTE],
                            Global.REQUEST_ID.ToBytes(++_requestId),
                            Global.FUNCTION_CODE.ToBytes(functionCode));
                        return (int)ErrorCodeEnum.OK;
                    default:
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int RspRoute(byte[] rspHeader, byte[] rspData, long time, int errorCode, string errorMSg, bool isLast)
    {
        if (RspInfoFrame.CheckErrorMsg(errorMSg))
        {
            switch (_conStatus)
            {
                case ConnectionStatusEnum.OK:
                    switch (_loginStatus)
                    {
                        case LoginStatusEnum.OK:
                            _process.SendMultipart(new List<byte[]>
                            {
                                new byte[] { (byte)SystemApiFunctionEnum.RSP_ROUTE },
                                Global.REQUEST_ID.ToBytes(++_requestId),
                                rspHeader,
                                RspInfoFrame.GetBytes(time, errorCode, errorMSg, isLast),
                                rspData
                            });
                            return (int)ErrorCodeEnum.OK;
                        default:
                            return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                    }
                default:
                    return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
            }
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    public int SET(string key, byte[] value)
    {
        if (PublicCommandHeaderFrame.TryGetBytes_SET(key, out var comm))
        {
            return PUB_COMMAND(comm, value);
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    public int PUB(byte[] data)
    {
        if (PublicCommandHeaderFrame.TryGetBytes_PUB(out var comm))
        {
            return PUB_COMMAND(comm, data);
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    public int SPUB(byte topicLength, byte[] data)
    {
        if (PublicCommandHeaderFrame.TryGetBytes_SPUB(topicLength, data, out var comm))
        {
            return PUB_COMMAND(comm, data);
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    private int PUB_COMMAND(byte[] commFrame, byte[] dataFrame)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                _publicMain.SendMultipart(commFrame, dataFrame);
                return (int)ErrorCodeEnum.OK;
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }
    #endregion

    #region Response Processes
    private void ProcessRspLogin(List<byte[]> msg, RspInfoFrameField rspInfo)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.IN_PROCESS)
        {
            if (rspInfo.ErrorCode == 0)
            {
                SetLoginStatus(LoginStatusEnum.OK);
            }
            else
                SetLoginStatus(LoginStatusEnum.READY);
            OnRspUserLogin(rspInfo, msg[2]);
        }
    }

    private void ProcessRspLogout(SystemChannel systemChannel, RspInfoFrameField rspInfo, SortedSet<string> userTopics)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
        {
            if (rspInfo.ErrorCode == 0)
            {
                SetLoginStatus(LoginStatusEnum.READY);
                var topics = userTopics.ToList();
                foreach (var topic in topics)
                    systemChannel.SendUserUnsubMessage(UserUnsubMessage.GetBytes(topic));
                userTopics.Clear();
            }
            OnRspUserLogout(rspInfo);
        }
    }

    private void ProcessRspSetUser(List<byte[]> msg, RspInfoFrameField rspInfo, int requestID)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
            OnRspSetUser(rspInfo, msg[2], requestID);
    }

    private void ProcessRspRtnPrivate(List<byte[]> msg, RspInfoFrameField rspInfo, int requestID)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
            OnRspRtnPrivate(rspInfo, msg[2], requestID);
    }

    private void ProcessRspSubRoute(List<byte[]> msg, RspInfoFrameField rspInfo, int requestID)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
            OnRspSubRoute(rspInfo, msg[2], requestID);
    }

    private void ProcessRspRspRoute(List<byte[]> msg, RspInfoFrameField rspInfo, int requestID)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
            OnRspRspRoute(rspInfo, msg[2], requestID);
    }
    #endregion

    #region Request Processes
    private static void ReqLogin(SystemChannel channel, UserRequestHeaderFrame header, List<byte[]> msg, SortedSet<string> userTopics)
    {
        if (msg.Count == 4)
        {
            header.Set(msg[2], Global.SESSION_ID.Generate(), msg[1], UserRequestFunctionEnum.LOGIN);
            var headerData = header.GetBuffer();
            var reqData = ReqUserLogin.GetBytes(msg[3], "");
            userTopics.Add(UserResponseHeaderFrame.GetUserSession(headerData));
            channel.SendUserRequestMessage(headerData, reqData);
        }
    }

    private static void ReqLogout(SystemChannel channel, UserRequestHeaderFrame header, List<byte[]> msg)
    {
        if (msg.Count == 2)
        {
            header.Set(msg[1], UserRequestFunctionEnum.LOGOUT);
            channel.SendUserRequestMessage(header.GetBuffer(), []);
        }
    }

    private static void ReqDestroy(SystemChannel channel, UserRequestHeaderFrame header)
    {
        var data = UserUnsubMessage.GetBytes(UserResponseHeaderFrame.GetUserSession(header.GetBuffer()));
        channel.SendUserUnsubMessage(data);
    }

    private static void ReqSetUser(SystemChannel channel, UserRequestHeaderFrame header, List<byte[]> msg)
    {
        if (msg.Count == 3)
        {
            header.Set(msg[1], UserRequestFunctionEnum.SET_USER);
            channel.SendUserRequestMessage(header.GetBuffer(), msg[2]);
        }
    }

    private static void ReqRtnPrivate(SystemChannel channel, UserRequestHeaderFrame header, List<byte[]> msg)
    {
        if (msg.Count == 3)
        {
            header.Set(msg[1], UserRequestFunctionEnum.RTN_PRIVATE);
            channel.SendUserRequestMessage(header.GetBuffer(), msg[2]);
        }
    }

    private static void ReqSubRoute(SystemChannel channel, UserRequestHeaderFrame header, List<byte[]> msg)
    {
        if (msg.Count == 3)
        {
            header.Set(msg[1], UserRequestFunctionEnum.SUB_ROUTE);
            channel.SendUserRequestMessage(header.GetBuffer(), RouteHeaderFrame.GetBytesOfFuntionTopic(msg[2]));
        }
    }

    private static void ReqRspRoute(SystemChannel channel, UserRequestHeaderFrame header, List<byte[]> msg)
    {
        if (msg.Count == 5)
        {
            header.Set(msg[1], UserRequestFunctionEnum.RSP_ROUTE);
            channel.SendUserRequestMessage(header.GetBuffer(), msg[2], msg[3], msg[4]);
        }
    }
    #endregion

    #region Events
    public event EventHandler<FrontDisconnectedEventArgs>? FrontDisconnected;
    public class FrontDisconnectedEventArgs(int errorCode) : EventArgs
    {
        public readonly int ErrorCode = errorCode;
    }
    private void OnFrontDisconnected(int errorCode)
    {
        FrontDisconnected?.Invoke(this, new FrontDisconnectedEventArgs(errorCode));
    }

    public event EventHandler<HeartbeatReceivedEventArgs>? HeartbeatReceived;
    public class HeartbeatReceivedEventArgs(HeartbeatMessageFiled message) : EventArgs
    {
        public readonly HeartbeatMessageFiled Message = message;
    }
    private void OnHeartbeatReceived(HeartbeatMessageFiled message)
    {
        HeartbeatReceived?.Invoke(this, new HeartbeatReceivedEventArgs(message));
    }

    public event EventHandler<FrontConnectedEventArgs>? FrontConnected;
    public class FrontConnectedEventArgs(WelcomeMessageField message) : EventArgs
    {
        public readonly WelcomeMessageField Message = message;
    }
    private void OnFrontConnected(WelcomeMessageField message)
    {
        FrontConnected?.Invoke(this, new FrontConnectedEventArgs(message));
    }

    public event EventHandler<RtnRouteEventArgs>? RtnRoute;
    public class RtnRouteEventArgs(RouteHeaderFrameField routeHeader, UserResponseHeaderFrameField userRspHeader, byte[] userRspHeaderData) : EventArgs
    {
        public readonly RouteHeaderFrameField RouteHeader = routeHeader;
        public readonly UserResponseHeaderFrameField UserRspHeader = userRspHeader;
        public readonly byte[] UserRspHeaderData = userRspHeaderData;
    }
    private void OnRtnRoute(RouteHeaderFrameField routeHeader, UserResponseHeaderFrameField userRspHeader, byte[] userRspHeaderData)
    {
        RtnRoute?.Invoke(this, new RtnRouteEventArgs(routeHeader, userRspHeader, userRspHeaderData));
    }

    public event EventHandler<RspUserLoginEventArgs>? RspUserLogin;
    public class RspUserLoginEventArgs(RspInfoFrameField rspInfo, byte[] rspData) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
    }
    private void OnRspUserLogin(RspInfoFrameField rspInfo, byte[] rspData)
    {
        RspUserLogin?.Invoke(this, new RspUserLoginEventArgs(rspInfo, rspData));
    }

    public event EventHandler<RspUserLogoutEventArgs>? RspUserLogout;
    public class RspUserLogoutEventArgs(RspInfoFrameField rspInfo) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
    }
    private void OnRspUserLogout(RspInfoFrameField rspInfo)
    {
        RspUserLogout?.Invoke(this, new RspUserLogoutEventArgs(rspInfo));
    }

    public event EventHandler<RspSetUserEventArgs>? RspSetUser;
    public class RspSetUserEventArgs(RspInfoFrameField rspInfo, byte[] rspData, int requestID) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
        public readonly int RequestID = requestID;
    }
    private void OnRspSetUser(RspInfoFrameField rspInfo, byte[] rspData, int requestID)
    {
        RspSetUser?.Invoke(this, new RspSetUserEventArgs(rspInfo, rspData, requestID));
    }

    public event EventHandler<RspRtnPrivateEventArgs>? RspRtnPrivate;
    public class RspRtnPrivateEventArgs(RspInfoFrameField rspInfo, byte[] rspData, int requestID) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
        public readonly int RequestID = requestID;
    }
    private void OnRspRtnPrivate(RspInfoFrameField rspInfo, byte[] rspData, int requestID)
    {
        RspRtnPrivate?.Invoke(this, new RspRtnPrivateEventArgs(rspInfo, rspData, requestID));
    }

    public event EventHandler<RspSubRouteEventArgs>? RspSubRoute;
    public class RspSubRouteEventArgs(RspInfoFrameField rspInfo, byte[] rspData, int requestID) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
        public readonly int RequestID = requestID;
    }
    private void OnRspSubRoute(RspInfoFrameField rspInfo, byte[] rspData, int requestID)
    {
        RspSubRoute?.Invoke(this, new RspSubRouteEventArgs(rspInfo, rspData, requestID));
    }

    public event EventHandler<RspRspRouteEventArgs>? RspRspRoute;
    public class RspRspRouteEventArgs(RspInfoFrameField rspInfo, byte[] rspData, int requestID) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
        public readonly int RequestID = requestID;
    }
    private void OnRspRspRoute(RspInfoFrameField rspInfo, byte[] rspData, int requestID)
    {
        RspRspRoute?.Invoke(this, new RspRspRouteEventArgs(rspInfo, rspData, requestID));
    }
    #endregion
}
