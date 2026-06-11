namespace SecureChat.Client.Constants
{
    public static class ApiEndpoints
    {
        public static class Auth
        {
            public const string RequestPasswordOtp = "api/auth/forgot-password/request-otp";
            public const string VerifyPasswordOtp = "api/auth/forgot-password/verify-otp";
            public const string ResetPassword = "api/auth/forgot-password/reset";
        }

        public static class Conversations
        {
            public const string GetAll = "api/conversations";
            public static string GetMessages(string convId) => $"api/conversations/{convId}/messages";
            public static string GetMembers(string convId) => $"api/conversations/{convId}/members";
        }

        public static class Friends
        {
            public const string GetAll = "api/friends";
            public const string RequestsReceived = "api/friends/requests/received";
            public const string RequestsSent = "api/friends/requests/sent";
            public const string Blocked = "api/friends/blocked";
        }
    }
}
