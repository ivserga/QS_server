import json
from datetime import datetime

path = r"D:\VsProjects\QScalpWPF_Api_Feed\Datasets\clusters.NVDA..20260603.020017.json"
with open(path, encoding="utf-8") as f:
    doc = json.load(f)

bars = []
for c in doc["clusters"]:
    t = datetime.fromisoformat(c["start_time"])
    bars.append(
        {
            "i": c["index"],
            "t": t,
            "o": c["open_price_int"],
            "h": c["max_price_int"],
            "l": c["min_price_int"],
            "cl": c["close_price_int"],
            "vol": c["total_volume"],
            "delta": c["delta"],
        }
    )


def fwd(idx, n):
    j = idx + n
    if j >= len(bars):
        return None
    return bars[j]["cl"] - bars[idx]["cl"]


def fmt(p):
    return p / 100


by_time = {b["t"].strftime("%Y-%m-%d %H:%M:%S"): i for i, b in enumerate(bars)}

signals = [
    ("BreakoutDown", "2026-06-02 17:15:30", "down"),
    ("BreakoutDown", "2026-06-02 17:17:30", "down"),
    ("BreakoutDown", "2026-06-02 17:20:00", "down"),
    ("BreakoutDown", "2026-06-02 17:23:00", "down"),
    ("BreakoutDown", "2026-06-02 17:25:00", "down"),
    ("BreakoutDown", "2026-06-02 17:32:00", "down"),
    ("BreakoutDown", "2026-06-02 17:33:30", "down"),
    ("BreakoutDown", "2026-06-02 17:35:00", "down"),
    ("BreakoutDown", "2026-06-02 17:38:00", "down"),
    ("BreakoutDown", "2026-06-02 17:51:30", "down"),
    ("BreakoutUp", "2026-06-02 17:56:00", "up"),
    ("BreakoutUp", "2026-06-02 17:59:30", "up"),
    ("Distribution", "2026-06-02 17:20:30", "down"),
    ("Distribution", "2026-06-02 17:28:00", "down"),
    ("Distribution", "2026-06-02 17:43:30", "down"),
    ("Distribution", "2026-06-02 17:46:00", "down"),
    ("Accumulation", "2026-06-02 17:55:30", "up"),
    ("Accumulation", "2026-06-02 17:59:30", "up"),
    ("BalanceAfterClimax", "2026-06-02 17:09:00", "neutral"),
    ("BalanceAfterClimax", "2026-06-02 17:25:30", "neutral"),
    ("VReversalDown", "2026-06-02 17:33:30", "down"),
    ("VReversalUp", "2026-06-02 17:55:30", "up"),
    ("DoubleBottom", "2026-06-02 17:56:00", "up"),
    ("HvnRejectUp", "2026-06-02 17:26:30", "up"),
    ("Legacy Resist", "2026-06-02 17:13:00", "down"),
    ("Legacy Resist", "2026-06-02 17:30:00", "down"),
    ("Legacy Resist", "2026-06-02 17:40:30", "down"),
    ("Legacy Resist", "2026-06-02 17:42:00", "down"),
    ("Legacy Resist", "2026-06-02 17:54:00", "down"),
    ("Legacy Resist", "2026-06-02 18:00:00", "down"),
    ("Legacy Support", "2026-06-02 17:58:30", "up"),
    ("Legacy AbsBuy", "2026-06-02 17:35:30", "up"),
    ("Legacy AbsBuy", "2026-06-02 17:36:30", "up"),
    ("Legacy AbsBuy", "2026-06-02 17:39:00", "up"),
    ("Legacy AbsBuy", "2026-06-02 17:52:30", "up"),
    ("Legacy AbsSell", "2026-06-02 17:57:30", "down"),
]

TH = 15  # ticks ~ $0.15

print(f"Bars: {len(bars)}")
print(
    f"Session: {fmt(bars[0]['o'])} -> {fmt(bars[-1]['cl'])} "
    f"({bars[-1]['cl'] - bars[0]['o']} ticks)"
)
print(f"Low: {fmt(min(b['l'] for b in bars))} High: {fmt(max(b['h'] for b in bars))}")
print()

false_list = []
ok_list = []
weak_list = []

for name, ts, exp in signals:
    i = by_time[ts]
    b = bars[i]
    f2, f5, f10 = fwd(i, 2), fwd(i, 5), fwd(i, 10)
    closes = [bars[j]["cl"] for j in range(i + 1, len(bars))]
    highs = [bars[j]["h"] for j in range(i + 1, len(bars))]
    lows = [bars[j]["l"] for j in range(i + 1, len(bars))]
    ref = b["cl"]
    max_up = max(h - ref for h in highs) if highs else 0
    max_dn = min(l - ref for l in lows) if lows else 0
    end_move = closes[-1] - ref if closes else 0

    if exp == "down":
        if max_up >= TH and (f5 is None or f5 > 8):
            v = "FALSE"
        elif end_move > 15:
            v = "FALSE*"
        elif max_dn <= -10:
            v = "OK"
        else:
            v = "weak"
    elif exp == "up":
        if max_dn <= -TH and (f5 is None or f5 < -8):
            v = "FALSE"
        elif end_move < -15:
            v = "FALSE*"
        elif max_up >= 10 or end_move >= 8:
            v = "OK"
        else:
            v = "weak"
    else:
        if abs(f10 or 0) > 25 or abs(end_move) > 25:
            v = "FALSE"
        elif abs(end_move) < 12 and abs(max_up) < 15 and abs(max_dn) < 15:
            v = "OK"
        else:
            v = "weak"

    row = (
        f"{v:6} {name:18} {ts} @{fmt(ref):.2f} "
        f"f2={f2} f5={f5} f10={f10} up={max_up} dn={max_dn} end={end_move}"
    )
    print(row)
    if v.startswith("FALSE"):
        false_list.append((name, ts, row))
    elif v == "OK":
        ok_list.append(name)
    else:
        weak_list.append((name, ts))

print("\n=== FALSE POSITIVES ===")
for x in false_list:
    print(x[2])

print(f"\nCount FALSE: {len(false_list)}, OK: {len(ok_list)}, weak: {len(weak_list)}")
