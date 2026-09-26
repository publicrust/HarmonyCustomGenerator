using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomGenerator.Launcher
{
    // Just enough HTTP/1.1 for one page and a JSON API on loopback: one request per connection
    internal sealed class Request
    {
        private const int MaxHeader = 64 * 1024, MaxBody = 16 * 1024 * 1024;

        public string Method, Path;
        public byte[] Body = new byte[0];
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _query = new Dictionary<string, string>();

        public string Header(string name) => _headers.TryGetValue(name, out var v) ? v : null;
        public string Query(string name) => _query.TryGetValue(name, out var v) ? v : null;

        public static Request Read(Stream stream) {
            var head = new MemoryStream();
            int matched = 0;
            while (matched < 4) {
                int b = stream.ReadByte();
                if (b < 0) return null;
                head.WriteByte((byte)b);
                matched = (b == '\r' && matched % 2 == 0) || (b == '\n' && matched % 2 == 1) ? matched + 1 : b == '\r' ? 1 : 0;
                if (head.Length > MaxHeader) return null;
            }

            var lines = Encoding.ASCII.GetString(head.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
            var first = lines[0].Split(' ');
            if (first.Length < 2) return null;
            var request = new Request { Method = first[0].ToUpperInvariant() };
            string target = first[1];
            int q = target.IndexOf('?');
            request.Path = q < 0 ? target : target.Substring(0, q);
            if (q >= 0) {
                foreach (var pair in target.Substring(q + 1).Split('&').Where(x => x.Length > 0)) {
                    int eq = pair.IndexOf('=');
                    request._query[Uri.UnescapeDataString(eq < 0 ? pair : pair.Substring(0, eq))] = eq < 0 ? "" : Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
                }
            }
            foreach (var line in lines.Skip(1)) {
                int colon = line.IndexOf(':');
                if (colon > 0) request._headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }

            if (int.TryParse(request.Header("Content-Length"), out int length) && length > 0) {
                if (length > MaxBody) return null;
                request.Body = new byte[length];
                int read = 0;
                while (read < length) {
                    int n = stream.Read(request.Body, read, length - read);
                    if (n <= 0) return null;
                    read += n;
                }
            }
            return request;
        }
    }

    internal sealed class Response
    {
        private readonly int _status;
        private readonly string _type;
        private readonly byte[] _body;

        public Response(int status, string type, byte[] body) { _status = status; _type = type; _body = body; }

        public static Response RawJson(string json) => new Response(200, "application/json; charset=utf-8", Program.Utf8.GetBytes(json));
        public static Response Error(int status, string message) =>
            new Response(status, "application/json; charset=utf-8", Program.Utf8.GetBytes(Program.Json.Serialize(new Dictionary<string, object> { ["error"] = message })));

        public void Write(Stream stream) {
            string reason = _status == 200 ? "OK" : _status == 404 ? "Not Found" : _status == 403 ? "Forbidden" : _status == 409 ? "Conflict" : _status == 400 ? "Bad Request" : "Error";
            var head = $"HTTP/1.1 {_status} {reason}\r\nContent-Type: {_type}\r\nContent-Length: {_body.Length}\r\n" +
                       "Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(head);
            stream.Write(bytes, 0, bytes.Length);
            stream.Write(_body, 0, _body.Length);
            stream.Flush();
        }
    }
}
