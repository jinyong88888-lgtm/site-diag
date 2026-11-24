# site-diag

**A single-file, no-install Windows CLI that tells you _where_ a website actually fails.**

"Can't open the site" is usually reported as one symptom, but it has at least four very different owners:
your PC, the office gateway, the ISP, or the site itself. `site-diag` runs six checks, correlates the
results, and prints a verdict with an owner and next steps — instead of a pile of raw output.

[中文说明](README.zh-CN.md)

```
==================================================================
  site-diag 2.0  ·  Website Access Diagnostics
==================================================================

[1/6] Local environment
  hosts file             no entry
  system proxy           disabled
  DNS servers            223.5.5.5, 114.114.114.114
  public egress IP       203.0.113.24   (needed when opening a ticket)

[2/6] DNS resolution
  system DNS             203.0.113.9
  AliDNS 223.5.5.5       203.0.113.9
  DNSPod 119.29.29.29    203.0.113.9
  114DNS                 203.0.113.9
  Google 8.8.8.8         203.0.113.9
  consistency            matches public DNS

[3/6] Connectivity
  ping 203.0.113.9       open  41 ms
  TCP 443                open  34 ms
  TCP 80                 open  27 ms

[4/6] HTTPS handshake: with / without SNI
  with SNI               ok  Tls12 (2/2 attempts ok)
  certificate            trusted (same as browser)

[5/6] HTTP response
  port 80                HTTP/1.1 301 Moved Permanently
  port 443               HTTP/1.1 200 OK

[6/6] Browser path (via system proxy / PAC, the route your browser takes)
  system HTTP stack      HTTP 200   (same route as the browser)

==================================================================
  VERDICT:  No blocking found — the site is reachable
  OWNER:    None (if your browser still fails, the problem is the browser)
==================================================================
```

## Why this exists

Support tickets almost always start with "the site is down". The first hour is then spent establishing
basic facts the reporter cannot easily produce: does DNS resolve, does TCP connect, does TLS complete,
does the browser take a different route than `curl`.

`site-diag` automates that first hour. It is deliberately a **correlation** tool, not a packet capture
tool: the value is in the combination of checks, not in any single one.

## The six checks

| # | Check | What it isolates |
|---|-------|-----------------|
| 1 | Local environment | `hosts` overrides, system proxy, PAC, VPN / zero-trust clients, configured resolvers |
| 2 | DNS resolution | your resolver vs. AliDNS, DNSPod, 114, Google — a single differing answer is a strong signal |
| 3 | Connectivity | ICMP, TCP 443, TCP 80, with timings |
| 4 | HTTPS handshake | the same IP **with** and **without** SNI, three attempts, plus real certificate validation |
| 5 | HTTP response | status line, `Location`, `Server` — catches redirect-based interception pages |
| 6 | Browser path | an actual `HttpWebRequest` through the system proxy/PAC, i.e. the route the browser takes |

Check 4 is the core idea: if the handshake **without** SNI succeeds but **with** SNI fails, something on the
path is reading the hostname and dropping the connection — that is domain-level blocking, and it looks
completely different from an IP-level block.

Check 6 exists because the opposite false negative is easy to produce: a raw socket can bypass a proxy or a
zero-trust client that the browser cannot, so "the tool says it works, the browser says it doesn't" is a real
and common outcome. Both paths are measured and compared.

## Quick start

Download `site-diag.exe` from [Releases](../../releases), or build it yourself (see below).

```
site-diag.exe                      # interactive, asks for a domain
site-diag.exe example.com          # run once
site-diag.exe https://example.com/path?q=1
site-diag.exe example.com --lang en
```

The very first screen asks for a language, in both languages at once:

```
  Select language / 请选择语言

    1)  English
    2)  中文

  Press Enter for English.
```

English is the default, so a user who cannot read Chinese is never stuck at step zero.

## Verdicts

| Verdict | Owner |
|---------|-------|
| `No blocking found` | — |
| `DNS tampering — a resolver returns a different IP` | ISP / company DNS |
| `Domain does not resolve` | domain / DNS records |
| `The hosts file on this PC has been edited` | this PC |
| `Target IP unreachable` | ISP or office gateway |
| `IP reachable but ports are closed` | ISP / firewall / server |
| `Domain-level blocking — the IP works, the hostname does not` | ISP or gateway |
| `Intermittent blocking` | ISP backbone / rate limiting |
| `Certificate not trusted` | interception or a bad certificate |
| `Direct connection works, but the browser's route is blocked` | local proxy / VPN / zero-trust client |
| `Server or CDN rejected your egress IP (403)` | CDN / origin WAF |

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | reachable, no blocking found |
| `1` | a problem was found |
| `2` | usage error, or not enough evidence |

Useful for scripting: `site-diag.exe example.com --lang en || alert "site down"`

## Build from source

Requirements: Windows 7 SP1 or later, .NET Framework 4.5+. `csc.exe` ships with the framework, so no SDK,
no NuGet, no Visual Studio.

```bat
build.bat
```

or manually:

```bat
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:exe /out:site-diag.exe /win32icon:assets\site-diag.ico ^
  /codepage:65001 /optimize+ src\Net.cs src\Program.cs
```

CI builds every push on `windows-latest` and attaches the binary to a release when a `v*` tag is pushed.

## Privacy

No telemetry, no analytics, no network calls other than:

* DNS queries to the resolvers listed above,
* TCP/TLS/HTTP requests to the host **you** type,
* one plain-HTTP request to `ip.3322.net` (fallback `myip.ipip.net`) to show your egress IP.

Results are written to a local `site-diag-<host>-<timestamp>.txt` and nowhere else.

## First run: SmartScreen

The released binary is not code-signed, so Windows may show **"Windows protected your PC"** the first time
you run a *downloaded* copy. This is a reputation prompt, not a malware detection — the file carries an NTFS
`Zone.Identifier` stream (*Mark of the Web*) that browsers and mail clients attach on download. No mark, no
prompt: the same bytes compiled locally run silently.

| Option | How | When |
|--------|-----|------|
| Run anyway | *More info* → *Run anyway* | one-off |
| Remove the mark (recommended) | right-click → Properties → **Unblock**; or `unblock.bat site-diag.exe`; or `Unblock-File` in PowerShell | that file, permanently |
| Build it yourself | `build.bat` — a locally compiled exe never carries the mark | rolling out to many machines |

Copying via USB, a network share, or a corporate messenger usually drops the mark too.

An EV code-signing certificate is the only way to remove the prompt for *everyone*, and it costs a few
hundred dollars a year — rarely worth it for an internal tool.

## Limitations

* Windows only, IPv4 oriented.
* It cannot see QUIC / HTTP3 (UDP 443). If a browser fails on a site that works here, disabling QUIC is a
  good first test.
* Many CDNs drop ICMP, so `ping = no reply` is reported as informational, never as a fault on its own.
* A TLS failure caused by the local Schannel stack is reported as such, not blamed on the network.
* The released binary is unsigned, so Windows SmartScreen will warn on first run. Build it locally
  (`build.bat`) if that matters to you.

## License

MIT — see [LICENSE](LICENSE).