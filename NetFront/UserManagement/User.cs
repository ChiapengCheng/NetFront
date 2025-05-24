namespace NetFront.UserManagement;

public class User
{
    private struct Length
    {
        public const int LENGTH = USER_ID + PASSWORD + USER_STATUS + USER_ROLE + CONNECTION_LIMIT;
        public const int USER_ID = Global.USER_ID.LENGTH;
        public const int PASSWORD = Global.PASSWORD.LENGTH;
        public const int USER_STATUS = 1;
        public const int USER_ROLE = 1;
        public const int CONNECTION_LIMIT = 4;
    }

    private struct Index
    {
        public const int USER_ID = 0;
        public const int PASSWORD = 40;
        public const int USER_STATUS = 48;
        public const int USER_ROLE = 49;
        public const int CONNECTION_LIMIT = 50;
    }

    public static byte[] GetBytes(UserField user)
    {
        var output = new byte[Length.LENGTH];
        var s = output.AsSpan();
        Serializer.WriteStringAsciiWithPadRight(s, Index.USER_ID, Length.USER_ID, user.UserID);
        Serializer.WriteStringAsciiWithPadRight(s, Index.PASSWORD,Length.PASSWORD, user.Password);
        Serializer.Write(s, Index.USER_STATUS, user.UserStatus);
        Serializer.Write(s, Index.USER_ROLE, user.UserRole);
        Serializer.Write(s, Index.CONNECTION_LIMIT, user.ConnectionLimit);
        return output;
    }

    public static byte[] GetUserID(string userID)
    {
        var output = new byte[Length.USER_ID];
        Serializer.WriteStringAsciiWithPadRight(output, Index.USER_ID, Length.USER_ID, userID);
        return output;
    }

    public static bool TryParse(Span<byte> data, out UserField user)
    {
        if (data.Length == Length.LENGTH)
        {
            user = new UserField()
            {
                UserID = Serializer.ReadStringAsciiWithTrimEnd(data,Index.USER_ID,Length.USER_ID),
                Password = Serializer.ReadStringAsciiWithTrimEnd(data, Index.PASSWORD, Length.PASSWORD),
                UserStatus = Serializer.Read<byte>(data,Index.USER_STATUS),
                UserRole = Serializer.Read<byte>(data, Index.USER_ROLE),
                ConnectionLimit = Serializer.Read<int>(data,Index.CONNECTION_LIMIT)
            };
            return true;
        }
        user = new UserField();
        return false;
    }

    public static bool CheckPassword(string password) => Serializer.IsAscii(password) && password.Length <= Length.PASSWORD;

    public static bool CheckUserID(string userID) => Serializer.IsAscii(userID) && userID.Length <= Length.USER_ID;
}
