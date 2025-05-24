using NetFront.Database;
using NetFront.Frames;
using NetFront.Messages;
using NetFront.UserManagement;
using NetMQ;
using NetMQ.Sockets;

namespace NetFront.Context;

public class BasicContext
{
    public bool IsRunning { get; set; }
    public BasicContext()
    {
        IsRunning = false;
    }

    public void Start(FrontSettings set)
    {
        if (!IsRunning)
        {
            IsRunning = true;
            _ = Task.Factory.StartNew(() => { RunCore(set); });
            _ = Task.Factory.StartNew(() => { RunHeartbeat(set); });
        }
    }

    private void RunCore(FrontSettings set)
    {
        var db = new DbContext();
        var um = new UserManagementContext(set.USERS);
        OnLoadUsersReceived(um.LoadUsers());
        using var systemSocket = new XPublisherSocket();
        using var clientSocket = new XPublisherSocket();
        using var poller = new NetMQPoller() { systemSocket, clientSocket };

        var systemChannel = new SystemChannel(systemSocket, set.SYSTEM_ADDRESS, 0, 0,
            WelcomeMessage.GetBytes(set.SYSTEM_ID, set.SHOW_SYSTEM_ADDRESS));
        var clientChannel = new ClientChannel(clientSocket, set.CLIENT_ADDRESS, 0, 0, 
            WelcomeMessage.GetBytes(set.SYSTEM_ID, set.SHOW_CLINET_ADDRESS));
        List<byte[]> msg = [];
        systemSocket.ReceiveReady += (s, a) =>
        {
            for (int i = 0; i < 200; i++)
            {
                if (!a.Socket.TryReceiveMultipartBytes(ref msg)) break;
                if (!systemChannel.TryGetMsgType(msg, out var msgType)) continue;
                switch (msgType)
                {
                    case (byte)MessageTypeEnum.PUBLIC_COMMAND:
                        if (!PublicCommandMessage.IsValid(msg, out var commandType)) break;
                        switch (commandType)
                        {
                            case (byte)PublicCommandTypeEnum.SPUB:
                                if (msg.Count == 2)
                                {
                                    if (PublicCommandMessage.TryGetTopic_SPUB(msg[0], msg[1], out byte[] topic))
                                    {
                                        if (PublicMessage.IsValidTopic(msg[1]))
                                        {
                                            db.Set(topic, msg[1]);
                                            clientChannel.SendPublicMessage(msg[1]);
                                        }
                                    }
                                }
                                break;
                            case (byte)PublicCommandTypeEnum.PUB:
                                if (msg.Count == 2)
                                {
                                    if (PublicMessage.IsValidTopic(msg[1]))
                                    {
                                        clientChannel.SendPublicMessage(msg[1]);
                                    }
                                }
                                break;
                            case (byte)PublicCommandTypeEnum.SET:
                                if (msg.Count == 2)
                                {
                                    if (PublicCommandHeaderFrame.TryGetArgData_SET(msg[0], out int offset, out int length))
                                    {
                                        db.Set(msg[0].AsSpan(offset, length), msg[1]);
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                        break;
                    case (byte)MessageTypeEnum.HEARTBEAT:
                        systemChannel.SendHeartbeatMessage(msg[0]);
                        clientChannel.SendHeartbeatMessage(msg[0]);
                        OnChannelStatisticsReceived(systemChannel.Statistics, clientChannel.Statistics);
                        break;
                    case (byte)MessageTypeEnum.FRONT_UNSUB:
                        if (FrontUnsubMessage.IsValid(msg))
                        {
                            if (FrontUnsubMessage.TryGetTopic(msg[0], out var topic))
                            {
                                systemChannel.Unsubscribe(topic);
                                if (UserSession.TryGetUserRspSession(topic, out var session))
                                {
                                    if (um.DelUserSession((byte)UserRoleEnum.SYSTEM, session))
                                    {
                                        OnSessionDeleted(session);
                                    }
                                }
                            }
                        }
                        break;
                    case (byte)MessageTypeEnum.USER_UNSUB:
                        if (UserUnsubMessage.IsValid(msg))
                        {
                            if (UserUnsubMessage.TryGetTopic(msg[0], out var topic))
                            {
                                systemChannel.Unsubscribe(topic);
                                if (UserSession.TryGetUserRspSession(topic, out var session))
                                {
                                    if (um.DelUserSession((byte)UserRoleEnum.SYSTEM, session))
                                    {
                                        OnSessionDeleted(session);
                                    }
                                }
                            }
                        }
                        break;
                    case (byte)MessageTypeEnum.HEARTBEAT_SUB:
                        if (HeartbeatSubMessage.IsValid(msg))
                        {
                            systemChannel.Subscribe(HeartbeatMessage.Topic);
                        }
                        break;
                    case (byte)MessageTypeEnum.USER_REQ:
                        if (UserRequestMessage.IsValid(msg))
                        {
                            var rspHeader = msg[0].AsSpan();
                            if (UserResponseHeaderFrame.TryConvert(rspHeader))
                            {
                                var functionID = UserResponseHeaderFrame.GetFunctionID(rspHeader);
                                var userSession = UserSession.GetUserRspSession(rspHeader);
                                switch (functionID)
                                {
                                    case (byte)UserRequestFunctionEnum.LOGIN:
                                        var reqData = msg[1].AsSpan();
                                        if (ReqUserLogin.TryParse(reqData, out var reqLogin))
                                        {
                                            var time = DateTime.UtcNow.Ticks;
                                            var userID = UserResponseHeaderFrame.GetUserID(rspHeader);
                                            um.LoginCheck((byte)UserRoleEnum.SYSTEM, userID,
                                                userSession, reqLogin.Password, time,
                                                out int errorCode, out string errorMsg);
                                            systemChannel.Subscribe(UserSession.GetUserRspSessionData(rspHeader));
                                            systemChannel.SendUserResponseMessage(msg[0],
                                                RspInfoFrame.GetBytes(time, errorCode, errorMsg, true), []);
                                            if (errorCode == 0)
                                            {
                                                OnSessionCreated(userID, userSession, time);
                                            }
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.LOGOUT:
                                        if (um.DelUserSession((byte)UserRoleEnum.SYSTEM, userSession))
                                        {
                                            systemChannel.SendUserResponseMessage(msg[0],
                                                RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), []);
                                            OnSessionDeleted(userSession);
                                        }
                                        else
                                        {
                                            systemChannel.SendUserResponseMessage(msg[0],
                                                RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.SESSION_NOT_EXISTS, "", true), []);
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.SET_USER:
                                        if (um.SessionExists((byte)UserRoleEnum.SYSTEM, userSession))
                                        {
                                            if (msg.Count == 2)
                                            {
                                                if (um.SetUser(msg[1], out UserField user))
                                                {
                                                    systemChannel.SendUserResponseMessage(msg[0],
                                                        RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), []);
                                                    OnSetUserReceived(user);
                                                }
                                                else
                                                {
                                                    systemChannel.SendUserResponseMessage(msg[0],
                                                        RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.SET_USER_ERROR, "", true), []);
                                                }
                                            }
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.SUB_ROUTE:
                                        if (um.SessionExists((byte)UserRoleEnum.SYSTEM, userSession))
                                        {
                                            if (RouteHeaderFrame.IsValidFunctionTopic(msg[1]))
                                            {
                                                systemChannel.Subscribe(msg[1]);
                                                systemChannel.SendUserResponseMessage(msg[0],
                                                    RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), []);
                                            }
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.RSP_ROUTE:
                                        if (um.SessionExists((byte)UserRoleEnum.SYSTEM, userSession))
                                        {
                                            if (msg.Count == 4)
                                            {
                                                clientChannel.SendUserResponseMessage(msg[1], msg[2], msg[3]);
                                                systemChannel.SendUserResponseMessage(msg[0],
                                                    RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), []);
                                            }
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.RTN_PRIVATE:
                                        if (um.SessionExists((byte)UserRoleEnum.SYSTEM, userSession))
                                        {
                                            if (PrivateMessage.IsValidUserTopic(msg[1]))
                                            {
                                                clientChannel.SendPrivateMessage(msg[1]);
                                                systemChannel.SendUserResponseMessage(msg[0],
                                                    RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), []);
                                            }
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        break;
                }
            }            
        };

        clientSocket.ReceiveReady += (s, a) =>
        {
            for (int i = 0; i < 200; i++)
            {
                if (!a.Socket.TryReceiveMultipartBytes(ref msg)) break;
                if (!clientChannel.TryGetMsgType(msg, out var msgType)) continue;
                switch (msgType)
                {
                    case (byte)MessageTypeEnum.FRONT_UNSUB:
                        if (FrontUnsubMessage.IsValid(msg))
                        {
                            if (FrontUnsubMessage.TryGetTopic(msg[0], out var topic))
                            {
                                clientChannel.Unsubscribe(topic);
                                if (UserSession.TryGetUserRspSession(topic, out var session))
                                {
                                    if (um.DelUserSession((byte)UserRoleEnum.CLINET, session))
                                    {
                                        OnSessionDeleted(session);
                                    }
                                }
                            }
                        }
                        break;
                    case (byte)MessageTypeEnum.USER_UNSUB:
                        if (UserUnsubMessage.IsValid(msg))
                        {
                            if (UserUnsubMessage.TryGetTopic(msg[0], out byte[] topic))
                            {
                                clientChannel.Unsubscribe(topic);
                                if (UserSession.TryGetUserRspSession(topic, out var session))
                                {
                                    if (um.DelUserSession((byte)UserRoleEnum.CLINET, session))
                                    {
                                        OnSessionDeleted(session);
                                    }
                                }
                            }
                        }
                        break;
                    case (byte)MessageTypeEnum.HEARTBEAT_SUB:
                        if (HeartbeatSubMessage.IsValid(msg))
                        {
                            clientChannel.Subscribe(HeartbeatMessage.Topic);
                        }
                        break;
                    case (byte)MessageTypeEnum.USER_REQ:
                        if (UserRequestMessage.IsValid(msg))
                        {
                            var rspHeader = msg[0].AsSpan();
                            if (UserResponseHeaderFrame.TryConvert(rspHeader))
                            {
                                var functionID = UserResponseHeaderFrame.GetFunctionID(rspHeader);
                                var userSession = UserSession.GetUserRspSession(rspHeader);
                                switch (functionID)
                                {
                                    case (byte)UserRequestFunctionEnum.LOGIN:
                                        var reqData = msg[1].AsSpan();
                                        if (ReqUserLogin.TryParse(reqData, out var reqLogin))
                                        {
                                            var time = DateTime.UtcNow.Ticks;
                                            var userID = UserResponseHeaderFrame.GetUserID(rspHeader);
                                            um.LoginCheck((byte)UserRoleEnum.CLINET, userID,
                                                userSession, reqLogin.Password, time,
                                                out int errorCode, out string errorMsg);
                                            clientChannel.Subscribe(UserSession.GetUserRspSessionData(rspHeader));
                                            clientChannel.SendUserResponseMessage(msg[0],
                                                RspInfoFrame.GetBytes(time, errorCode, errorMsg, true), []);
                                            if (errorCode == 0)
                                            {
                                                OnSessionCreated(userID, userSession, time);
                                            }
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.LOGOUT:
                                        if (um.DelUserSession((byte)UserRoleEnum.CLINET, userSession))
                                        {
                                            clientChannel.SendUserResponseMessage(msg[0],
                                                RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), []);
                                            OnSessionDeleted(userSession);
                                        }
                                        else
                                        {
                                            clientChannel.SendUserResponseMessage(msg[0],
                                                RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.SESSION_NOT_EXISTS, "", true), []);
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.SUB_PUBLIC:
                                        if (um.SessionExists((byte)UserRoleEnum.CLINET, userSession))
                                        {
                                            if (PublicMessage.IsValidTopic(msg[1]))
                                            {
                                                clientChannel.Subscribe(msg[1]);
                                                if (db.TryGet(msg[1], out var data))
                                                {
                                                    clientChannel.SendUserResponseMessage(msg[0],
                                                        RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), data);
                                                }
                                                else
                                                {
                                                    clientChannel.SendUserResponseMessage(msg[0],
                                                        RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.NO_CACHE_DATA, "", true), data);
                                                }
                                            }
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.GET_PUBLIC:
                                        if (um.SessionExists((byte)UserRoleEnum.CLINET, userSession))
                                        {
                                            if (db.TryGet(msg[1], out var data))
                                            {
                                                clientChannel.SendUserResponseMessage(msg[0],
                                                    RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), data);
                                            }
                                            else
                                            {
                                                clientChannel.SendUserResponseMessage(msg[0],
                                                    RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.NO_CACHE_DATA, "", true), data);
                                            }
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.SUB_PRIVATE:
                                        if (um.SessionExists((byte)UserRoleEnum.CLINET, userSession))
                                        {
                                            var userTopic = PrivateMessage.GetUserTopic(msg[1], UserResponseHeaderFrame.GetBytesOfUserID(msg[0]));
                                            clientChannel.Subscribe(userTopic);
                                            clientChannel.SendUserResponseMessage(msg[0],
                                                RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), []);
                                        }
                                        break;
                                    case (byte)UserRequestFunctionEnum.REQ_ROUTE:
                                        if (um.SessionExists((byte)UserRoleEnum.CLINET, userSession))
                                        {
                                            if (RouteHeaderFrame.IsValidUserTopic(msg[1]))
                                            {
                                                systemChannel.SendRouteMessage(msg[1], msg[0]);
                                                //clientChannel.SendUserResponseMessage(msg[0], 
                                                //    RspInfoFrame.GetBytes(DateTime.UtcNow.Ticks, (int)ErrorCodeEnum.OK, "", true), Array.Empty<byte>());
                                            }
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        break;
                }
            }
        };

        poller.Run();
    }

    private static void RunHeartbeat(FrontSettings set)
    {
        var timer = new NetMQTimer(set.HEARTBEAT_INTERVAL);
        using var systemSocket = new XSubscriberSocket();
        using var clientSocket = new XSubscriberSocket();
        using var poller = new NetMQPoller();
        var hb = new HeartbeatMessage(set.SYSTEM_ID);
        var systemHbChannel = new HeartbeatChannel(systemSocket, set.SYSTEM_ADDRESS, 0, 0);
        var clientHbChannel = new HeartbeatChannel(clientSocket, set.CLIENT_ADDRESS, 0, 0);
        List<byte[]> msg = [];
        timer.Elapsed += (s, a) =>
        {
            systemHbChannel.SendHeartbeatMessage(hb.GetBuffer(DateTime.UtcNow.Ticks));
        };
        systemSocket.ReceiveReady += (s, a) =>
        {
            for (int i = 0; i < 200; i++)
            {
                if (!a.Socket.TryReceiveMultipartBytes(ref msg)) break;
                //Do nothing
            }
        };
        clientSocket.ReceiveReady += (s, a) =>
        {
            for (int i = 0; i < 200; i++)
            {
                if (!a.Socket.TryReceiveMultipartBytes(ref msg)) break;
                //Do nothing
            }
        };
        poller.Add(systemSocket);
        poller.Add(clientSocket);
        poller.Add(timer);
        poller.Run();
    }

    public event EventHandler<LoadUsersReceivedEventArgs>? LoadUsersReceived;
    public class LoadUsersReceivedEventArgs(UserField[] users) : EventArgs
    {
        public readonly UserField[] Users = users;
    }
    private void OnLoadUsersReceived(UserField[] users)
    {
        LoadUsersReceived?.Invoke(this, new LoadUsersReceivedEventArgs(users));
    }

    public event EventHandler<ChannelStatisticsReceivedEventArgs>? ChannelStatisticsReceived;
    public class ChannelStatisticsReceivedEventArgs(ChannelStatisticsField sys, ChannelStatisticsField client) : EventArgs
    {
        public ChannelStatisticsField SystemStatistics = sys;
        public ChannelStatisticsField ClientStatistics = client;
    }
    private void OnChannelStatisticsReceived(ChannelStatisticsField sys, ChannelStatisticsField client)
    {
        ChannelStatisticsReceived?.Invoke(this, new ChannelStatisticsReceivedEventArgs(sys, client));
    }

    public event EventHandler<SessionDeletedEventArgs>? SessionDeleted;
    public class SessionDeletedEventArgs(string userSession) : EventArgs
    {
        public readonly string UserSession = userSession;
    }
    private void OnSessionDeleted(string userSession)
    {
        SessionDeleted?.Invoke(this, new SessionDeletedEventArgs(userSession));
    }

    public event EventHandler<SessionCreatedEventArgs>? SessionCreated;
    public class SessionCreatedEventArgs(string userID, string userSession, long ticks) : EventArgs
    {
        public readonly string UserID = userID;
        public readonly string UserSession = userSession;
        public readonly long Ticks = ticks;
    }
    private void OnSessionCreated(string userID, string userSession, long ticks)
    {
        SessionCreated?.Invoke(this, new SessionCreatedEventArgs(userID, userSession, ticks));
    }

    public event EventHandler<SetUserReceivedEventArgs>? SetUserReceived;
    public class SetUserReceivedEventArgs(UserField user) : EventArgs
    {
        public readonly UserField User = user;
    }
    private void OnSetUserReceived(UserField user)
    {
        SetUserReceived?.Invoke(this, new SetUserReceivedEventArgs(user));
    }
}
