using System.Net;
using System.Net.Http;

namespace Commons.Extensions
{
    public static class HttpStatusCodeExtensions
    {
        public static bool IsSuccessStatusCode(this HttpStatusCode statusCode)
            => (int) statusCode is >= 200 and < 300;

        public static void EnsureSuccessStatusCode(this HttpStatusCode statusCode)
        {
            if (!IsSuccessStatusCode(statusCode))
            {
                throw new HttpRequestException($"{statusCode} is not a success status code");
            }
        }
    }
}