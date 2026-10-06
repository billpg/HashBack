# 🦔 HashBack Wallboard

<script>setTimeout(function() { location.reload(); }, 15000);</script>
- Refreshed <span id='now'/> UTC
- Up since <span id='startedAt'/> UTC (<span id='uptime'/>)
- Auto-refreshes every 15s

## 📈 Hello Requests

- **<span id='helloTotal'/>** total
- **<span id='helloSuccess'/>** succeeded (<span id='successRate'/>%)
- **<span id='hello24h'/>** in the last 24h

<table><tr><th>Outcome</th><th>Count</th></tr>
<tr id='helloByOutcome'><td id='helloByOutcome.Outcome'/><td id='helloByOutcome.Count'/></tr>
</table>

## 🫱 Outbound GETs

**<span id='outboundTotal'/>** total

<table><tr><th>Source</th><th>Target Domain</th><th>Count</th></tr>
<tr id='outboundBySourceAndHost'><td id='outboundBySourceAndHost.Source'/><td id='outboundBySourceAndHost.TargetHost'/><td id='outboundBySourceAndHost.Count'/></tr>
</table>

## 🔑 Hash Store

**<span id='hashTotal'/>** stored &middot; **<span id='hashRetrieved'/>** retrieved at least once

<table><tr><th>Caller IP</th><th>Count</th></tr>
<tr id='hashesByCallerAndSource'><td id='hashesByCallerAndSource.CallerIp'/><td id='hashesByCallerAndSource.Count'/></tr>
</table>

## 🕒 Recent Hello Activity

<table><tr><th>Time (UTC)</th><th>Caller</th><th>Outcome</th></tr>
<tr id='recent'><td id='r.RequestedAt'/><td id='r.CallerIp'/><td id='r.Outcome'/></tr>
</table>