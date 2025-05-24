namespace NetFront.UserManagement;

public class UserManagementContext
{
    private readonly Dictionary<string, UserField> _dicUser = [];
    private readonly Dictionary<string, long> _dicUserSession = [];
    private readonly Dictionary<string, int> _dicUserConnection = [];

    public UserManagementContext(UserField[] defaultUsers)
    {
        foreach (var user in defaultUsers)
        {
            _dicUser[user.UserID] = user;
        }
    }

    public UserField[] LoadUsers()        
    { 
        return [.. _dicUser.Values];
    }

    public bool SetUser(byte[] dataFrame, out UserField user)
    {
        if (User.TryParse(dataFrame, out user))
        {
            _dicUser[user.UserID] = user;
            return true;
        }
        return false;
    }

    public bool DelUserSession(byte userRole, string userSession)
    {
        var key = $"{userRole}:{userSession}";
        if (_dicUserSession.TryGetValue(key, out _))
        {
            _dicUserSession.Remove(key);
            var userID = UserSession.GetUserID(userSession);
            if (_dicUserConnection.TryGetValue(userID, out int count))
                _dicUserConnection[userID] = count - 1;
            return true;
        }
        return false;
    }

    public bool SessionExists(byte userRole, string userSession)
    {
        return _dicUserSession.ContainsKey($"{userRole}:{userSession}");
    }

    private void CreateSession(byte userRole, string userID, string userSession, long time)
    {
        _dicUserSession.Add($"{userRole}:{userSession}", time);
        if (!_dicUserConnection.TryGetValue(userID, out int count))
            count = 0;
        _dicUserConnection[userID] = count + 1;
    }

    private int GetUserConntionCount(string userID)
    {
        if (_dicUserConnection.TryGetValue(userID, out int count))
            return count;
        else
            return 0;
    }

    public bool LoginCheck(byte userRole, string userID, string userSession, string password, long time, out int errorCode, out string errorMsg)
    {
        errorMsg = "";
        if (!_dicUser.TryGetValue(userID, out UserField user))
        {
            errorCode = (int)ErrorCodeEnum.USER_ID_NOT_EXISTS;
            return false;
        }
        if (user.Password != password)
        {
            errorCode = (int)ErrorCodeEnum.WRONG_PASSWORD;
            return false;
        }
        if (user.UserStatus != (byte)UserStatusEnum.ACTIVE)
        {
            errorCode = (int)ErrorCodeEnum.STATUS_CHECK_FAIL;
            return false;
        }
        if (user.UserRole != userRole)
        {
            errorCode = (int)ErrorCodeEnum.USER_ROLE_CHECK_FAIL;
            return false;
        }
        if (SessionExists(userRole, userSession))
        {
            errorCode = (int)ErrorCodeEnum.SESSION_EXISTS;
            return false;
        }
        var connectionCount = GetUserConntionCount(user.UserID);
        if (user.ConnectionLimit <= connectionCount)
        {
            errorCode = (int)ErrorCodeEnum.OVER_CONNECTION_LIMIT;
            return false;
        }
        CreateSession(userRole, userID, userSession, time);
        errorCode = (int)ErrorCodeEnum.OK;
        return true;
    }

}
