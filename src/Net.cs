// Net.cs — network primitives for site-diag.
// No UI here except user-visible error strings, which go through L.T(zh, en).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SiteDiag
{
    internal class DnsAnswer
    {
        public string Server = "";
        public int Rcode = -1;
        public bool Timeout;
        public string Error = "";
        public List<string> A = new List<string>();
        public List<string> Aaaa = new List<string>();
        public List<string> Cname = new List<string>();
    }

    internal class TlsOut
    {
        public bool Ok;
        public string Err = "";
        public string Subject = "";
        public string Issuer = "";
        public string NotAfter = "";
        public string Protocol = "";
        public bool CertTrusted = true;
        public string CertIssue = "";
    }

    internal class WebOut
    {
        public bool Ok;
        public int Status;
        public string Err = "";
        public string FinalUrl = "";
    }

    internal class HttpOut
    {
        public bool Ok;
        public string Err = "";
        public int Status;
        public string StatusLine = "";
        public string Location = "";
        public string Server = "";
        public string Body = "";
    }

    internal class Obs
    {
        public string Raw = "";
        public string Host = "";
        public bool IsIp;
        public IPAddress Ip;

        public bool HasHosts;
        public string HostsLine = "";
        public bool ProxyOn;
        public string ProxyStr = "";
        public string PacUrl = "";
        public string VpnInfo = "";
        public string SysDns = "";
        public string SysDnsAdapter = "";
        public string PublicIp = "";

        public bool SysDnsFail;
        public List<string> SysA = new List<string>();
        public Dictionary<string, DnsAnswer> Pub = new Dictionary<string, DnsAnswer>();
        public bool PubHasAnswer;
        public List<string> PubA = new List<string>();
        public int DnsMismatch;          // 0 unknown, 1 same, 2 different
        public bool HasAaaa;

        public bool PingOk;
        public int PingMs;
        public bool Tcp443;
        public string Tcp443Err = "";
        public int Tcp443Ms;
        public bool Tcp80;
        public string Tcp80Err = "";
        public int Tcp80Ms;

        public TlsOut TlsSni;
        public TlsOut TlsNoSni;
        public int TlsOkCount;
        public int TlsFailCount;
        public string TlsFailErr = "";

        public HttpOut Http80;
        public HttpOut Http443;
        public WebOut Browser;
    }

    internal class Verdict
    {
        public string Title = "";
        public string Owner = "";
        public List<string> Evidence = new List<string>();
        public List<string> Actions = new List<string>();
        public ConsoleColor Color = ConsoleColor.White;
        public int ExitCode;
    }

    internal static class Net
    {
        // ---------------------------------------------------------------- sockets
        public static string SocketText(SocketException se)
        {
            switch (se.ErrorCode)
            {
                case 10060: return L.T("连接超时（包被丢弃）", "timed out (packets dropped)");
                case 10061: return L.T("连接被拒绝（端口未监听或被拒绝）", "connection refused (port closed or rejected)");
                case 10054: return L.T("连接被重置（RST）", "connection reset (RST)");
                case 10051: return L.T("网络不可达", "network unreachable");
                case 10065: return L.T("主机不可达", "host unreachable");
                case 10013: return L.T("权限被拒绝", "permission denied");
                default: return se.Message + " (" + se.ErrorCode + ")";
            }
        }

        /// <summary>Windows error text is localized; map the few we care about.</summary>
        public static string MapMsg(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf("凭证") >= 0 || s.IndexOf("credential", StringComparison.OrdinalIgnoreCase) >= 0)
                return L.T("安全包中没有可用的凭证", "the security package has no credentials (Schannel)");
            return s;
        }

        public static string Describe(Exception ex)
        {
            Exception e = ex;
            int guard = 0;
            while (e != null && guard++ < 6)
            {
                SocketException se = e as SocketException;
                if (se != null) return SocketText(se);
                AuthenticationException ae = e as AuthenticationException;
                if (ae != null) return L.T("TLS 握手失败：", "TLS handshake failed: ") + Util.Short(MapMsg(ae.Message));
                IOException io = e as IOException;
                if (io != null && io.InnerException != null) { e = io.InnerException; continue; }
                if (e.InnerException != null) { e = e.InnerException; continue; }
                return Util.Short(MapMsg(e.Message));
            }
            return Util.Short(MapMsg(ex.Message));
        }

        public static string WebText(WebException we)
        {
            switch (we.Status)
            {
                case WebExceptionStatus.NameResolutionFailure: return L.T("域名解析失败", "name resolution failed");
                case WebExceptionStatus.ProxyNameResolutionFailure: return L.T("代理服务器域名解析失败", "proxy name resolution failed");
                case WebExceptionStatus.Timeout: return L.T("请求超时", "request timed out");
                case WebExceptionStatus.ConnectFailure: return L.T("无法建立连接", "could not connect");
                case WebExceptionStatus.TrustFailure: return L.T("证书不受信任（浏览器会报“不安全”）", "untrusted certificate (browser shows \"not secure\")");
                case WebExceptionStatus.SecureChannelFailure: return L.T("TLS 握手失败（安全通道）", "TLS handshake failed");
                case WebExceptionStatus.ConnectionClosed: return L.T("连接被断开", "connection closed");
                case WebExceptionStatus.ServerProtocolViolation: return L.T("服务器协议异常", "server protocol violation");
                case WebExceptionStatus.KeepAliveFailure: return L.T("连接被中途重置", "connection reset mid-request");
                case WebExceptionStatus.ReceiveFailure: return L.T("接收数据失败（连接被重置？）", "receive failed (connection reset?)");
                default: return we.Status.ToString() + ": " + Util.Short(we.Message);
            }
        }

        // ---------------------------------------------------------------- probes
        public static bool PingHost(IPAddress ip, int timeout, out int rtt)
        {
            rtt = 0;
            try
            {
                using (Ping p = new Ping())
                {
                    PingReply r = p.Send(ip, timeout);
                    if (r != null && r.Status == IPStatus.Success) { rtt = (int)r.RoundtripTime; return true; }
                    return false;
                }
            }
            catch { return false; }
        }

        public static bool TcpProbe(IPAddress ip, int port, int timeoutMs, out int ms, out string err)
        {
            ms = 0; err = null;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                using (TcpClient c = new TcpClient(ip.AddressFamily))
                {
                    IAsyncResult ar = c.BeginConnect(ip, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                    {
                        ms = (int)sw.ElapsedMilliseconds;
                        err = L.T("连接超时（包被丢弃）", "timed out (packets dropped)");
                        return false;
                    }
                    c.EndConnect(ar);
                    ms = (int)sw.ElapsedMilliseconds;
                    return true;
                }
            }
            catch (SocketException se) { ms = (int)sw.ElapsedMilliseconds; err = SocketText(se); return false; }
            catch (Exception ex) { ms = (int)sw.ElapsedMilliseconds; err = ex.Message; return false; }
        }

        public static TlsOut TlsProbe(IPAddress ip, int port, string targetHost, int timeoutMs)
        {
            TlsOut o = new TlsOut();
            SslPolicyErrors errs = SslPolicyErrors.None;
            string chainMsg = "";
            try
            {
                using (TcpClient c = new TcpClient(ip.AddressFamily))
                {
                    IAsyncResult ar = c.BeginConnect(ip, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                    {
                        o.Err = L.T("TCP 连接超时", "TCP connect timed out");
                        return o;
                    }
                    c.EndConnect(ar);
                    c.ReceiveTimeout = timeoutMs;
                    c.SendTimeout = timeoutMs;
                    using (SslStream ssl = new SslStream(c.GetStream(), false,
                        delegate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors)
                        {
                            errs = errors;
                            if (chain != null && chain.ChainStatus != null)
                            {
                                StringBuilder sb = new StringBuilder();
                                foreach (X509ChainStatus st in chain.ChainStatus)
                                {
                                    if (st.Status == X509ChainStatusFlags.NoError) continue;
                                    if (sb.Length > 0) sb.Append("; ");
                                    sb.Append(st.StatusInformation.Trim());
                                }
                                chainMsg = sb.ToString();
                            }
                            return true;   // complete the handshake, report trust separately
                        }))
                    {
                        ssl.AuthenticateAsClient(targetHost, null,
                            SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                        o.Ok = true;
                        o.Protocol = ssl.SslProtocol.ToString();
                        o.CertTrusted = (errs == SslPolicyErrors.None);
                        o.CertIssue = CertIssueText(errs, chainMsg);
                        X509Certificate cert = ssl.RemoteCertificate;
                        if (cert != null)
                        {
                            X509Certificate2 c2 = new X509Certificate2(cert);
                            o.Subject = c2.Subject;
                            o.Issuer = c2.Issuer;
                            o.NotAfter = c2.NotAfter.ToString("yyyy-MM-dd");
                        }
                    }
                }
            }
            catch (Exception ex) { o.Err = Describe(ex); }
            return o;
        }

        public static string CertIssueText(SslPolicyErrors errs, string chainMsg)
        {
            if (errs == SslPolicyErrors.None) return "";
            StringBuilder sb = new StringBuilder();
            if ((errs & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                sb.Append(L.T("证书与域名不匹配", "certificate does not match hostname"));
            if ((errs & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(L.T("没有拿到证书", "no certificate presented"));
            }
            if ((errs & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(L.T("证书链不受信任（可能是自签或中间人替换）", "untrusted chain (self-signed or intercepted)"));
            }
            if (!string.IsNullOrEmpty(chainMsg))
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(Util.Short(chainMsg));
            }
            return sb.ToString();
        }

        public static HttpOut HttpGet(IPAddress ip, int port, bool tls, string targetHost, string hostHeader, int timeoutMs)
        {
            HttpOut o = new HttpOut();
            SslStream ssl = null;
            try
            {
                using (TcpClient c = new TcpClient(ip.AddressFamily))
                {
                    IAsyncResult ar = c.BeginConnect(ip, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                    {
                        o.Err = L.T("TCP 连接超时", "TCP connect timed out");
                        return o;
                    }
                    c.EndConnect(ar);
                    c.ReceiveTimeout = timeoutMs;
                    c.SendTimeout = timeoutMs;

                    Stream st = c.GetStream();
                    if (tls)
                    {
                        ssl = new SslStream(st, false, delegate { return true; });
                        ssl.AuthenticateAsClient(targetHost, null,
                            SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                        st = ssl;
                    }

                    string req = "GET / HTTP/1.1\r\nHost: " + hostHeader + "\r\n"
                               + "User-Agent: site-diag/2.0\r\nAccept: */*\r\nConnection: close\r\n\r\n";
                    byte[] b = Encoding.ASCII.GetBytes(req);
                    st.Write(b, 0, b.Length);
                    st.Flush();

                    byte[] buf = new byte[4096];
                    MemoryStream ms = new MemoryStream();
                    int n;
                    try
                    {
                        while ((n = st.Read(buf, 0, buf.Length)) > 0)
                        {
                            ms.Write(buf, 0, n);
                            if (ms.Length > 16384) break;
                        }
                    }
                    catch (IOException) { }

                    string all = Encoding.UTF8.GetString(ms.ToArray());
                    if (all.Length == 0)
                    {
                        o.Err = L.T("没有收到任何数据", "no data received");
                        return o;
                    }

                    int headEnd = all.IndexOf("\r\n\r\n");
                    string head = headEnd >= 0 ? all.Substring(0, headEnd) : all;
                    string body = headEnd >= 0 ? all.Substring(headEnd + 4) : "";
                    string[] lines = head.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    o.StatusLine = lines.Length > 0 ? lines[0] : "";
                    o.Status = ParseStatus(o.StatusLine);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        int col = lines[i].IndexOf(':');
                        if (col <= 0) continue;
                        string k = lines[i].Substring(0, col).Trim().ToLowerInvariant();
                        string v = lines[i].Substring(col + 1).Trim();
                        if (k == "location") o.Location = v;
                        else if (k == "server") o.Server = v;
                    }
                    o.Body = body;
                    o.Ok = o.Status > 0;
                    if (!o.Ok) o.Err = L.T("无法解析状态行：", "cannot parse status line: ") + Util.Short(o.StatusLine);
                    if (ssl != null) ssl.Dispose();
                }
            }
            catch (Exception ex) { o.Err = Describe(ex); }
            return o;
        }

        public static int ParseStatus(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;
            string[] p = line.Split(' ');
            if (p.Length >= 2)
            {
                int s;
                if (int.TryParse(p[1], out s)) return s;
            }
            return 0;
        }

        // ---------------------------------------------------------------- browser path
        public static WebOut BrowserProbe(string url, int timeoutMs)
        {
            WebOut o = new WebOut();
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                              + "(KHTML, like Gecko) Chrome/124.0 Safari/537.36";
                req.Accept = "text/html,application/xhtml+xml,*/*;q=0.8";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.AllowAutoRedirect = true;
                req.MaximumAutomaticRedirections = 5;
                req.KeepAlive = false;
                // Proxy intentionally left unset: .NET then uses the IE settings
                // (including PAC), which is the same route the browser takes.
                try
                {
                    using (HttpWebResponse r = (HttpWebResponse)req.GetResponse())
                    {
                        o.Ok = true;
                        o.Status = (int)r.StatusCode;
                        o.FinalUrl = r.ResponseUri == null ? "" : r.ResponseUri.ToString();
                    }
                }
                catch (WebException we)
                {
                    HttpWebResponse r = we.Response as HttpWebResponse;
                    if (r != null)
                    {
                        o.Ok = true;
                        o.Status = (int)r.StatusCode;
                        o.FinalUrl = r.ResponseUri == null ? "" : r.ResponseUri.ToString();
                    }
                    else o.Err = WebText(we);
                }
            }
            catch (Exception ex) { o.Err = Util.Short(ex.Message); }
            return o;
        }

        // ---------------------------------------------------------------- local info
        public static void GetSystemDns(Obs o)
        {
            try
            {
                List<string> withGw = new List<string>();
                List<string> all = new List<string>();
                string gwAdapter = "";
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    IPInterfaceProperties props = ni.GetIPProperties();
                    bool hasGw = false;
                    try { hasGw = props.GatewayAddresses.Count > 0; } catch { }
                    foreach (IPAddress d in props.DnsAddresses)
                    {
                        string s = d.ToString();
                        if (!all.Contains(s)) all.Add(s);
                        if (hasGw && !withGw.Contains(s))
                        {
                            withGw.Add(s);
                            if (gwAdapter.Length == 0) gwAdapter = ni.Name;
                        }
                    }
                }
                List<string> pick = withGw.Count > 0 ? withGw : all;
                o.SysDns = string.Join(", ", pick.ToArray());
                o.SysDnsAdapter = gwAdapter;
            }
            catch { }
        }

        public static string GetPublicIp()
        {
            string[] urls = new string[] { "http://ip.3322.net/", "http://myip.ipip.net/" };
            foreach (string u in urls)
            {
                try
                {
                    string r = SimpleHttp(u, 2500);
                    if (r == null) continue;
                    r = r.Trim();
                    int idx = r.IndexOf("IP：");
                    if (idx < 0) idx = r.IndexOf("IP:");
                    if (idx >= 0) r = r.Substring(idx + 3).Trim();
                    int sp = r.IndexOfAny(new char[] { ' ', '\r', '\n', ',' });
                    if (sp > 0) r = r.Substring(0, sp);
                    IPAddress tmp;
                    if (IPAddress.TryParse(r, out tmp) && !tmp.IsIPv6LinkLocal) return tmp.ToString();
                }
                catch { }
            }
            return "";
        }

        public static string SimpleHttp(string url, int timeoutMs)
        {
            try
            {
                Uri u = new Uri(url);
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect(u.Host, u.Port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) return null;
                    c.EndConnect(ar);
                    c.ReceiveTimeout = timeoutMs;
                    using (NetworkStream ns = c.GetStream())
                    {
                        string path = u.PathAndQuery.Length == 0 ? "/" : u.PathAndQuery;
                        string req = "GET " + path + " HTTP/1.0\r\nHost: " + u.Host
                                   + "\r\nUser-Agent: site-diag\r\nConnection: close\r\n\r\n";
                        byte[] b = Encoding.ASCII.GetBytes(req);
                        ns.Write(b, 0, b.Length);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            byte[] buf = new byte[2048];
                            int n;
                            while ((n = ns.Read(buf, 0, buf.Length)) > 0)
                            {
                                ms.Write(buf, 0, n);
                                if (ms.Length > 4096) break;
                            }
                            string all = Encoding.UTF8.GetString(ms.ToArray());
                            int p = all.IndexOf("\r\n\r\n");
                            return p >= 0 ? all.Substring(p + 4) : all;
                        }
                    }
                }
            }
            catch { return null; }
        }

        public static string DetectVpn()
        {
            string[] keys = new string[] { "atrust", "sangfor", "深信服", "anyconnect", "forticlient",
                                           "zscaler", "easyconnect", "openvpn", "wireguard", "tailscale",
                                           "zerotier", "proxifier", "proxycap", "netskope", "pulse secure" };
            List<string> found = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string desc = ni.Description ?? "";
                    string low = (desc + " " + (ni.Name ?? "")).ToLowerInvariant();
                    foreach (string k in keys)
                    {
                        if (low.IndexOf(k) >= 0)
                        {
                            if (!found.Contains(desc.Trim())) found.Add(desc.Trim());
                            break;
                        }
                    }
                }
            }
            catch { }
            return string.Join("; ", found.ToArray());
        }

        public static bool IsPrivateDns(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (string raw in s.Split(','))
            {
                string t = raw.Trim();
                if (t.StartsWith("10.") || t.StartsWith("192.168.")) return true;
                if (t.StartsWith("172.16.") || t.StartsWith("172.17.") || t.StartsWith("172.18.") || t.StartsWith("172.19.")) return true;
                if (t.StartsWith("172.2") || t.StartsWith("172.3")) return true;
                if (t.StartsWith("fd") || t.StartsWith("fec0")) return true;
            }
            return false;
        }
    }

    // ================================================================== DNS client
    internal static class DnsQuery
    {
        public static DnsAnswer Resolve(string server, string name, int qtype, int timeoutMs)
        {
            DnsAnswer r = new DnsAnswer();
            r.Server = server;
            byte[] q = BuildQuery(name, qtype);
            try
            {
                using (UdpClient udp = new UdpClient())
                {
                    udp.Client.ReceiveTimeout = timeoutMs;
                    udp.Client.SendTimeout = timeoutMs;
                    IPEndPoint srv = new IPEndPoint(IPAddress.Parse(server), 53);
                    udp.Send(q, q.Length, srv);
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref remote);
                    Parse(resp, r);
                    if ((resp[2] & 0x02) != 0)
                    {
                        byte[] tcp = TcpQuery(server, q, timeoutMs);
                        if (tcp != null)
                        {
                            DnsAnswer t = new DnsAnswer();
                            t.Server = server;
                            Parse(tcp, t);
                            return t;
                        }
                    }
                }
            }
            catch (SocketException) { r.Timeout = true; r.Error = L.T("无应答", "no reply"); }
            catch (Exception ex) { r.Timeout = true; r.Error = ex.Message; }
            return r;
        }

        private static byte[] TcpQuery(string server, byte[] query, int timeoutMs)
        {
            try
            {
                using (TcpClient c = new TcpClient())
                {
                    IAsyncResult ar = c.BeginConnect(server, 53, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) return null;
                    c.EndConnect(ar);
                    c.ReceiveTimeout = timeoutMs;
                    using (NetworkStream ns = c.GetStream())
                    {
                        byte[] len = new byte[] { (byte)(query.Length >> 8), (byte)(query.Length & 0xFF) };
                        ns.Write(len, 0, 2);
                        ns.Write(query, 0, query.Length);
                        byte[] l2 = ReadExactly(ns, 2);
                        if (l2 == null) return null;
                        int n = (l2[0] << 8) | l2[1];
                        return ReadExactly(ns, n);
                    }
                }
            }
            catch { return null; }
        }

        private static byte[] ReadExactly(Stream s, int n)
        {
            byte[] buf = new byte[n];
            int off = 0;
            while (off < n)
            {
                int r = s.Read(buf, off, n - off);
                if (r <= 0) return null;
                off += r;
            }
            return buf;
        }

        private static byte[] BuildQuery(string name, int qtype)
        {
            List<byte> p = new List<byte>();
            p.Add(0x5A); p.Add(0x3C);
            p.Add(0x01); p.Add(0x00);
            p.Add(0x00); p.Add(0x01);
            p.Add(0x00); p.Add(0x00);
            p.Add(0x00); p.Add(0x00);
            p.Add(0x00); p.Add(0x00);
            foreach (string lab in name.TrimEnd('.').Split('.'))
            {
                byte[] bs = Encoding.ASCII.GetBytes(lab);
                p.Add((byte)bs.Length);
                p.AddRange(bs);
            }
            p.Add(0x00);
            p.Add((byte)(qtype >> 8)); p.Add((byte)(qtype & 0xFF));
            p.Add(0x00); p.Add(0x01);
            return p.ToArray();
        }

        private static void Parse(byte[] b, DnsAnswer r)
        {
            if (b.Length < 12) { r.Timeout = true; r.Error = L.T("应答过短", "short response"); return; }
            r.Rcode = b[3] & 0x0F;
            int qd = (b[4] << 8) | b[5];
            int an = (b[6] << 8) | b[7];
            int pos = 12;
            for (int i = 0; i < qd; i++)
            {
                ReadName(b, ref pos);
                pos += 4;
                if (pos > b.Length) return;
            }
            for (int i = 0; i < an; i++)
            {
                if (pos >= b.Length) return;
                ReadName(b, ref pos);
                if (pos + 10 > b.Length) return;
                int type = (b[pos] << 8) | b[pos + 1]; pos += 2;
                pos += 2;
                pos += 4;
                int rdlen = (b[pos] << 8) | b[pos + 1]; pos += 2;
                if (pos + rdlen > b.Length) return;
                if (type == 1 && rdlen == 4)
                    r.A.Add(new IPAddress(new byte[] { b[pos], b[pos + 1], b[pos + 2], b[pos + 3] }).ToString());
                else if (type == 28 && rdlen == 16)
                {
                    byte[] v = new byte[16];
                    Array.Copy(b, pos, v, 0, 16);
                    r.Aaaa.Add(new IPAddress(v).ToString());
                }
                else if (type == 5)
                {
                    int rp = pos;
                    r.Cname.Add(ReadName(b, ref rp));
                }
                pos += rdlen;
            }
        }

        private static string ReadName(byte[] b, ref int pos)
        {
            StringBuilder sb = new StringBuilder();
            int p = pos;
            bool jumped = false;
            int guard = 0;
            while (guard++ < 64)
            {
                if (p >= b.Length) break;
                int len = b[p];
                if (len == 0)
                {
                    p++;
                    if (!jumped) pos = p;
                    break;
                }
                if ((len & 0xC0) == 0xC0)
                {
                    if (p + 1 >= b.Length) break;
                    int ptr = ((len & 0x3F) << 8) | b[p + 1];
                    if (!jumped) { pos = p + 2; jumped = true; }
                    p = ptr;
                    continue;
                }
                p++;
                if (p + len > b.Length) break;
                if (sb.Length > 0) sb.Append('.');
                sb.Append(Encoding.ASCII.GetString(b, p, len));
                p += len;
            }
            return sb.ToString();
        }
    }

    internal static class Util
    {
        public static string Short(string s)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            if (s.Length <= 60) return s;
            return s.Substring(0, 57) + "...";
        }
    }
}