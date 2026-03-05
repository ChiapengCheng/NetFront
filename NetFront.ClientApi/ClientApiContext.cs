using NetFront.Frames;
using NetFront.Messages;
using NetFront.Transport;
using NetFront.UserManagement;

namespace NetFront.ClientApi;

public class ClientApiContext : IDisposable
{
    private readonly string INTERNAL_ADDRESS = $"inproc://{Guid.NewGuid():N}";
    private const int HEARTBEAT_CHECK_TIMER_INTERVAL = 30000;
    private ConnectionStatusEnum _conStatus;
    private LoginStatusEnum _loginStatus;
    private InprocChannel _process = default!;
    private int _requestId;
    private CancellationTokenSource _runCts = new();

    public LoginStatusEnum LoginStatus
    {
        get { return _loginStatus; }
    }

    public ClientApiContext()
    {
        Init();
    }

    ~ClientApiContext()
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
            using var processSocket = new InprocChannel();
            using var clientSocket = new TcpSubClient();
            var clientChannel = new ClientChannel(clientSocket, address, 0, 0);
            var processChannel = new ProcessChannel(processSocket, INTERNAL_ADDRESS, 0, 0);

            var hbTimeFlag = 0L;
            var checkTimeFlag = 0L;
            var header = new UserRequestHeaderFrame();
            var userTopics = new SortedSet<string>();
            var dicFunctionCode = new Dictionary<int, int>();

            List<byte[]> clientMsg = [];
            clientSocket.ReceiveReady += () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    if (clientSocket.TryReceiveMultipartBytes(ref clientMsg))
                    {
                        if (clientChannel.TryGetMsgType(clientMsg, out var type))
                        {
                            switch (type)
                            {
                                case (byte)MessageTypeEnum.PUBLIC:
                                    OnRtnPublic(clientMsg[0]);
                                    break;
                                case (byte)MessageTypeEnum.PRIVATE:
                                    if (PrivateMessage.TryParse(clientMsg[0], out PrivateMessageField field))
                                        OnRtnPrivate(field);
                                    break;
                                case (byte)MessageTypeEnum.USER_RSP:
                                    if (UserResponseMessage.IsValid(clientMsg))
                                    {
                                        if (UserResponseHeaderFrame.TryParse(clientMsg[0], out UserResponseHeaderFrameField rspHeader))
                                        {
                                            if (RspInfoFrame.TryParse(clientMsg[1], out RspInfoFrameField rspInfo))
                                            {
                                                switch (rspHeader.FunctionID)
                                                {
                                                    case (byte)UserRequestFunctionEnum.LOGIN:
                                                        ProcessRspLogin(clientMsg, rspInfo);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.LOGOUT:
                                                        ProcessRspLogout(clientChannel, rspInfo, userTopics);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.SUB_PUBLIC:
                                                        ProcessRspSubPublic(clientMsg, rspInfo, rspHeader.RequestID);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.GET_PUBLIC:
                                                        ProcessRspGetPublic(clientMsg, rspInfo, rspHeader.RequestID);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.SUB_PRIVATE:
                                                        ProcessRspSubPrivate(clientMsg, rspInfo, rspHeader.RequestID);
                                                        break;
                                                    case (byte)UserRequestFunctionEnum.REQ_ROUTE:
                                                        ProcessRspRoute(clientMsg, rspInfo, rspHeader.RequestID, dicFunctionCode);
                                                        break;
                                                    default:
                                                        break;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case (byte)MessageTypeEnum.WELCOME:
                                    if (WelcomeMessage.TryParse(clientMsg[0], out WelcomeMessageField wc))
                                    {
                                        switch (_conStatus)
                                        {
                                            case ConnectionStatusEnum.IN_PROCESS:
                                                SetConnStatus(ConnectionStatusEnum.OK);
                                                clientChannel.SendHeartbeatSubMessage();
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
                                    if (HeartbeatMessage.TryParse(clientMsg[0], out HeartbeatMessageFiled hb))
                                        OnHeartbeatReceived(hb);
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
                                case (byte)ClientApiFunctionEnum.LOGIN:
                                    ReqLogin(clientChannel, header, procMsg, userTopics);
                                    break;
                                case (byte)ClientApiFunctionEnum.LOGOUT:
                                    ReqLogout(clientChannel, header, procMsg);
                                    break;
                                case (byte)ClientApiFunctionEnum.SUB_PUBLIC:
                                    ReqSubPublic(clientChannel, header, procMsg, userTopics);
                                    break;
                                case (byte)ClientApiFunctionEnum.GET_PUBLIC:
                                    ReqGetPublic(clientChannel, header, procMsg);
                                    break;
                                case (byte)ClientApiFunctionEnum.UNSUB_PUBLIC:
                                    ReqUnsubPublic(clientChannel, procMsg, userTopics);
                                    break;
                                case (byte)ClientApiFunctionEnum.SUB_PRIVATE:
                                    ReqSubPrivate(clientChannel, header, procMsg, userTopics);
                                    break;
                                case (byte)ClientApiFunctionEnum.UNSUB_PRIVATE:
                                    ReqUnsubPrivate(clientChannel, procMsg, header, userTopics);
                                    break;
                                case (byte)ClientApiFunctionEnum.REQ_ROUTE:
                                    ReqReqRoute(clientChannel, header, procMsg, dicFunctionCode);
                                    break;
                                case (byte)ClientApiFunctionEnum.DESTROY:
                                    ReqDestroy(clientChannel, header);
                                    break;
                                case (byte)ClientApiFunctionEnum.DISCONNECT:
                                    _runCts.Cancel();
                                    Init();
                                    OnFrontDisconnected((int)ErrorCodeEnum.DISCONNECT_FROM_EVENT_PRX);
                                    break;
                                case 101:
                                    ReqReq101(clientChannel, header, procMsg);
                                    break;
                                default:
                                    break;
                            }
                        }
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
        return Connect($"tcp://{ip}:{port}");
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
                    Thread.Sleep(200);
                    _process = new InprocChannel();
                    _process.Connect(INTERNAL_ADDRESS);
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
                _process.SendFrame([(byte)ClientApiFunctionEnum.DISCONNECT]);
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
                            Thread.Sleep(200);
                            _requestId = 0;
                            _process.SendMultipart(
                                [(byte)ClientApiFunctionEnum.LOGIN],
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
                            [(byte)ClientApiFunctionEnum.LOGOUT],
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
                        _process.SendFrame([(byte)ClientApiFunctionEnum.DESTROY]);
                        return (int)ErrorCodeEnum.OK;
                    default:
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int SubPublic(string topic, out int requestId)
    {
        return SubPublic(Serializer.GetUtf8Bytes(topic), out requestId);
    }

    public int SubPublic(byte[] topic, out int requestId)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        if (topic != null)
                        {
                            if (topic.Length >= 2)
                            {
                                requestId = ++_requestId;
                                _process.SendMultipart(
                                    [(byte)ClientApiFunctionEnum.SUB_PUBLIC],
                                    Global.REQUEST_ID.ToBytes(requestId),
                                    topic);
                                return (int)ErrorCodeEnum.OK;
                            }
                        }
                        requestId = -1;
                        return (int)ErrorCodeEnum.TOPIC_CHECK_ERROR;
                    default:
                        requestId = -1;
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                requestId = -1;
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int SubPrivate(int functionCode, out int requestId)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        requestId = ++_requestId;
                        _process.SendMultipart(
                            [(byte)ClientApiFunctionEnum.SUB_PRIVATE],
                            Global.REQUEST_ID.ToBytes(requestId),
                            Global.FUNCTION_CODE.ToBytes(functionCode));
                        return (int)ErrorCodeEnum.OK;
                    default:
                        requestId = -1;
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                requestId = -1;
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int UnsubPublic(string topic)
    {
        return UnsubPublic(Serializer.GetUtf8Bytes(topic));
    }

    public int UnsubPublic(byte[] topic)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        if (topic != null)
                            if (topic.Length >= 2)
                                _process.SendMultipart(
                                    [(byte)ClientApiFunctionEnum.UNSUB_PUBLIC],
                                    topic);
                        return (int)ErrorCodeEnum.OK;
                    default:
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int UnsubPrivate(int functionCode)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        _process.SendMultipart(
                            [(byte)ClientApiFunctionEnum.UNSUB_PRIVATE],
                            Global.FUNCTION_CODE.ToBytes(functionCode));
                        return (int)ErrorCodeEnum.OK;
                    default:
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int GetPublic(string key, out int requestId)
    {
        return GetPublic(Serializer.GetUtf8Bytes(key), out requestId);
    }

    public int GetPublic(byte[] key, out int requestId)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        requestId = ++_requestId;
                        _process.SendMultipart(
                            [(byte)ClientApiFunctionEnum.GET_PUBLIC],
                            Global.REQUEST_ID.ToBytes(requestId),
                            key);
                        return (int)ErrorCodeEnum.OK;
                    default:
                        requestId = -1;
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                requestId = -1;
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int ReqRoute(int functionCode, byte[] data, out int requestId)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        requestId = ++_requestId;
                        _process.SendMultipart(
                            [(byte)ClientApiFunctionEnum.REQ_ROUTE],
                            Global.REQUEST_ID.ToBytes(requestId),
                            Global.FUNCTION_CODE.ToBytes(functionCode),
                            data);
                        return (int)ErrorCodeEnum.OK;
                    default:
                        requestId = -1;
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                requestId = -1;
                return (int)ErrorCodeEnum.CONNECTION_STATUS_ERROR;
        }
    }

    public int Req101(string strategyID, out int requestId)
    {
        switch (_conStatus)
        {
            case ConnectionStatusEnum.OK:
                switch (_loginStatus)
                {
                    case LoginStatusEnum.OK:
                        requestId = ++_requestId;
                        _process.SendMultipart(
                            [101],
                            Global.REQUEST_ID.ToBytes(requestId),
                            Serializer.GetUtf8Bytes(strategyID));
                        return (int)ErrorCodeEnum.OK;
                    default:
                        requestId = -1;
                        return (int)ErrorCodeEnum.LOGIN_STATUS_ERROR;
                }
            default:
                requestId = -1;
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

    private void ProcessRspLogout(ClientChannel clientChannel, RspInfoFrameField rspInfo, SortedSet<string> userTopics)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
        {
            if (rspInfo.ErrorCode == 0)
            {
                SetLoginStatus(LoginStatusEnum.READY);
                var topics = userTopics.ToList();
                foreach (var topic in topics)
                    clientChannel.SendUserUnsubMessage(UserUnsubMessage.GetBytes(topic));
                userTopics.Clear();
            }
            OnRspUserLogout(rspInfo);
        }
    }

    private void ProcessRspSubPublic(List<byte[]> msg, RspInfoFrameField rspInfo, int requestID)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
            OnRspSubPublic(rspInfo, msg[2], requestID);
    }

    private void ProcessRspGetPublic(List<byte[]> msg, RspInfoFrameField rspInfo, int requestID)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
            OnRspGetPublic(rspInfo, msg[2], requestID);
    }

    private void ProcessRspSubPrivate(List<byte[]> msg, RspInfoFrameField rspInfo, int requestID)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
            OnRspSubPrivate(rspInfo, msg[2], requestID);
    }

    private void ProcessRspRoute(List<byte[]> msg, RspInfoFrameField rspInfo, int requestID, Dictionary<int, int> dicFunctionCode)
    {
        if (_conStatus == ConnectionStatusEnum.OK && _loginStatus == LoginStatusEnum.OK)
            if (dicFunctionCode.TryGetValue(requestID, out int functionCode))
                OnRspRoute(functionCode, rspInfo, msg[2], requestID);
    }
    #endregion

    #region Request Processes
    private static void ReqLogin(ClientChannel channel, UserRequestHeaderFrame header, List<byte[]> msg, SortedSet<string> userTopics)
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

    private static void ReqLogout(ClientChannel channel, UserRequestHeaderFrame header, List<byte[]> msg)
    {
        if (msg.Count == 2)
        {
            header.Set(msg[1], UserRequestFunctionEnum.LOGOUT);
            channel.SendUserRequestMessage(header.GetBuffer(), []);
        }
    }

    private static void ReqDestroy(ClientChannel channel, UserRequestHeaderFrame header)
    {
        var data = UserUnsubMessage.GetBytes(UserResponseHeaderFrame.GetUserSession(header.GetBuffer()));
        channel.SendUserUnsubMessage(data);
    }

    private static void ReqSubPublic(ClientChannel channel, UserRequestHeaderFrame header, List<byte[]> msg, SortedSet<string> userTopics)
    {
        if (msg.Count == 3)
        {
            header.Set(msg[1], UserRequestFunctionEnum.SUB_PUBLIC);
            userTopics.Add(Serializer.ReadStringUtf8(msg[2]));
            channel.SendUserRequestMessage(header.GetBuffer(), msg[2]);
        }
    }

    private static void ReqSubPrivate(ClientChannel channel, UserRequestHeaderFrame header, List<byte[]> msg, SortedSet<string> userTopics)
    {
        if (msg.Count == 3)
        {
            header.Set(msg[1], UserRequestFunctionEnum.SUB_PRIVATE);
            var topic = PrivateMessage.GetUserTopic(msg[2], header.GetBytesOfUserID());
            userTopics.Add(Serializer.ReadStringUtf8(topic));
            channel.SendUserRequestMessage(header.GetBuffer(), msg[2]);
        }
    }

    private static void ReqGetPublic(ClientChannel channel, UserRequestHeaderFrame header, List<byte[]> msg)
    {
        if (msg.Count == 3)
        {
            header.Set(msg[1], UserRequestFunctionEnum.GET_PUBLIC);
            channel.SendUserRequestMessage(header.GetBuffer(), msg[2]);
        }
    }

    private static void ReqUnsubPublic(ClientChannel channel, List<byte[]> msg, SortedSet<string> userTopics)
    {
        if (msg.Count == 2)
        {
            var topic = Serializer.ReadStringUtf8(msg[1]);
            channel.SendUserUnsubMessage(UserUnsubMessage.GetBytes(topic));
            userTopics.Remove(topic);
        }
    }

    private static void ReqUnsubPrivate(ClientChannel channel, List<byte[]> msg, UserRequestHeaderFrame header, SortedSet<string> userTopics)
    {
        if (msg.Count == 2)
        {
            var topic = Serializer.ReadStringUtf8(PrivateMessage.GetUserTopic(msg[1], header.GetBytesOfUserID()));
            channel.SendUserUnsubMessage(UserUnsubMessage.GetBytes(topic));
            userTopics.Remove(topic);
        }
    }

    private static void ReqReqRoute(ClientChannel channel, UserRequestHeaderFrame header, List<byte[]> msg, Dictionary<int, int> dicFunctionCode)
    {
        if (msg.Count == 4)
        {
            header.Set(msg[1], UserRequestFunctionEnum.REQ_ROUTE);
            dicFunctionCode[Global.REQUEST_ID.ToValue(msg[1])] = Global.FUNCTION_CODE.ToValue(msg[2]);
            channel.SendUserRequestMessage(header.GetBuffer(),
                RouteHeaderFrame.GetBytes(msg[2], header.GetBytesOfUserID(), msg[3]));
        }
    }

    private static void ReqReq101(ClientChannel channel, UserRequestHeaderFrame header, List<byte[]> msg)
    {
        if (msg.Count == 3)
        {
            header.Set(msg[1], 101);
            channel.SendUserRequestMessage(header.GetBuffer(), msg[2]);
        }
    }
    #endregion

    #region Events
    public event EventHandler<RspRouteEventArgs>? RspRoute;
    public class RspRouteEventArgs(int functionCode, RspInfoFrameField rspInfo, byte[] rspData, int requestID) : EventArgs
    {
        public readonly int FunctionCode = functionCode;
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
        public readonly int RequestID = requestID;
    }
    private void OnRspRoute(int functionCode, RspInfoFrameField rspInfo, byte[] rspData, int requestID)
    {
        RspRoute?.Invoke(this, new RspRouteEventArgs(functionCode, rspInfo, rspData, requestID));
    }

    public event EventHandler<RspSubPrivateEventArgs>? RspSubPrivate;
    public class RspSubPrivateEventArgs(RspInfoFrameField rspInfo, byte[] rspData, int requestID) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
        public readonly int RequestID = requestID;
    }
    private void OnRspSubPrivate(RspInfoFrameField rspInfo, byte[] rspData, int requestID)
    {
        RspSubPrivate?.Invoke(this, new RspSubPrivateEventArgs(rspInfo, rspData, requestID));
    }

    public event EventHandler<RspGetPublicEventArgs>? RspGetPublic;
    public class RspGetPublicEventArgs(RspInfoFrameField rspInfo, byte[] rspData, int requestID) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
        public readonly int RequestID = requestID;
    }
    private void OnRspGetPublic(RspInfoFrameField rspInfo, byte[] rspData, int requestID)
    {
        RspGetPublic?.Invoke(this, new RspGetPublicEventArgs(rspInfo, rspData, requestID));
    }

    public event EventHandler<RspSubPublicEventArgs>? RspSubPublic;
    public class RspSubPublicEventArgs(RspInfoFrameField rspInfo, byte[] rspData, int requestID) : EventArgs
    {
        public readonly RspInfoFrameField RspInfo = rspInfo;
        public readonly byte[] RspData = rspData;
        public readonly int RequestID = requestID;
    }
    private void OnRspSubPublic(RspInfoFrameField rspInfo, byte[] rspData, int requestID)
    {
        RspSubPublic?.Invoke(this, new RspSubPublicEventArgs(rspInfo, rspData, requestID));
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

    public event EventHandler<FrontDisconnectedEventArgs>? FrontDisconnected;
    public class FrontDisconnectedEventArgs(int errorCode) : EventArgs
    {
        public readonly int ErrorCode = errorCode;
    }
    private void OnFrontDisconnected(int errorCode)
    {
        FrontDisconnected?.Invoke(this, new FrontDisconnectedEventArgs(errorCode));
    }

    public event EventHandler<RtnPublicEventArgs>? RtnPublic;
    public class RtnPublicEventArgs(byte[] data) : EventArgs
    {
        public readonly byte[] Data = data;
    }
    private void OnRtnPublic(byte[] data)
    {
        RtnPublic?.Invoke(this, new RtnPublicEventArgs(data));
    }

    public event EventHandler<RtnPrivateEventArgs>? RtnPrivate;
    public class RtnPrivateEventArgs(PrivateMessageField field) : EventArgs
    {
        public readonly PrivateMessageField Field = field;
    }
    private void OnRtnPrivate(PrivateMessageField field)
    {
        RtnPrivate?.Invoke(this, new RtnPrivateEventArgs(field));
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
    #endregion
}
