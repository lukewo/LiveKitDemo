using System;
using System.Security.Cryptography;
using System.Text;

namespace LiveKitDemo.Services
{
    /// <summary>
    /// Creates LiveKit access tokens (HS256 JWTs) from an API key and secret.
    /// Same claims as `lk token create --join --room R --identity I`.
    ///
    /// DEMO ONLY: in a real app this runs on your server, never on the device.
    /// </summary>
    public static class LiveKitTokenGenerator
    {
        public static string Create(string apiKey, string apiSecret, string room, string identity,
            string name, TimeSpan lifetime)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required.", nameof(apiKey));
            if (string.IsNullOrWhiteSpace(apiSecret)) throw new ArgumentException("API secret is required.", nameof(apiSecret));
            if (string.IsNullOrWhiteSpace(room)) throw new ArgumentException("Room name is required.", nameof(room));
            if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Identity is required.", nameof(identity));

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long exp = now + (long)lifetime.TotalSeconds;

            const string header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";
            string payload =
                "{" +
                $"\"iss\":{Json(apiKey)}," +
                $"\"sub\":{Json(identity)}," +
                $"\"name\":{Json(string.IsNullOrWhiteSpace(name) ? identity : name)}," +
                $"\"nbf\":{now - 10}," +   // small allowance for clock skew
                $"\"exp\":{exp}," +
                "\"video\":{" +
                    $"\"room\":{Json(room)}," +
                    "\"roomJoin\":true," +
                    "\"canPublish\":true," +
                    "\"canSubscribe\":true," +
                    "\"canPublishData\":true" +
                "}" +
                "}";

            string unsigned = Base64Url(Encoding.UTF8.GetBytes(header)) + "." + Base64Url(Encoding.UTF8.GetBytes(payload));
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
            {
                byte[] signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(unsigned));
                return unsigned + "." + Base64Url(signature);
            }
        }

        static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>Minimal JSON string literal encoder.</summary>
        static string Json(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
