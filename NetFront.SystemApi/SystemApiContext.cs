using NetFront.Frames;
using NetFront.Messages;
using NetFront.UserManagement;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.SystemApi;

public class SystemApiContext : IDisposable
{
    private readonly string INTERNAL_PROCESS_ADDRESS = $"inproc://{Guid.NewGuid():N}";
    private readonly string INTERNAL_PUBLIC_ADDRESS = $"inproc://{Guid.NewGuid():N}";
    private const int HEARTBEAT_CHECK_TIMER_INTERVAL = 30000;
    private const int SYSTEM_WAIT_INTERVAL = 500;
    private ConnectionStatusEnum _conStatus;
    private LoginStatusEnum _loginStatus;
    private PublisherSocket _process = default!;
    private PublisherSocket _publicMain = default!;
    private PublisherSocket _publicSub = default!;
    private int _requestId;

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
        GC.SuppressFinalize(this);
    }

    private void RunCore(string address)
    {
        Task.Factory.StartNew(() =>
        {
            try
            {
                var timer = new NetMQTimer(HEARTBEAT_CHECK_TIMER_INTERVAL);
                using var publicSocket = new XSubscriberSocket();
                using var processSocket = new XSubscriberSocket();
                using var systemSocket = new XSubscriberSocket();
                using var poller = new NetMQPoller();
                var systemChannel = new SystemChannel(systemSocket, address, 0, 0);
                var processChannel = new ProcesslChannel(processSocket, INTERNAL_PROCESS_ADDRESS, 0, 0);
                var publicChannel = new PublicChannel(publicSocket, INTERNAL_PUBLIC_ADDRESS, 0, 0);

                var hbTimeFlag = 0L;
                var checkTimeFlag = 0L;
                var header = new UserRequestHeaderFrame();
                var userTopics = new SortedSet<string>();
                List<byte[]> msg = [];
                timer.Elapsed += (s, a) =>
                {
                    #region Connection Check
                    switch (_conStatus)
                    {
                        case ConnectionStatusEnum.OK:
                            if (hbTimeFlag != 0)
                            {
                                if (checkTimeFlag == hbTimeFlag)
                                {
                                    poller.Stop();
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
                    #endregion
                };
                systemSocket.ReceiveReady += (s, a) =>
                {
                    for (int i = 0; i < 200; i++)
                    {
                        if (a.Socket.TryReceiveMultipartBytes(ref msg))
                        {
                            if (systemChannel.TryGetMsgType(msg, out var type))
                            {
                                switch (type)
                                {
                                    case (byte)MessageTypeEnum.USER_RSP:
                                        if (UserResponseMessage.IsValid(msg))
                                        {
                                            if (UserResponseHeaderFrame.TryParse(msg[0], out UserResponseHeaderFrameField rspHeader))
                                            {
                                                if (RspInfoFrame.TryParse(msg[1], out RspInfoFrameField rspInfo))
                                                {
                                                    switch (rspHeader.FunctionID)
                                                    {
                                                        case (byte)UserRequestFunctionEnum.LOGIN:
                                                            ProcessRspLogin(msg, rspInfo);
                                                            break;
                                                        case (byte)UserRequestFunctionEnum.LOGOUT:
                                                            ProcessRspLogout(systemChannel, rspInfo, userTopics);
                                                            break;
                                                        case (byte)UserRequestFunctionEnum.SET_USER:
                                                            ProcessRspSetUser(msg, rspInfo, rspHeader.RequestID);
                                                            break;
                                                        case (byte)UserRequestFunctionEnum.RTN_PRIVATE:
                                                            ProcessRspRtnPrivate(msg, rspInfo, rspHeader.RequestID);
                                                            break;
                                                        case (byte)UserRequestFunctionEnum.SUB_ROUTE:
                                                            ProcessRspSubRoute(msg, rspInfo, rspHeader.RequestID);
                                                            break;
                                                        case (byte)UserRequestFunctionEnum.RSP_ROUTE:
                                                            ProcessRspRspRoute(msg, rspInfo, rspHeader.RequestID);
                                                            break;
                                                        default:
                                                            break;
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    case (byte)MessageTypeEnum.WELCOME:
                                        if (WelcomeMessage.TryParse(msg[0], out WelcomeMessageField wc))
                                        {
                                            switch (_conStatus)
                                            {
                                                case ConnectionStatusEnum.IN_PROCESS:
                                                    SetConnStatus(ConnectionStatusEnum.OK);
                                                    systemChannel.SendHeartbeatSubMessage();
                                                    Task.Factory.StartNew(() =>
                                                    {
                                                        //Thread.Sleep(100);
                                                        OnFrontConnected(wc);
                                                    });
                                                    break;
                                            }
                                        }
                                        break;
                                    case (byte)MessageTypeEnum.HEARTBEAT:
                                        hbTimeFlag++;
                                        if (HeartbeatMessage.TryParse(msg[0], out HeartbeatMessageFiled hb))
                                            OnHeartbeatReceived(hb);
                                        break;
                                    case (byte)MessageTypeEnum.ROUTE:
                                        if (RouteMessage.IsValid(msg, out RouteHeaderFrameField routeHeader, out UserResponseHeaderFrameField userRspHeader))
                                            OnRtnRoute(routeHeader, userRspHeader, msg[1]);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }                    
                };
                processSocket.ReceiveReady += (s, a) =>
                {
                    for (int i = 0; i < 200; i++)
                    {
                        if (a.Socket.TryReceiveMultipartBytes(ref msg))
                        {
                            if (msg.Count > 0 && msg[0].Length > 0)
                            {
                                switch (msg[0][0])
                                {
                                    case (byte)SystemApiFunctionEnum.LOGIN:
                                        ReqLogin(systemChannel, header, msg, userTopics);
                                        break;
                                    case (byte)SystemApiFunctionEnum.LOGOUT:
                                        ReqLogout(systemChannel, header, msg);
                                        break;
                                    case (byte)SystemApiFunctionEnum.SET_USER:
                                        ReqSetUser(systemChannel, header, msg);
                                        break;
                                    case (byte)SystemApiFunctionEnum.RTN_PRIVATE:
                                        ReqRtnPrivate(systemChannel, header, msg);
                                        break;
                                    case (byte)SystemApiFunctionEnum.SUB_ROUTE:
                                        ReqSubRoute(systemChannel, header, msg);
                                        break;
                                    case (byte)SystemApiFunctionEnum.RSP_ROUTE:
                                        ReqRspRoute(systemChannel, header, msg);
                                        break;
                                    case (byte)SystemApiFunctionEnum.DESTROY:
                                        ReqDestroy(systemChannel, header);
                                        break;
                                    case (byte)SystemApiFunctionEnum.DISCONNECT:
                                        poller.Stop();
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
                publicSocket.ReceiveReady += (s, a) =>
                {
                    for (int i = 0; i < 200; i++)
                    {
                        if (a.Socket.TryReceiveMultipartBytes(ref msg))
                        {
                            systemSocket.SendMultipartBytes(msg);
                        }
                    }                    
                };
                poller.Add(publicSocket);
                poller.Add(processSocket);
                poller.Add(systemSocket);
                poller.Add(timer);
                poller.Run();
            }
            catch (TerminatingException) { }
        });
    }

    #region Status Control
    private void Init()
    {
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
                    _process = new PublisherSocket();
                    _process.Options.SendHighWatermark = 0;
                    _process.Connect(INTERNAL_PROCESS_ADDRESS);
                    _publicMain = new PublisherSocket();
                    _publicMain.Options.SendHighWatermark = 0;
                    _publicMain.Connect(INTERNAL_PUBLIC_ADDRESS);
                    _publicSub = new PublisherSocket();
                    _publicSub.Options.SendHighWatermark = 0;
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
                            _process.SendMoreFrame([(byte)SystemApiFunctionEnum.LOGIN])
                                .SendMoreFrame(Global.REQUEST_ID.ToBytes(_requestId))
                                .SendMoreFrame(Global.USER_ID.ToBytes(userID))
                                .SendFrame(Global.PASSWORD.ToBytes(password));
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
                        _process.SendMoreFrame([(byte)SystemApiFunctionEnum.LOGOUT])
                            .SendFrame(Global.REQUEST_ID.ToBytes(++_requestId));
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

    //OK
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
                            _process.SendMoreFrame([(byte)SystemApiFunctionEnum.SET_USER])
                                .SendMoreFrame(Global.REQUEST_ID.ToBytes(++_requestId))
                                .SendFrame(User.GetBytes(new UserField()
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

    //OK
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
                            _process.SendMoreFrame([(byte)SystemApiFunctionEnum.RTN_PRIVATE])
                                .SendMoreFrame(Global.REQUEST_ID.ToBytes(++_requestId))
                                .SendFrame(PrivateMessage.GetBytes(functionCode, userID, data));
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
                        _process.SendMoreFrame([(byte)SystemApiFunctionEnum.SUB_ROUTE])
                            .SendMoreFrame(Global.REQUEST_ID.ToBytes(++_requestId))
                            .SendFrame(Global.FUNCTION_CODE.ToBytes(functionCode));
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
                            _process.SendMoreFrame([(byte)SystemApiFunctionEnum.RSP_ROUTE])
                                .SendMoreFrame(Global.REQUEST_ID.ToBytes(++_requestId))
                                .SendMoreFrame(rspHeader)
                                .SendMoreFrame(RspInfoFrame.GetBytes(time, errorCode, errorMSg, isLast))
                                .SendFrame(rspData);
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

    //OK
    public int SET(string key, byte[] value)
    {
        if (PublicCommandHeaderFrame.TryGetBytes_SET(key, out var comm))
        {
            return PUB_COMMAND(comm, value);
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    //OK
    public int PUB(byte[] data)    
    {
        if (PublicCommandHeaderFrame.TryGetBytes_PUB(out var comm))
        {
            return PUB_COMMAND(comm, data);
        }
        return (int)ErrorCodeEnum.FUNCTION_ARGS_ERROR;
    }

    //OK
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
                _publicMain.SendMoreFrame(commFrame).SendFrame(dataFrame);
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
