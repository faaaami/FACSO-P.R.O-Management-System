param(
  [Parameter(Mandatory = $true)][string]$WsUrl,
  [string]$Base = 'http://localhost:5173'
)
$ErrorActionPreference = 'Stop'

$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ws.ConnectAsync([Uri]$WsUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null

function Send-Cdp([int]$id, [string]$method, $params) {
  $payload = @{ id = $id; method = $method; params = $params } | ConvertTo-Json -Depth 63 -Compress
  $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
  $seg = New-Object 'System.ArraySegment[byte]' -ArgumentList (, $bytes)
  $ws.SendAsync($seg, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
}

function Recv-Cdp([int]$id) {
  for ($i = 0; $i -lt 500; $i++) {
    try {
      $stream = New-Object System.IO.MemoryStream
      $buffer = New-Object 'byte[]' 65536
      do {
        $bseg = New-Object 'System.ArraySegment[byte]' -ArgumentList (, $buffer)
        $res = $ws.ReceiveAsync($bseg, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
        if ($res.Count -gt 0) { $stream.Write($buffer, 0, $res.Count) }
      } while (-not $res.EndOfMessage)
      $data = [Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json
      if ($data.id -eq $id) { return $data }
    } catch { break }
  }
  return $null
}

$inject = @'
(function(){
  if (window.__rrOn) return;
  window.__rrOn = 1;
  window.__rrErrs = [];
  window.__rrNet = [];
  var ce = console.error.bind(console);
  console.error = function(){ try{ var s = Array.prototype.map.call(arguments, function(a){ return String((a && a.message) || a); }).join(' '); window.__rrErrs.push('CONSOLE ' + s); }catch(_){} return ce.apply(console, arguments); };
  window.addEventListener('error', function(e){ try{ window.__rrErrs.push('ERR ' + String((e && e.message) || e.error)); }catch(_){} });
  window.addEventListener('unhandledrejection', function(e){ try{ window.__rrErrs.push('REJ ' + String((e.reason && e.reason.message) || e.reason)); }catch(_){} });
  var o = XMLHttpRequest.prototype.open;
  XMLHttpRequest.prototype.open = function(m, u){ try{ window.__rrNet.push(String(m).toUpperCase() + ' ' + String(u)); }catch(_){} return o.apply(this, arguments); };
  if (window.fetch) {
    var f = window.fetch.bind(window);
    window.fetch = function(){ try{ window.__rrNet.push('FETCH ' + String(arguments[0])); }catch(_){} return f.apply(window, arguments); };
  }
})();
'@

# Replay capture: re-encode current curl() of localStorage keys we care about
function Snap-Eval([string]$label) {
  $e = '(function(){ var h1 = document.querySelector("h1"); var rows = (window.__rrNet || []).filter(function(u){ return /\/api\/v1\//.test(u); }); return { label: "' + $label + '", href: location.href, path: location.pathname, h1: h1 ? h1.textContent : null, apis: rows, apiCount: rows.length, errs: (window.__rrErrs || []) }; })()'
  $null = Send-Cdp 1 'Runtime.evaluate' @{ expression = $e; returnByValue = $true }
  $r = Recv-Cdp 1
  if ($r -and $r.result.result.value) { try { return ($r.result.result.value | ConvertFrom-Json) } catch {} }
  return @{ label = $label; href = 'NO-RESP'; apiCount = 0 }
}

function Nav([string]$url, [int]$waitMs = 2500) {
  $null = Send-Cdp 2 'Page.navigate' @{ url = $url }
  $null = Recv-Cdp 2
  Start-Sleep -Milliseconds $waitMs
}

function Eval-Only([string]$expr) {
  $null = Send-Cdp 3 'Runtime.evaluate' @{ expression = $expr; returnByValue = $true }
  $null = Recv-Cdp 3
}

function Login-As([string]$email, [string]$password) {
  $setv = '(function(){ function setVal(el, v){ var proto = Object.getPrototypeOf(el); var d = Object.getOwnPropertyDescriptor(proto, "value"); if (d && d.set) { d.set.call(el, v); } else { el.value = v; } el.dispatchEvent(new Event("input", { bubbles: true })); } var em = document.getElementById("email"); var pw = document.getElementById("password"); if (!em) return "NOEMAILFIELD"; if (!pw) return "NOPASSWORDFIELD"; setVal(em, "' + $email + '"); setVal(pw, "' + $password + '"); var form = document.querySelector("form"); if (form && form.requestSubmit) { form.requestSubmit(); return "SUBMITTED"; } return "NOSUBMIT"; })()'
  Eval-Only $setv
}

$results = @()

# ---- Prerequisite: browser at /login, logged out; confirm storage cleared ----
Nav "$Base/login" 1500
$results += Snap-Eval 'pre-logged-out-login'

# ---- Scenario A: logged-out "/" -> /login (no loop) ----
$null = Eval-Only '(function(){ ["accessToken","refreshToken","user"].forEach(function(k){ localStorage.removeItem(k); }); return "CLEARED"; })()'
Nav "$Base/"
Start-Sleep -Milliseconds 1500
$results += Snap-Eval 'A1-root-loggedout'
Start-Sleep -Milliseconds 1800
$results += Snap-Eval 'A2-root-loggedout-recheck'
