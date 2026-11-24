// Program.cs — CLI, checks, verdict, output.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Microsoft.Win32;

namespace SiteDiag
{
    /// <summary>Language state. Every user-visible string goes through T(zh, en).</summary>
    internal static class L
    {
        public static bool En;
        public static string T(string zh, string en) { return En ? en : zh; }
    }

    internal static class UI
    {
        public static bool Color = true;
        public static StringBuilder Log = new StringBuilder();

        public static void Set(ConsoleColor c)
        {
            if (Color) Console.ForegroundColor = c;
        }

        public static void Reset()
        {
            if (Color) Console.ResetColor();
        }

        public static int Width(string s)
        {
            int w = 0;
            foreach (char c in s) w += (c > 127 ? 2 : 1);
            return w;
        }

        public static string Pad(string s, int width)
        {
            StringBuilder b = new StringBuilder(s);
            int cur = Width(s);
            while (cur < width) { b.Append(' '); cur++; }
            return b.ToString();
        }

        public static void Say(string text, ConsoleColor color)
        {
            Set(color);
            Console.WriteLine(text);
            Reset();
            Log.AppendLine(text);
        }

        public static void Plain(string text)
        {
            Console.WriteLine(text);
            Log.AppendLine(text);
        }

        public static void Row(string label, string value, ConsoleColor color)
        {
            Set(ConsoleColor.Gray);
            Console.Write("  " + Pad(label, 22));
            Console.Write(" ");
            Reset();
            Set(color);
            Console.WriteLine(value);
            Reset();
            Log.AppendLine("  " + Pad(label, 22) + " " + value);
        }

        public static void Note(string text)
        {
            Set(ConsoleColor.DarkGray);
            Console.WriteLine("       " + text);
            Reset();
            Log.AppendLine("       " + text);
        }

        public static void Section(int index, int total, string title)
        {
            Console.WriteLine();
            Set(ConsoleColor.DarkCyan);
            Console.WriteLine("[" + index + "/" + total + "] " + title);
            Reset();
            Log.AppendLine();
            Log.AppendLine("[" + index + "/" + total + "] " + title);
        }
    }

    internal static class Program
    {
        private const string Version = "2.0";
        private static readonly string[] PublicDns = new string[] { "223.5.5.5", "119.29.29.29", "114.114.114.114", "8.8.8.8" };

        private static void Main(string[] args)
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }

            string target = null;
            bool color = true;
            bool langSet = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "-h" || a == "--help") { L.En = true; PrintHelp(); return; }
                else if (a == "--version") { Console.WriteLine("site-diag " + Version); return; }
                else if (a == "--no-color") color = false;
                else if (a == "--lang" && i + 1 < args.Length)
                {
                    string v = args[++i].ToLowerInvariant();
                    L.En = (v == "en" || v == "english");
                    langSet = true;
                }
                else if (a == "--en") { L.En = true; langSet = true; }
                else if (a == "--zh") { L.En = false; langSet = true; }
                else if (target == null && !a.StartsWith("-")) target = a;
            }

            UI.Color = color;
            if (!langSet)
            {
                if (target == null) AskLanguage();   // interactive: ask on the very first screen
                else L.En = true;                     // scripted: English by default, use --zh to override
            }

            Banner();

            if (target != null)
            {
                int code = Run(target);
                Console.WriteLine();
                Environment.ExitCode = code;
                if (IsInteractive()) Pause();
                return;
            }

            while (true)
            {
                Console.WriteLine();
                UI.Set(ConsoleColor.Cyan);
                Console.Write(L.T("请输入域名或网址（回车退出）> ", "Enter a domain or URL (Enter to quit) > "));
                UI.Reset();
                string s = Console.ReadLine();
                if (s == null || s.Trim().Length == 0) break;
                Run(s.Trim());
                if (!IsInteractive()) break;
            }
        }

        private static bool IsInteractive()
        {
            try { return !Console.IsInputRedirected; } catch { return true; }
        }

        private static void Pause()
        {
            UI.Set(ConsoleColor.DarkGray);
            Console.Write(L.T("按任意键关闭窗口 ...", "Press any key to close ..."));
            UI.Reset();
            try { Console.ReadKey(true); } catch { }
        }

        /// <summary>First screen: bilingual, English first and default.</summary>
        private static void AskLanguage()
        {
            Console.WriteLine();
            Console.WriteLine("==================================================================");
            Console.WriteLine("  site-diag " + Version + "  ·  Website Access Diagnostics");
            Console.WriteLine("==================================================================");
            Console.WriteLine();
            Console.WriteLine("  Select language / 请选择语言");
            Console.WriteLine();
            Console.WriteLine("    1)  English");
            Console.WriteLine("    2)  中文");
            Console.WriteLine();
            Console.WriteLine("  Press Enter for English.");
            Console.WriteLine("  直接按回车 = 英文，输入 2 再回车 = 中文。");
            Console.WriteLine();

            for (int attempt = 0; attempt < 3; attempt++)
            {
                Console.Write("  Choice / 选择 [1]: ");
                string s = Console.ReadLine();
                if (s == null) { L.En = true; break; }
                s = s.Trim();
                if (s.Length == 0 || s == "1") { L.En = true; break; }
                if (s == "2") { L.En = false; break; }
                Console.WriteLine("  ?  1 = English    2 = 中文");
            }
            Console.WriteLine();
        }

        private static void Banner()
        {
            Console.WriteLine();
            UI.Set(ConsoleColor.DarkCyan);
            Console.WriteLine("==================================================================");
            UI.Set(ConsoleColor.Cyan);
            Console.WriteLine("  site-diag " + Version + "  ·  " + L.T("网站访问诊断", "Website Access Diagnostics"));
            UI.Set(ConsoleColor.DarkCyan);
            Console.WriteLine("==================================================================");
            UI.Reset();
            UI.Set(ConsoleColor.DarkGray);
            Console.WriteLine("  " + L.T("判断“打不开”到底卡在本机、公司出口、运营商，还是网站那边。",
                                        "Find out where a broken site actually fails:"));
            Console.WriteLine("  " + L.T("结果会自动保存为 txt，可直接发给 IT。",
                                        "this PC, the office gateway, the ISP, or the site itself."));
            UI.Reset();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("site-diag " + Version + "  ·  Website Access Diagnostics / 网站访问诊断");
            Console.WriteLine();
            Console.WriteLine("Usage / 用法:");
            Console.WriteLine("  site-diag.exe                        interactive, asks for a domain");
            Console.WriteLine("  site-diag.exe <domain-or-url>        run once and exit");
            Console.WriteLine("  site-diag.exe <target> --lang en     output in English");
            Console.WriteLine("  site-diag.exe <target> --lang zh     output in Chinese / 输出中文");
            Console.WriteLine();
            Console.WriteLine("Options / 选项:");
            Console.WriteLine("  --lang en|zh   set output language / 设置输出语言");
            Console.WriteLine("  --no-color     disable ANSI colours / 关闭彩色");
            Console.WriteLine("  --version      print version / 显示版本");
            Console.WriteLine("  -h, --help     this help / 显示帮助");
            Console.WriteLine();
            Console.WriteLine("Exit codes: 0 reachable, 1 problem found, 2 usage error.");
            Console.WriteLine("退出码：0 正常，1 发现问题，2 用法错误。");
        }

        // ---------------------------------------------------------------- one run
        private static int Run(string input)
        {
            UI.Log = new StringBuilder();
            Obs o = new Obs();
            o.Raw = input;
            ParseInput(o);

            if (o.Host.Length == 0)
            {
                Console.WriteLine(L.T("输入为空或格式不正确。", "Empty or invalid input."));
                return 2;
            }

            Console.WriteLine();
            UI.Set(ConsoleColor.White);
            Console.WriteLine(L.T("目标：", "Target: ") + o.Host);
            UI.Set(ConsoleColor.DarkGray);
            Console.WriteLine(L.T("时间：", "Time:   ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            UI.Reset();
            UI.Log.AppendLine(L.T("目标：", "Target: ") + o.Host + "   " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            StepLocal(o);
            StepDns(o);
            StepConnect(o);
            StepTls(o);
            StepHttp(o);
            StepBrowser(o);

            Verdict v = Decide(o);
            PrintVerdict(v);
            SaveLog(o);

            return v.ExitCode;
        }

        private static void ParseInput(Obs o)
        {
            string s = o.Raw.Trim();
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            else if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(8);
            int cut = s.IndexOfAny(new char[] { '/', '?', '#' });
            if (cut >= 0) s = s.Substring(0, cut);
            int colon = s.LastIndexOf(':');
            if (colon > 0 && s.IndexOf(']') < 0)
            {
                int p;
                if (int.TryParse(s.Substring(colon + 1), out p) && p > 0 && p < 65536) s = s.Substring(0, colon);
            }
            o.Host = s.Trim();
            IPAddress ip;
            if (IPAddress.TryParse(o.Host, out ip)) { o.IsIp = true; o.Ip = ip; }
        }

        // ---------------------------------------------------------------- 1
        private static void StepLocal(Obs o)
        {
            UI.Section(1, 6, L.T("本机环境", "Local environment"));

            string hostsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            try
            {
                if (File.Exists(hostsPath))
                {
                    foreach (string ln in File.ReadAllLines(hostsPath))
                    {
                        string t = ln.Trim();
                        if (t.Length == 0 || t.StartsWith("#")) continue;
                        string[] parts = t.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (string.Equals(parts[i], o.Host, StringComparison.OrdinalIgnoreCase))
                            {
                                o.HasHosts = true;
                                o.HostsLine = t;
                            }
                        }
                    }
                }
            }
            catch { }

            if (o.HasHosts)
                UI.Row(L.T("hosts 文件", "hosts file"), L.T("发现记录：", "entry found: ") + o.HostsLine, ConsoleColor.Red);
            else
                UI.Row(L.T("hosts 文件", "hosts file"), L.T("无相关记录", "no entry"), ConsoleColor.Green);

            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k != null)
                    {
                        object en = k.GetValue("ProxyEnable");
                        if (en != null && Convert.ToInt32(en) != 0)
                        {
                            o.ProxyOn = true;
                            object sv = k.GetValue("ProxyServer");
                            o.ProxyStr = sv == null ? "" : sv.ToString();
                        }
                        object pac = k.GetValue("AutoConfigURL");
                        if (pac != null) o.PacUrl = pac.ToString();
                    }
                }
            }
            catch { }

            if (o.ProxyOn)
                UI.Row(L.T("系统代理", "system proxy"), L.T("已启用 ", "enabled ") + o.ProxyStr, ConsoleColor.Yellow);
            else if (o.PacUrl.Length > 0)
                UI.Row(L.T("系统代理", "system proxy"), L.T("PAC 自动配置 ", "PAC autoconfig ") + o.PacUrl, ConsoleColor.Yellow);
            else
                UI.Row(L.T("系统代理", "system proxy"), L.T("未启用", "disabled"), ConsoleColor.Green);

            o.VpnInfo = Net.DetectVpn();
            if (o.VpnInfo.Length > 0)
                UI.Row(L.T("VPN / 安全客户端", "VPN / security client"),
                       o.VpnInfo + L.T("（浏览器流量可能经它转发）", " (browser traffic may go through it)"),
                       ConsoleColor.Yellow);

            Net.GetSystemDns(o);
            UI.Row(L.T("本机 DNS 服务器", "DNS servers"),
                   o.SysDns.Length == 0 ? L.T("（未获取到）", "(not detected)") : o.SysDns,
                   ConsoleColor.Gray);
            if (Net.IsPrivateDns(o.SysDns))
                UI.Note(L.T("其中包含内网 DNS —— 拦截也可能发生在公司内网 DNS 上，不一定是运营商",
                            "includes an internal DNS — blocking may happen on the company resolver, not the ISP"));

            o.PublicIp = Net.GetPublicIp();
            if (o.PublicIp.Length > 0)
                UI.Row(L.T("出口公网 IP", "public egress IP"),
                       o.PublicIp + L.T("   （报障时必填）", "   (needed when opening a ticket)"),
                       ConsoleColor.Gray);
        }

        // ---------------------------------------------------------------- 2
        private static void StepDns(Obs o)
        {
            UI.Section(2, 6, L.T("域名解析", "DNS resolution"));

            if (o.IsIp)
            {
                UI.Row(L.T("解析", "resolution"), L.T("输入的是 IP 地址，跳过 DNS 检查", "input is an IP address, skipping DNS"), ConsoleColor.Gray);
                return;
            }

            try
            {
                foreach (IPAddress ip in Dns.GetHostAddresses(o.Host))
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) o.SysA.Add(ip.ToString());
            }
            catch { o.SysDnsFail = true; }

            if (o.SysA.Count == 0)
            {
                o.SysDnsFail = true;
                UI.Row(L.T("本机 DNS", "system DNS"), L.T("解析失败（超时 / 域名不存在）", "failed (timeout / NXDOMAIN)"), ConsoleColor.Red);
            }
            else UI.Row(L.T("本机 DNS", "system DNS"), string.Join(", ", o.SysA.ToArray()), ConsoleColor.White);

            for (int i = 0; i < PublicDns.Length; i++)
            {
                DnsAnswer a = DnsQuery.Resolve(PublicDns[i], o.Host, 1, 3000);
                o.Pub[PublicDns[i]] = a;
                string shown;
                ConsoleColor c;
                if (a.Timeout) { shown = L.T("无应答", "no reply"); c = ConsoleColor.DarkGray; }
                else if (a.Rcode == 3) { shown = L.T("域名不存在(NXDOMAIN)", "NXDOMAIN"); c = ConsoleColor.Red; }
                else if (a.A.Count == 0) { shown = L.T("无 A 记录", "no A record"); c = ConsoleColor.Yellow; }
                else
                {
                    shown = string.Join(", ", a.A.ToArray());
                    c = ConsoleColor.Green;
                    o.PubHasAnswer = true;
                    foreach (string ip in a.A) if (!o.PubA.Contains(ip)) o.PubA.Add(ip);
                }
                string label;
                if (i == 0) label = L.T("阿里 223.5.5.5", "AliDNS 223.5.5.5");
                else if (i == 1) label = L.T("腾讯 119.29.29.29", "DNSPod 119.29.29.29");
                else if (i == 2) label = "114DNS";
                else label = L.T("谷歌 8.8.8.8", "Google 8.8.8.8");
                UI.Row(label, shown, c);
            }

            o.DnsMismatch = 0;
            if (o.PubA.Count > 0 && o.SysA.Count > 0)
            {
                bool same = false;
                foreach (string a in o.SysA) if (o.PubA.Contains(a)) same = true;
                o.DnsMismatch = same ? 1 : 2;
                if (same) UI.Row(L.T("解析一致性", "consistency"), L.T("与公共 DNS 一致", "matches public DNS"), ConsoleColor.Green);
                else UI.Row(L.T("解析一致性", "consistency"), L.T("与本机 DNS 结果不一致！", "DIFFERS from system DNS!"), ConsoleColor.Red);
            }

            if (o.PubA.Count > 0)
            {
                DnsAnswer q = DnsQuery.Resolve("223.5.5.5", o.Host, 28, 2500);
                o.HasAaaa = q.Aaaa.Count > 0;
                if (o.HasAaaa) UI.Row(L.T("IPv6 记录", "IPv6 (AAAA)"), string.Join(", ", q.Aaaa.ToArray()), ConsoleColor.Gray);
                else UI.Row(L.T("IPv6 记录", "IPv6 (AAAA)"), L.T("无 AAAA 记录", "no AAAA record"), ConsoleColor.DarkGray);
            }

            string pick = null;
            if (o.SysA.Count > 0) pick = o.SysA[0];
            else if (o.PubA.Count > 0) pick = o.PubA[0];
            if (pick != null)
            {
                IPAddress ip;
                if (IPAddress.TryParse(pick, out ip)) o.Ip = ip;
            }
        }

        // ---------------------------------------------------------------- 3
        private static void StepConnect(Obs o)
        {
            UI.Section(3, 6, L.T("连通性", "Connectivity"));
            if (o.Ip == null)
            {
                UI.Row(L.T("连通性", "connectivity"), L.T("没有可用的 IP，跳过", "no usable IP, skipping"), ConsoleColor.DarkGray);
                return;
            }

            int rtt;
            o.PingOk = Net.PingHost(o.Ip, 1500, out rtt);
            o.PingMs = rtt;
            if (o.PingOk)
                UI.Row("ping " + o.Ip, L.T("通  ", "ok  ") + rtt + " ms", ConsoleColor.Green);
            else
                UI.Row("ping " + o.Ip, L.T("不通（仅供参考，CDN 常禁 ping）", "no reply (informational; CDNs often block ICMP)"), ConsoleColor.DarkGray);

            int ms;
            string err;
            o.Tcp443 = Net.TcpProbe(o.Ip, 443, 4000, out ms, out err);
            o.Tcp443Ms = ms; o.Tcp443Err = err ?? "";
            if (o.Tcp443) UI.Row("TCP 443", L.T("连通  ", "open  ") + ms + " ms", ConsoleColor.Green);
            else UI.Row("TCP 443", L.T("不通：", "failed: ") + o.Tcp443Err, ConsoleColor.Red);

            o.Tcp80 = Net.TcpProbe(o.Ip, 80, 4000, out ms, out err);
            o.Tcp80Ms = ms; o.Tcp80Err = err ?? "";
            if (o.Tcp80) UI.Row("TCP 80", L.T("连通  ", "open  ") + ms + " ms", ConsoleColor.Green);
            else UI.Row("TCP 80", L.T("不通：", "failed: ") + o.Tcp80Err, ConsoleColor.Red);
        }

        // ---------------------------------------------------------------- 4
        private static void StepTls(Obs o)
        {
            UI.Section(4, 6, L.T("HTTPS 握手：带域名 / 不带域名", "HTTPS handshake: with / without SNI"));
            if (o.Ip == null)
            {
                UI.Row("HTTPS", L.T("没有可用的 IP，跳过", "no usable IP, skipping"), ConsoleColor.DarkGray);
                return;
            }
            if (!o.Tcp443 && !o.Tcp80)
            {
                UI.Row("HTTPS", L.T("TCP 都连不上，跳过 TLS", "TCP unreachable, skipping TLS"), ConsoleColor.DarkGray);
                return;
            }
            if (!o.Tcp443)
            {
                UI.Row("HTTPS", L.T("443 端口不通，无法握手", "port 443 closed, cannot handshake"), ConsoleColor.Red);
                return;
            }

            TlsOut firstOk = null, firstFail = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                TlsOut t = Net.TlsProbe(o.Ip, 443, o.IsIp ? o.Ip.ToString() : o.Host, 6000);
                if (t.Ok) { o.TlsOkCount++; if (firstOk == null) firstOk = t; }
                else
                {
                    o.TlsFailCount++;
                    if (firstFail == null) { firstFail = t; o.TlsFailErr = t.Err; }
                }
                if (o.TlsOkCount >= 2) break;
            }
            o.TlsSni = firstOk != null ? firstOk : firstFail;

            string cnt = "";
            if (o.TlsOkCount + o.TlsFailCount > 1)
                cnt = L.T("（3 次里成功 " + o.TlsOkCount + " 次）", " (" + o.TlsOkCount + "/" + (o.TlsOkCount + o.TlsFailCount) + " attempts ok)");

            if (o.TlsSni.Ok)
            {
                if (o.TlsFailCount > 0)
                    UI.Row(L.T("带域名握手", "with SNI"), L.T("时通时不通", "intermittent") + cnt, ConsoleColor.Yellow);
                else
                    UI.Row(L.T("带域名握手", "with SNI"), L.T("成功  ", "ok  ") + o.TlsSni.Protocol + cnt, ConsoleColor.Green);

                UI.Note(L.T("证书：", "cert: ") + Util.Short(o.TlsSni.Subject)
                        + L.T("  颁发者：", "  issuer: ") + Util.Short(o.TlsSni.Issuer)
                        + L.T("  有效期至：", "  expires: ") + o.TlsSni.NotAfter);

                if (o.TlsSni.CertTrusted)
                    UI.Row(L.T("证书校验", "certificate"), L.T("通过（与浏览器一致）", "trusted (same as browser)"), ConsoleColor.Green);
                else
                    UI.Row(L.T("证书校验", "certificate"), L.T("失败：", "FAILED: ") + o.TlsSni.CertIssue, ConsoleColor.Red);
            }
            else
            {
                UI.Row(L.T("带域名握手", "with SNI"),
                       L.T("连续 " + o.TlsFailCount + " 次都失败：", "failed " + o.TlsFailCount + " times: ") + o.TlsSni.Err,
                       ConsoleColor.Red);

                o.TlsNoSni = Net.TlsProbe(o.Ip, 443, o.Ip.ToString(), 6000);
                if (o.TlsNoSni.Ok)
                    UI.Row(L.T("不带域名握手", "without SNI"),
                           L.T("成功（说明 IP 和端口正常，问题出在域名）", "ok (IP and port are fine — the hostname is the problem)"),
                           ConsoleColor.Yellow);
                else
                    UI.Row(L.T("不带域名握手", "without SNI"), L.T("也失败：", "also failed: ") + o.TlsNoSni.Err, ConsoleColor.Red);
            }
        }

        // ---------------------------------------------------------------- 5
        private static void StepHttp(Obs o)
        {
            UI.Section(5, 6, L.T("HTTP 返回", "HTTP response"));
            if (o.Ip == null) return;
            string hostHeader = o.IsIp ? o.Ip.ToString() : o.Host;

            if (o.Tcp80)
            {
                o.Http80 = Net.HttpGet(o.Ip, 80, false, null, hostHeader, 6000);
                if (o.Http80.Ok)
                {
                    UI.Row(L.T("80 端口", "port 80"), o.Http80.StatusLine, StatusColor(o.Http80.Status));
                    if (o.Http80.Location.Length > 0) UI.Note("Location: " + o.Http80.Location);
                    if (o.Http80.Server.Length > 0) UI.Note("Server: " + o.Http80.Server);
                    if (LooksLikeIntercept(o.Http80))
                        UI.Row(L.T("80 端口内容", "port 80 body"),
                               L.T("疑似拦截页（含备案/拦截关键词）", "looks like an interception page"), ConsoleColor.Red);
                }
                else UI.Row(L.T("80 端口", "port 80"), L.T("请求失败：", "request failed: ") + o.Http80.Err, ConsoleColor.Red);
            }

            if (o.Tcp443 && o.TlsSni != null && o.TlsSni.Ok)
            {
                o.Http443 = Net.HttpGet(o.Ip, 443, true, o.IsIp ? o.Ip.ToString() : o.Host, hostHeader, 8000);
                if (o.Http443.Ok)
                {
                    UI.Row(L.T("443 端口", "port 443"), o.Http443.StatusLine, StatusColor(o.Http443.Status));
                    if (o.Http443.Server.Length > 0) UI.Note("Server: " + o.Http443.Server);
                }
                else UI.Row(L.T("443 端口", "port 443"), L.T("请求失败：", "request failed: ") + o.Http443.Err, ConsoleColor.Red);
            }
        }

        private static ConsoleColor StatusColor(int s)
        {
            if (s >= 200 && s < 400) return ConsoleColor.Green;
            if (s == 403 || s == 401) return ConsoleColor.Yellow;
            if (s >= 500) return ConsoleColor.Red;
            return ConsoleColor.White;
        }

        private static bool LooksLikeIntercept(HttpOut h)
        {
            string s = ((h.Body ?? "") + " " + (h.Location ?? "")).ToLowerInvariant();
            string[] keys = new string[] { "备案", "未备案", "拦截", "屏蔽", "blocked", "not filed", "beian", "intercept" };
            foreach (string k in keys) if (s.IndexOf(k) >= 0) return true;
            return false;
        }

        // ---------------------------------------------------------------- 6
        private static void StepBrowser(Obs o)
        {
            UI.Section(6, 6, L.T("浏览器路径（走系统代理 / PAC，和浏览器尽量一致）",
                                 "Browser path (via system proxy / PAC, the route your browser takes)"));
            if (o.IsIp)
            {
                UI.Row(L.T("浏览器路径", "browser path"), L.T("输入的是 IP，跳过", "input is an IP, skipping"), ConsoleColor.DarkGray);
                return;
            }

            WebOut w = Net.BrowserProbe("https://" + o.Host + "/", 12000);
            o.Browser = w;

            if (w != null && w.Ok)
            {
                UI.Row(L.T("系统 HTTP 栈", "system HTTP stack"),
                       L.T("HTTP " + w.Status + "   （与浏览器走同一条路）", "HTTP " + w.Status + "   (same route as the browser)"),
                       ConsoleColor.Green);
                if (!string.IsNullOrEmpty(w.FinalUrl)) UI.Note(L.T("最终地址：", "final URL: ") + Util.Short(w.FinalUrl));
            }
            else
            {
                UI.Row(L.T("系统 HTTP 栈", "system HTTP stack"),
                       L.T("失败：", "failed: ") + (w == null ? L.T("未知原因", "unknown") : w.Err),
                       ConsoleColor.Red);
            }
        }

        // ---------------------------------------------------------------- verdict
        private static Verdict Decide(Obs o)
        {
            Verdict v = new Verdict();

            if (o.HasHosts)
            {
                v.Title = L.T("本机 hosts 文件被手工改过", "The hosts file on this PC has been edited");
                v.Owner = L.T("本机", "This PC");
                v.Color = ConsoleColor.Yellow; v.ExitCode = 1;
                v.Evidence.Add(L.T("hosts 中有记录：", "hosts contains: ") + o.HostsLine);
                v.Actions.Add(L.T("用记事本以管理员身份打开 C:\\Windows\\System32\\drivers\\etc\\hosts，删掉这一行",
                                  "Open C:\\Windows\\System32\\drivers\\etc\\hosts as admin and delete that line"));
                v.Actions.Add(L.T("执行 ipconfig /flushdns 后重试", "Run ipconfig /flushdns and try again"));
                return v;
            }

            if (!o.IsIp && o.DnsMismatch == 2)
            {
                v.Title = L.T("域名解析被篡改（本机 DNS、内网 DNS 或运营商 DNS）",
                              "DNS tampering — a resolver returns a different IP");
                v.Owner = L.T("运营商 DNS 或公司内网 DNS", "ISP DNS or company DNS");
                v.Color = ConsoleColor.Red; v.ExitCode = 1;
                v.Evidence.Add(L.T("本机解析到：", "system DNS returned: ") + string.Join(", ", o.SysA.ToArray()));
                v.Evidence.Add(L.T("公共 DNS 解析到：", "public DNS returned: ") + string.Join(", ", o.PubA.ToArray()));
                v.Actions.Add(L.T("把电脑或路由器的 DNS 改成 223.5.5.5 和 119.29.29.29，再测一次",
                                  "Set DNS to 223.5.5.5 and 119.29.29.29, then run again"));
                v.Actions.Add(L.T("改完能打开 → 就是 DNS 层的问题", "If it works after the change, the problem is DNS"));
                return v;
            }

            if (!o.IsIp && o.SysDnsFail && !o.PubHasAnswer)
            {
                v.Title = L.T("域名解析不出来（域名可能过期或未正确解析）",
                              "Domain does not resolve (expired or missing records)");
                v.Owner = L.T("域名 / DNS", "Domain / DNS");
                v.Color = ConsoleColor.Yellow; v.ExitCode = 1;
                v.Evidence.Add(L.T("本机 DNS 解析失败，公共 DNS 也查不到 A 记录",
                                   "system resolver failed and public resolvers return no A record"));
                v.Actions.Add(L.T("先确认域名是否续费、解析是否被删除", "Check that the domain is paid up and the records exist"));
                v.Actions.Add(L.T("用 whois 或域名控制台核对解析记录", "Verify records with whois or the DNS console"));
                return v;
            }

            if (!o.IsIp && o.SysDnsFail && o.PubHasAnswer)
            {
                v.Title = L.T("只有你的 DNS 解析失败（运营商或内网 DNS 异常）",
                              "Only your resolver fails (ISP or internal DNS)");
                v.Owner = L.T("运营商 DNS 或公司内网 DNS", "ISP DNS or company DNS");
                v.Color = ConsoleColor.Red; v.ExitCode = 1;
                v.Evidence.Add(L.T("本机 DNS 解析失败", "system resolver failed"));
                v.Evidence.Add(L.T("公共 DNS 能解析：", "public DNS resolves: ") + string.Join(", ", o.PubA.ToArray()));
                v.Actions.Add(L.T("把 DNS 改成 223.5.5.5 后重测", "Set DNS to 223.5.5.5 and retest"));
                return v;
            }

            if (o.Ip == null)
            {
                v.Title = L.T("无法确定目标 IP，证据不足", "Cannot determine the target IP — not enough evidence");
                v.Owner = L.T("未知", "Unknown");
                v.Color = ConsoleColor.Yellow; v.ExitCode = 2;
                v.Actions.Add(L.T("换一个网络（手机热点）再跑一次做对照", "Run it again on another network (phone hotspot)"));
                return v;
            }

            if (IsTlsEnvError(o))
            {
                v.Title = L.T("本机 HTTPS 组件异常，握手做不下去（不是网络问题）",
                              "Local TLS stack failed — this is not a network problem");
                v.Owner = L.T("本机（Windows 安全组件 / 权限）", "This PC (Windows security stack / permissions)");
                v.Color = ConsoleColor.Yellow; v.ExitCode = 1;
                v.Evidence.Add(L.T("TCP 443 可以连通，说明 IP 和端口都没问题", "TCP 443 connects, so IP and port are fine"));
                v.Evidence.Add(L.T("但 TLS 握手在本机就失败了：", "but TLS fails locally: ") + o.TlsSni.Err);
                if (o.Tcp80) v.Evidence.Add(L.T("80 端口正常，说明链路是通的", "port 80 works, so the path is fine"));
                v.Actions.Add(L.T("用浏览器直接打开该网址做对照", "Open the site in a browser to compare"));
                v.Actions.Add(L.T("换个普通用户会话或重启后重试", "Retry in a normal user session or after a reboot"));
                v.Actions.Add(L.T("检查系统时间是否准确", "Check that the system clock is correct"));
                return v;
            }

            bool anyTcp = o.Tcp443 || o.Tcp80;

            if (!anyTcp)
            {
                if (!o.PingOk)
                {
                    v.Title = L.T("目标 IP 不可达：IP 层或线路被阻断", "Target IP unreachable — IP-level or route-level blocking");
                    v.Owner = L.T("运营商 或 公司出口设备", "ISP or the office gateway");
                    v.Color = ConsoleColor.Red; v.ExitCode = 1;
                    v.Evidence.Add(L.T("ping 不通，100% 丢包", "ping gets no reply, 100% loss"));
                    v.Evidence.Add("TCP 443: " + o.Tcp443Err);
                    v.Evidence.Add("TCP 80:  " + o.Tcp80Err);
                    v.Actions.Add(L.T("执行 tracert -d " + o.Host + " 并截图，看断在第几跳",
                                      "Run tracert -d " + o.Host + " and check which hop it dies at"));
                    v.Actions.Add(L.T("换手机热点再跑一次：热点正常 → 责任在运营商或公司出口",
                                      "Run again on a phone hotspot: if it works, the ISP or gateway is responsible"));
                }
                else
                {
                    v.Title = L.T("IP 可达但端口不通：端口被拦截或服务没开",
                                  "IP reachable but ports are closed — filtered or not listening");
                    v.Owner = L.T("运营商 / 公司防火墙 / 服务器", "ISP / company firewall / server");
                    v.Color = ConsoleColor.Red; v.ExitCode = 1;
                    v.Evidence.Add(L.T("ping 通（" + o.PingMs + " ms）", "ping ok (" + o.PingMs + " ms)"));
                    v.Evidence.Add("TCP 443: " + o.Tcp443Err);
                    v.Evidence.Add("TCP 80:  " + o.Tcp80Err);
                    v.Actions.Add(L.T("换手机热点复测：热点通 → 是本网络拦了端口",
                                      "Retest on a hotspot: if it works, this network is filtering the ports"));
                }
                return v;
            }

            if (!o.Tcp443 && o.Tcp80)
            {
                v.Title = L.T("443 端口不通，但 80 端口通", "Port 443 is blocked while 80 is open");
                v.Owner = L.T("运营商 / 公司防火墙 / 服务器", "ISP / company firewall / server");
                v.Color = ConsoleColor.Red; v.ExitCode = 1;
                v.Evidence.Add(L.T("TCP 80 连通", "TCP 80 connects"));
                v.Evidence.Add("TCP 443: " + o.Tcp443Err);
                v.Actions.Add(L.T("换手机热点复测 443", "Retest port 443 on a hotspot"));
                return v;
            }

            if (o.Http80 != null && o.Http80.Ok && o.Http80.Location.Length > 0)
            {
                string loc = o.Http80.Location;
                bool foreign = loc.IndexOf(o.Host, StringComparison.OrdinalIgnoreCase) < 0;
                if (foreign && LooksLikeIntercept(o.Http80))
                {
                    v.Title = L.T("HTTP 被替换成拦截页（备案 / 违规拦截）", "HTTP was replaced by an interception page");
                    v.Owner = L.T("运营商", "ISP");
                    v.Color = ConsoleColor.Red; v.ExitCode = 1;
                    v.Evidence.Add(L.T("80 端口返回 ", "port 80 returned ") + o.Http80.StatusLine);
                    v.Evidence.Add(L.T("跳转到非本站地址：", "redirects to a foreign host: ") + loc);
                    v.Actions.Add(L.T("把跳转后的整页截图作为证据", "Screenshot the redirected page as evidence"));
                    v.Actions.Add(L.T("联系 CDN / 接入商核实备案状态", "Ask the CDN or hosting provider about the filing status"));
                    return v;
                }
            }

            if (o.TlsSni != null && !o.TlsSni.Ok && o.TlsNoSni != null && o.TlsNoSni.Ok)
            {
                v.Title = L.T("域名层阻断：通 IP、不通域名（SNI / Host 过滤）",
                              "Domain-level blocking — the IP works, the hostname does not");
                v.Owner = L.T("运营商 或 公司出口设备", "ISP or the office gateway");
                v.Color = ConsoleColor.Red; v.ExitCode = 1;
                v.Evidence.Add(L.T("IP 的 443 端口可以建立 TCP 连接", "TCP to port 443 succeeds"));
                v.Evidence.Add(L.T("带域名握手失败：", "handshake with SNI failed: ") + o.TlsSni.Err);
                v.Evidence.Add(L.T("不带域名直接连 IP，握手成功", "handshake without SNI succeeded"));
                v.Actions.Add(L.T("换手机热点复测：热点正常 → 就是你这条线路在拦这个域名",
                                  "Retest on a hotspot: if it works, this line is blocking the hostname"));
                v.Actions.Add(L.T("用 Wireshark 过滤 tcp.port==443，看 ClientHello 之后是否收到 RST",
                                  "Capture with Wireshark on tcp.port==443 and look for a RST after ClientHello"));
                return v;
            }

            if (o.TlsSni != null && !o.TlsSni.Ok && (o.TlsNoSni == null || !o.TlsNoSni.Ok))
            {
                v.Title = L.T("HTTPS 连接被中断（TCP 能连，握手过不去）", "HTTPS interrupted — TCP connects, the handshake does not");
                v.Owner = L.T("运营商 / 公司设备 / 服务器", "ISP / network device / server");
                v.Color = ConsoleColor.Red; v.ExitCode = 1;
                v.Evidence.Add(L.T("TCP 443 连通，但 TLS 握手失败", "TCP 443 connects but TLS fails"));
                v.Evidence.Add(L.T("带域名：", "with SNI: ") + o.TlsSni.Err);
                if (o.TlsNoSni != null) v.Evidence.Add(L.T("不带域名：", "without SNI: ") + o.TlsNoSni.Err);
                v.Actions.Add(L.T("换手机热点复测，区分是本网络还是服务器",
                                  "Retest on a hotspot to separate the network from the server"));
                return v;
            }

            if (o.Http443 != null && o.Http443.Status == 403)
            {
                v.Title = L.T("服务器 / CDN 拒绝了你的出口 IP（403）", "Server or CDN rejected your egress IP (403)");
                v.Owner = L.T("CDN 或源站 WAF（不是运营商）", "CDN or origin WAF (not the ISP)");
                v.Color = ConsoleColor.Yellow; v.ExitCode = 1;
                v.Evidence.Add("HTTPS 403 Forbidden");
                v.Evidence.Add(L.T("出口公网 IP：", "egress IP: ") + (o.PublicIp.Length > 0 ? o.PublicIp : L.T("未获取到", "unknown")));
                v.Actions.Add(L.T("把出口 IP 发给 CDN 技术支持，请他们查是否被 WAF 拉黑",
                                  "Send the egress IP to the CDN and ask if it is blocked by the WAF"));
                v.Actions.Add(L.T("换手机热点对比：热点返回 200 → 就是按 IP 封的",
                                  "Compare with a hotspot: if it returns 200, you are blocked by IP"));
                return v;
            }

            if (o.Http443 != null && o.Http443.Status >= 500)
            {
                v.Title = L.T("网站服务器出错（5xx）", "Server-side error (5xx)");
                v.Owner = L.T("网站源站 / CDN", "Origin server / CDN");
                v.Color = ConsoleColor.Yellow; v.ExitCode = 1;
                v.Evidence.Add(L.T("HTTPS 返回 ", "HTTPS returned ") + o.Http443.StatusLine);
                v.Actions.Add(L.T("换一个网络复测，若同样 5xx，说明与你的网络无关",
                                  "Retest on another network; the same 5xx means it is not your network"));
                return v;
            }

            if (o.TlsOkCount > 0 && o.TlsFailCount > 0)
            {
                v.Title = L.T("间歇性阻断：同一目标有时能连、有时被丢",
                              "Intermittent blocking — sometimes it connects, sometimes it is dropped");
                v.Owner = L.T("运营商链路（被干扰站点的典型特征）或公司设备限流",
                              "ISP backbone (typical of interference) or gateway rate limiting");
                v.Color = ConsoleColor.Red; v.ExitCode = 1;
                v.Evidence.Add(L.T("带域名握手试了 " + (o.TlsOkCount + o.TlsFailCount) + " 次：成功 " + o.TlsOkCount + " 次、失败 " + o.TlsFailCount + " 次",
                                   "handshake attempted " + (o.TlsOkCount + o.TlsFailCount) + " times: " + o.TlsOkCount + " ok, " + o.TlsFailCount + " failed"));
                if (o.TlsFailErr.Length > 0) v.Evidence.Add(L.T("失败时的报错：", "failure: ") + o.TlsFailErr);
                v.Actions.Add(L.T("换手机热点连续跑 3 次做对照", "Run three times on a hotspot for comparison"));
                v.Actions.Add(L.T("把成功/失败次数一起报给运营商", "Report the success/failure counts to the ISP"));
                return v;
            }

            bool rawOk = o.TlsSni != null && o.TlsSni.Ok
                      && ((o.Http443 != null && o.Http443.Status > 0 && o.Http443.Status < 400)
                          || (o.Http80 != null && o.Http80.Status > 0 && o.Http80.Status < 400));
            bool browserOk = o.Browser != null && o.Browser.Ok;

            if (rawOk && o.TlsSni != null && !o.TlsSni.CertTrusted)
            {
                v.Title = L.T("HTTPS 证书不可信（浏览器会拦，链路是通的）",
                              "Certificate not trusted — the browser will block it, the path is fine");
                v.Owner = L.T("中间人（运营商/公司网关）或证书本身有问题", "Interception (ISP / gateway) or a bad certificate");
                v.Color = ConsoleColor.Red; v.ExitCode = 1;
                v.Evidence.Add(L.T("TLS 握手成功，但证书校验没通过：", "TLS succeeds but the certificate fails validation: ") + o.TlsSni.CertIssue);
                v.Evidence.Add(L.T("证书：", "cert: ") + Util.Short(o.TlsSni.Subject) + L.T("，颁发者：", "  issuer: ") + Util.Short(o.TlsSni.Issuer));
                v.Actions.Add(L.T("在浏览器里点开证书，看颁发者是不是你认识的 CA",
                                  "Open the certificate in the browser and check the issuer"));
                v.Actions.Add(L.T("换手机热点再跑一次：热点证书正常 → 是本网络在替换证书",
                                  "Rerun on a hotspot: a normal certificate there means this network replaces it"));
                return v;
            }

            if (rawOk && !browserOk)
            {
                v.Title = L.T("本机直连是通的，但浏览器走的路径被拦了",
                              "Direct connection works, but the browser's route is blocked");
                v.Owner = L.T("本机代理 / VPN / 零信任客户端 或 浏览器策略",
                              "Local proxy / VPN / zero-trust client, or a browser policy");
                v.Color = ConsoleColor.Red; v.ExitCode = 1;
                v.Evidence.Add(L.T("绕过代理直连目标：TCP 通、TLS 通、HTTP 正常",
                                   "Bypassing the proxy: TCP ok, TLS ok, HTTP ok"));
                v.Evidence.Add(L.T("但走系统代理/PAC 的请求失败：", "but the proxy/PAC request failed: ")
                               + (o.Browser == null ? L.T("未知", "unknown") : o.Browser.Err));
                if (o.ProxyOn) v.Evidence.Add(L.T("检测到系统代理已启用：", "system proxy enabled: ") + o.ProxyStr);
                if (o.PacUrl.Length > 0) v.Evidence.Add(L.T("检测到 PAC 自动配置：", "PAC autoconfig: ") + o.PacUrl);
                if (o.VpnInfo.Length > 0) v.Evidence.Add(L.T("检测到 VPN/安全客户端：", "VPN/security client: ") + o.VpnInfo);
                v.Actions.Add(L.T("关掉 VPN / 零信任客户端后再测", "Quit the VPN / zero-trust client and retest"));
                v.Actions.Add(L.T("在“Internet 选项 → 连接 → 局域网设置”里关掉代理和 PAC",
                                  "Turn off the proxy and PAC in Internet Options > Connections > LAN settings"));
                v.Actions.Add(L.T("浏览器里关掉安全 DNS / DoH 和 QUIC 再试",
                                  "Disable secure DNS (DoH) and QUIC in the browser and retry"));
                return v;
            }

            if (rawOk && browserOk)
            {
                v.Title = L.T("未发现链路阻断，网站可以正常访问", "No blocking found — the site is reachable");
                v.Owner = L.T("无（若浏览器仍打不开，是本机浏览器的问题）",
                              "None (if your browser still fails, the problem is the browser)");
                v.Color = ConsoleColor.Green; v.ExitCode = 0;
                v.Evidence.Add(L.T("DNS 一致，TCP 通，TLS 通，HTTP 返回正常", "DNS consistent, TCP ok, TLS ok, HTTP ok"));
                v.Evidence.Add(L.T("走系统代理的请求也成功，与浏览器路径一致",
                                   "the proxied request also succeeded — same route as the browser"));
                v.Actions.Add(L.T("用无痕窗口重开，排除缓存和插件", "Retry in a private window to rule out cache and extensions"));
                v.Actions.Add(L.T("检查系统时间是否准确", "Check that the system clock is correct"));
                return v;
            }

            v.Title = L.T("未能确定原因，证据不足", "Inconclusive — not enough evidence");
            v.Owner = L.T("未知", "Unknown");
            v.Color = ConsoleColor.Yellow; v.ExitCode = 2;
            v.Actions.Add(L.T("换手机热点再跑一次做对照", "Run it again on a phone hotspot for comparison"));
            return v;
        }

        private static bool IsTlsEnvError(Obs o)
        {
            if (o.TlsSni == null || o.TlsSni.Ok) return false;
            string e = (o.TlsSni.Err ?? "").ToLowerInvariant();
            return e.IndexOf("凭证") >= 0 || e.IndexOf("credential") >= 0
                || e.IndexOf("0x8009030e") >= 0 || e.IndexOf("安全包") >= 0;
        }

        private static void PrintVerdict(Verdict v)
        {
            Console.WriteLine();
            UI.Set(ConsoleColor.DarkCyan);
            Console.WriteLine("==================================================================");
            UI.Set(v.Color);
            Console.WriteLine("  " + L.T("结论：", "VERDICT:  ") + v.Title);
            UI.Set(ConsoleColor.White);
            Console.WriteLine("  " + L.T("责任方：", "OWNER:    ") + v.Owner);
            UI.Set(ConsoleColor.DarkCyan);
            Console.WriteLine("==================================================================");
            UI.Reset();

            UI.Log.AppendLine();
            UI.Log.AppendLine("==================================================================");
            UI.Log.AppendLine("  " + L.T("结论：", "VERDICT:  ") + v.Title);
            UI.Log.AppendLine("  " + L.T("责任方：", "OWNER:    ") + v.Owner);
            UI.Log.AppendLine("==================================================================");

            if (v.Evidence.Count > 0)
            {
                UI.Set(ConsoleColor.Gray);
                Console.WriteLine("  " + L.T("依据：", "Evidence:"));
                UI.Reset();
                UI.Log.AppendLine("  " + L.T("依据：", "Evidence:"));
                foreach (string e in v.Evidence)
                {
                    UI.Set(ConsoleColor.Gray);
                    Console.WriteLine("   - " + e);
                    UI.Reset();
                    UI.Log.AppendLine("   - " + e);
                }
            }

            if (v.Actions.Count > 0)
            {
                Console.WriteLine();
                UI.Set(ConsoleColor.Cyan);
                Console.WriteLine("  " + L.T("下一步：", "Next steps:"));
                UI.Reset();
                UI.Log.AppendLine();
                UI.Log.AppendLine("  " + L.T("下一步：", "Next steps:"));
                int i = 1;
                foreach (string a in v.Actions)
                {
                    UI.Set(ConsoleColor.White);
                    Console.WriteLine("   " + i + ") " + a);
                    UI.Reset();
                    UI.Log.AppendLine("   " + i + ") " + a);
                    i++;
                }
            }

            Console.WriteLine();
            UI.Set(ConsoleColor.DarkGray);
            string tip = L.T("提示：要和“手机热点”的结果对照才有意义 —— 同一台电脑，换网络再跑一次。",
                             "Tip: compare with a phone hotspot — same PC, different network.");
            Console.WriteLine("  " + tip);
            UI.Reset();
            UI.Log.AppendLine("  " + tip);
        }

        private static void SaveLog(Obs o)
        {
            string safe = o.Host.Replace(':', '_').Replace('/', '_');
            string name = "site-diag-" + safe + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt";
            string exeDir = "";
            try { exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName); } catch { }

            List<string> dirs = new List<string>();
            try { dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)); } catch { }
            if (!string.IsNullOrEmpty(exeDir)) dirs.Add(exeDir);
            try { dirs.Add(Path.GetTempPath()); } catch { }

            foreach (string d in dirs)
            {
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
                try
                {
                    string path = Path.Combine(d, name);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("site-diag " + Version + "  ·  Website Access Diagnostics / 网站访问诊断");
                    sb.AppendLine("lang: " + (L.En ? "en" : "zh"));
                    sb.AppendLine("input: " + o.Raw);
                    sb.AppendLine();
                    sb.Append(UI.Log.ToString());
                    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                    Console.WriteLine();
                    UI.Set(ConsoleColor.DarkGray);
                    Console.WriteLine("  " + L.T("结果已保存：", "Report saved: ") + path);
                    UI.Reset();
                    return;
                }
                catch { }
            }
            UI.Set(ConsoleColor.DarkGray);
            Console.WriteLine("  " + L.T("（结果没能保存成文件，请直接截屏）", "(could not save the report, please screenshot)"));
            UI.Reset();
        }
    }
}