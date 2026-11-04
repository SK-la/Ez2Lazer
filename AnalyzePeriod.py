#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Ez 诊断 CSV 的周期性 / 相位分析。

用途见 docs/EZ-PERFORMANCE-HANDOFF.md §3.4：把「隔一会一次的不流畅」「判定全程规律正弦波动」
这类**体感**变成可读数字 —— 是否存在 2–4 s 周期、这个周期由哪一路信号驱动、各路信号彼此谁先谁后。

为什么能跨探针互相关：三个探针（frame-stall / press-latency / judgment）的 `WallMs` 都取自
`EzJudgmentDiagnostics.WallClockMs`（进程启动即开始的同一个 Stopwatch），因此**同一局**产出的
三份 CSV 的墙钟是同一原点、同一单位，可以直接对齐比较。

口径（读数字前必须先看这几条）：
  * 事件型序列（judgment 12/s、press 12/s）是**不规则采样**，脚本按 `--dt` 线性插值到统一网格。
    对 1 Hz 以下的内容，20 ms 网格足够；但它会把高于 Nyquist（1/dt/2）的东西折进来，故插值前
    只做线性去趋势，不出更细的结论。
  * 帧型序列（2000 fps）**不能**点采样，否则 1 kHz 的噪声整体混叠；脚本先按 `--dt` 做块均值
    （Envelope 保留、逐帧噪声被平均掉），再对齐网格。
  * `--frame` 传进来的若不是全集（阈值 > 0），周期结论无效（尾部截断会伪造周期性）。脚本会读同名
    `.summary.txt` 检查并在这种情况下明确警告。
  * 两条会被**自动拒绝**的情况，不要试图绕过：
      ① 序列的 `RMS / 稳健σ(MAD) > 5` —— 方差由开局加载 / 退出那几个离群帧独占，带内「周期」其实是
         它们的余振（实测同一局 `ElapsedMs` 全段 15.7x、去掉首尾各 3s 后 2.6x，带内 RMS 3.294 → 0.026 ms）。
      ② 跨序列相位表的读数未越过**偶然水平阈值** —— 带通到 1.5–5 s 后独立样本只有约「跨度/周期」个，
         |r| 的偶然峰很高（零分布实测 p95 0.503）；工具按 0.05/对数反解家族性阈值并逐行标注。
      另外「定义性派生」的对（如 SpikeRate := elapsed ≥ 2×p50）直接剔除；纯重复的列干脆不采（见 §2.4.19）。

用法：
    python AnalyzePeriod.py diagnostics/judgment_20260924_222233.csv
    python AnalyzePeriod.py --judgment diagnostics/judgment_X.csv --frame diagnostics/framestall_X.csv --press diagnostics/presslatency_X.csv
    python AnalyzePeriod.py diagnostics/*.csv --trim 3,3 --band 1.5,5 --plot

    --trim A,B  先裁掉首尾各 A / B 秒再分析。找稳态周期**基本都该加**：开局加载与退出帧是已知的大离群点。
    --plot      输出 PNG；--windows 打印所有序列的滑窗明细；--only 只分析指定序列。

依赖：numpy（必需）、matplotlib（仅 --plot）。
"""

from __future__ import annotations

import argparse
import csv
import math
import os
import sys
from dataclasses import dataclass, replace

import numpy as np

SPARK = "▁▂▃▄▅▆▇█"

# 去趋势后「普通 RMS / 稳健 σ(MAD)」超过这个倍数，就认定方差由少数离群帧独占，带内结论不可用。
# 实测同一局的 `ElapsedMs`：全段 15.7x（被首个 141ms 帧占死），去掉首尾各 3s 后 3.2x。
OUTLIER_RATIO = 5.0


# --------------------------------------------------------------------------- 读入

def read_table(path: str) -> tuple[list[str], list[list[str]]]:
    with open(path, newline="", encoding="utf-8-sig") as handle:
        reader = csv.reader(handle)
        header = next(reader)
        rows = [row for row in reader if row and row[0].strip()]
    return header, rows


def col(header: list[str], name: str, rows: list[list[str]]) -> np.ndarray:
    """取一列成 float 数组；空字段与 'NaN' 一律记 NaN（探针用空串表示无值）。"""
    try:
        index = header.index(name)
    except ValueError:
        return np.full(len(rows), np.nan)
    out = np.empty(len(rows))
    for i, row in enumerate(rows):
        try:
            text = row[index].strip()
        except IndexError:
            out[i] = np.nan
            continue
        out[i] = np.nan if text == "" else float(text)
    return out


def detect_kind(header: list[str]) -> str:
    names = set(header)
    if {"ElapsedMs", "GcPauseDeltaMs", "SubtreeMs"} <= names:
        return "frame"
    if {"TimeOffset", "Drift"} <= names:
        return "judgment"
    if "PreColumnMs" in names and "FrameAgeMs" in names:
        return "press"
    return "unknown"


@dataclass
class Series:
    """一条待分析的 (t, y) 序列。mode: 'interp' 稀疏事件插值 / 'block' 密集帧块均值。

    valid=False 表示这条序列**不适合**做周期 / 相位结论（目前只有一种情况：frame 探针跑在
    阈值 > 0 的尾部模式下，块均值桶大量靠插值填补，会伪造出极强但完全虚假的低频相关）。
    """
    name: str
    t: np.ndarray
    y: np.ndarray
    mode: str
    unit: str
    source: str
    valid: bool = True
    # 若本条由其它序列**按定义派生**（例如 SpikeRate 就是 ElapsedMs >= 2*p50 的指示函数，
    # InterpRate 是 ΔInterpMs / ElapsedMs），记下来源名：它们之间的相关性是构造出来的，
    # 不是发现，相位表必须跳过。可以有多来源。
    derived_from: tuple[str, ...] = ()


def trim_series(series: Series, lo: float, hi: float) -> Series | None:
    """按绝对时间裁掉首尾过渡段。开局加载与退出帧是**已知**的大离群点（且不是用户抱怨的对象），
    但它们会让整段带内能量变成同一个脉冲的余振，因此周期/相位分析应把它们排除在外。"""
    keep = (series.t >= lo) & (series.t <= hi)
    if int(keep.sum()) < 8:
        return None
    return replace(series, t=series.t[keep], y=series.y[keep])


def build_series(path: str, table, full_capture: bool = True) -> list[Series]:
    header, rows = table
    kind = detect_kind(header)
    if kind == "unknown":
        return []
    t = col(header, "WallMs", rows) / 1000.0  # -> 秒
    out: list[Series] = []
    src = os.path.basename(path)

    if kind == "judgment":
        drift = col(header, "Drift", rows)
        offset = col(header, "TimeOffset", rows)
        # BassSource / InterpClock 两列已删（2026-09-25，docs/EZ-PERFORMANCE.md §2.4.19）：
        # BassSource ≡ GameTime − Drift − 15.000（常数 15 ms 是音频偏移），InterpClock ≡ GameTime。
        # 由此 AudioLag = BassSource − GameTime 与 Drift 去趋势后必然 r = −1.000，是恒等式不是发现，
        # 所以随列一起删掉，而不是继续以 derived 的形式列出来。
        out += [
            Series("Drift", t, drift, "interp", "ms", src),
            Series("TimeOffset", t, offset, "interp", "ms", src),
            Series("FrameElapsed", t, col(header, "FrameElapsed", rows), "interp", "ms", src),
        ]
    elif kind == "press":
        out += [
            Series("PreColumnMs", t, col(header, "PreColumnMs", rows), "interp", "ms", src),
            Series("ColumnMs", t, col(header, "ColumnMs", rows), "interp", "ms", src),
            Series("FrameAgeMs", t, col(header, "FrameAgeMs", rows), "interp", "ms", src),
        ]
    else:  # frame
        elapsed = col(header, "ElapsedMs", rows)
        gc = col(header, "GcPauseDeltaMs", rows)
        finite = elapsed[np.isfinite(elapsed)]
        p50 = float(np.median(finite)) if finite.size else float("nan")
        spike = (elapsed >= 2.0 * p50).astype(float) if finite.size else elapsed * np.nan
        out += [
            Series("ElapsedMs", t, elapsed, "block", "ms", src, full_capture),
            Series("SpikeRate", t, spike, "block", "1", src, full_capture,
                   derived_from=("ElapsedMs",)),
            Series("GcPauseDeltaMs", t, gc, "block", "ms", src, full_capture),
        ]

        # 音频时钟列（2026-09-24 追加；旧 CSV 没有这两列，故先看 header 再取）。
        if "AudioSrcMs" in header and "InterpMs" in header:
            audio = col(header, "AudioSrcMs", rows)
            interp = col(header, "InterpMs", rows)
            step = np.diff(audio, prepend=np.nan)

            with np.errstate(invalid="ignore", divide="ignore"):
                rate = np.diff(interp, prepend=np.nan) / elapsed

            # 帧长 >5ms 的帧本身不干净（卡顿 / 加载），其「速率」没有解释力，口径与探针摘要一致。
            rate = np.where(elapsed > 5, np.nan, rate)

            out += [
                # 音频源的逐帧步进：阶梯的直接证据（约 96% 的帧应为 0，其余等于一个固定步长）。
                Series("AudioStep", t, step, "block", "ms", src, full_capture),
                # InterpRate = ΔInterpMs / ElapsedMs ⇒ 与这两条都是定义性关系，不是发现。
                Series("InterpRate", t, rate, "block", "1", src, full_capture,
                       derived_from=("InterpMs", "ElapsedMs")),
            ]

    return out


def frame_capture_state(path: str) -> tuple[str, bool]:
    """读同名 summary；返回 (说明, 是否全集)。尾部 CSV 的周期 / 相位结论无效。"""
    summary = os.path.splitext(path)[0] + ".summary.txt"
    if not os.path.exists(summary):
        return "无 summary，无法确认是否全集 ⇒ 按尾部处理", False
    first = ""
    with open(summary, encoding="utf-8-sig") as handle:
        for line in handle:
            if "[EzFrameStall]" in line:
                first = line
                break
    if not first:
        return "summary 里没有 [EzFrameStall] 行 ⇒ 按尾部处理", False
    threshold = None
    for token in first.split():
        if token.startswith("threshold="):
            threshold = token[len("threshold="):]
    if threshold is None:
        return "summary 阈值缺失 ⇒ 按尾部处理", False
    try:
        value = float(threshold.rstrip("ms"))
    except ValueError:
        return f"summary 阈值不可解析（{threshold}）⇒ 按尾部处理", False
    if value <= 0:
        return f"全集（threshold={threshold}）✓", True
    return (f"**尾部**（threshold={threshold}，{threshold}）⇒ 帧序列不参与周期/相位结论，"
            "需抓全集重跑（`EZ_FRAME_PROBE_MS=0`，或临时把 `ThresholdMs` 默认改为 0）"), False


# --------------------------------------------------------------------------- 网格

def to_grid(series: Series, t0: float, dt: float, count: int) -> np.ndarray | None:
    mask = np.isfinite(series.t) & np.isfinite(series.y)
    t, y = series.t[mask], series.y[mask]
    if t.size < 8:
        return None

    if series.mode == "block":
        index = np.floor((t - t0) / dt).astype(np.int64)
        keep = (index >= 0) & (index < count)
        index, y = index[keep], y[keep]
        if index.size == 0:
            return None
        sums = np.bincount(index, weights=y, minlength=count)
        counts = np.bincount(index, minlength=count)
        grid = np.full(count, np.nan)
        filled = counts > 0
        grid[filled] = sums[filled] / counts[filled]
        valid = np.isfinite(grid)
        if valid.sum() < 8:
            return None
        return np.interp(np.arange(count), np.flatnonzero(valid), grid[valid])

    order = np.argsort(t)
    return np.interp(np.arange(count) * dt + t0, t[order], y[order])


# --------------------------------------------------------------------------- 数学

def detrend(y: np.ndarray) -> np.ndarray:
    x = np.arange(len(y), dtype=float)
    slope, intercept = np.polyfit(x, y, 1)
    return y - (slope * x + intercept)


def autocorr(y: np.ndarray, max_lag: int) -> np.ndarray:
    y = y - y.mean()
    size = 1
    while size < 2 * len(y):
        size <<= 1
    spectrum = np.fft.rfft(y, size)
    ac = np.fft.irfft(spectrum * np.conj(spectrum), size)[: max_lag + 1]
    return ac / ac[0] if ac[0] > 0 else np.zeros_like(ac)


def local_peaks(values: np.ndarray, lo: int, hi: int) -> list[tuple[int, float]]:
    hi = min(hi, len(values) - 2)
    peaks = []
    for i in range(max(1, lo), hi):
        if values[i] > values[i - 1] and values[i] >= values[i + 1] and values[i] > 0:
            peaks.append((i, float(values[i])))
    return sorted(peaks, key=lambda item: -item[1])


def welch(y: np.ndarray, dt: float, segment_s: float) -> tuple[np.ndarray, np.ndarray, float]:
    n = len(y)
    seg = int(round(segment_s / dt))
    seg = max(16, min(seg, n))
    step = max(1, seg // 2)
    window = np.hanning(seg)
    acc = None
    count = 0
    for start in range(0, n - seg + 1, step):
        chunk = detrend(y[start:start + seg]) * window
        power = np.abs(np.fft.rfft(chunk)) ** 2
        acc = power if acc is None else acc + power
        count += 1
    if acc is None:
        acc = np.abs(np.fft.rfft(detrend(y) * np.hanning(n))) ** 2
        seg = n
        count = 1
    freqs = np.fft.rfftfreq(seg, dt)
    return freqs[1:], (acc / count)[1:], seg * dt


def bandpass(y: np.ndarray, dt: float, lo: float, hi: float) -> np.ndarray:
    spectrum = np.fft.rfft(y)
    freqs = np.fft.rfftfreq(len(y), dt)
    spectrum[(freqs < lo) | (freqs > hi)] = 0.0
    spectrum[0] = 0.0
    return np.fft.irfft(spectrum, len(y))


def sinfit(t: np.ndarray, y: np.ndarray, freq: float) -> tuple[float, float]:
    """在固定频率上最小二乘拟合 y ≈ A·sin(2πft + φ)；返回 (A, φ)。"""
    omega = 2 * math.pi * freq
    design = np.column_stack([np.ones_like(t), t, np.cos(omega * t), np.sin(omega * t)])
    coef, *_ = np.linalg.lstsq(design, y, rcond=None)
    cos_c, sin_c = coef[2], coef[3]
    return math.hypot(cos_c, sin_c), math.atan2(cos_c, sin_c)


def periodogram_at(y: np.ndarray, t: np.ndarray, freq: float) -> float:
    """任意频率上的周期图功率（等价于对该频率的最小二乘正弦拟合的解释方差）。"""
    omega = 2 * math.pi * freq
    cos_part = float(y @ np.cos(omega * t))
    sin_part = float(y @ np.sin(omega * t))
    return (cos_part * cos_part + sin_part * sin_part) / len(y)


def refine_freq(y: np.ndarray, t: np.ndarray, coarse: float, steps: int = 600) -> float:
    """Welch 的分辨率受段长限制（30 s 记录只有 30 mHz 级），在峰附近做细扫把周期定准。

    没有这一步，2–4 s 带内的「主周期」会被量化到 0.3 s 上，相位与滞后都跟着失真。
    """
    lo, hi = coarse * 0.6, coarse * 1.6
    grid = np.linspace(lo, hi, steps)
    powers = [periodogram_at(y, t, f) for f in grid]  # noqa: C416 - 显式循环更易读
    return float(grid[int(np.argmax(powers))])


def corr_curve(a: np.ndarray, b: np.ndarray, max_lag: int) -> dict[int, float]:
    """全部整数滞后的 Pearson r（lag>0 ⇒ A 落后 B）。落在两端重叠不足 16 点的滞后不入表。"""
    curve = {}
    for lag in range(-max_lag, max_lag + 1):
        if lag >= 0:
            x, y = a[lag:], b[: len(b) - lag]
        else:
            x, y = a[: len(a) + lag], b[-lag:]
        if x.size < 16:
            continue
        xs, ys = x - x.mean(), y - y.mean()
        denom = math.sqrt(float(xs @ xs) * float(ys @ ys))
        if denom > 0:
            curve[lag] = float(xs @ ys) / denom
    return curve


def locate_lag(curve: dict[int, float], max_lag: int, resolution: int) -> tuple[str, int, float] | None:
    """定位相关峰并判它属于哪一类：'simultaneous' / 'interior' / 'edge'。返回 (类, lag, r)。

    三类分别对应「能说什么」：

    * `simultaneous` —— 峰落在**一个采样间隔内**。这些序列先被 1.5–5 s 带通，相关长度本就是秒级，
        互相关系数在 0 附近又宽又平；把 20 ms 与 0 区分开在数值上不可能（平坦度差异约 0.2%）。
        此时唯一诚实的表述是「两者同相位（在采样分辨率内）」，报一个具体毫秒数就是假精度。
        **不能把这一段直接排除**：真峰在这里时，剩下的 argmax 会停到周期旁瓣上（实测两个完全相同的
        信号会被报到 −1.06 s），比不设限更错。
    * `interior` —— 峰**严格落在区间内部** ⇒ 先后关系可读，这是唯一能给出「谁领先」的情况。
    * `edge` —— 峰贴在 ±`max_lag`（最短周期）上 ⇒ 被搜索区间截断，滞后**数值**无意义，只有 r 有意义。
    """
    usable = {lag: r for lag, r in curve.items() if abs(lag) <= max_lag}
    if not usable:
        return None
    # 升序遍历 ⇒ 并列时取 |lag| 较小者，于是「内部峰与边界同高」会判成 interior（保守方向相反时更可取）。
    best_lag = max(sorted(usable, key=abs), key=lambda lag: abs(usable[lag]))
    if abs(best_lag) < resolution:
        return ("simultaneous", best_lag, usable[best_lag])
    if abs(best_lag) >= max_lag:
        # 峰就落在搜索上限上 ⇒ 真峰可能在被截断的另一侧，滞后**数值**不可信。
        # 不能因为「有个内部峰只低 0.2%」就改判 interior：那只是平顶，不构成定位。
        return ("edge", best_lag, usable[best_lag])
    return ("interior", best_lag, usable[best_lag])


def sparkline(values: np.ndarray, width: int = 100) -> str:
    finite = values[np.isfinite(values)]
    if finite.size == 0:
        return ""
    lo, hi = float(np.min(finite)), float(np.max(finite))
    span = hi - lo if hi > lo else 1.0
    step = max(1, len(values) // width)
    out = []
    for i in range(0, len(values), step):
        value = float(np.mean(values[i:i + step]))
        if not math.isfinite(value):
            out.append(" ")
            continue
        rank = int((value - lo) / span * (len(SPARK) - 1))
        out.append(SPARK[max(0, min(len(SPARK) - 1, rank))])
    return "".join(out)


# --------------------------------------------------------------------------- 主流程

@dataclass
class Analyzed:
    series: Series
    grid: np.ndarray
    dt: float
    f_dom: float
    p_dom: float
    r_acf: float
    excess: float
    significant: bool
    # 该序列的滞后分辨率（秒）：插值序列取采样间隔中位，帧块均值序列为 0。小于它的滞后只报「同时」，
    # 不报毫秒数——带通后 20ms 与 0 在数值上不可分。注意是**降级表述**，不是排除：真峰落在这里时，
    # 若把该段排除，argmax 会退到周期旁瓣上（实测两个完全相同的信号会被报到 −1.06s）。
    min_lag: float = 0.0
    # 普通 RMS / 稳健 σ 的比值。> `OUTLIER_RATIO` 说明方差由少数离群帧（开局加载、退出）主导，
    # 带内周期与相位结论此时全是那个脉冲的余振，必须拒绝 —— 实测同一个 141ms 首帧让
    # `ElapsedMs × GcPauseDeltaMs` 得到 r=+0.998 这种不可能成立的读数。
    outlier_ratio: float = 0.0

    @property
    def phase_usable(self) -> bool:
        """能否参与带内周期 / 相位结论：既要抓全（非尾部），又不能是离群帧主导。"""
        return self.series.valid and self.outlier_ratio <= OUTLIER_RATIO


def analyze(series: Series, grid_t0: float, dt: float, count: int, band: tuple[float, float],
            max_lag_s: float) -> Analyzed | None:
    values = to_grid(series, grid_t0, dt, count)
    if values is None or np.allclose(values, values[0], equal_nan=True):
        return None

    detrended = detrend(values)
    rms = float(np.sqrt(np.mean(detrended ** 2)))
    if rms <= 0:
        return None

    # 稳健尺度：MAD 对离群帧不敏感。两者差距大 = 方差由少数帧（开局加载、退出）独占。
    mad = float(np.median(np.abs(detrended - np.median(detrended))))
    robust = 1.4826 * mad
    outlier_ratio = rms / robust if robust > 1e-12 else float("inf")

    max_lag = min(int(round(max_lag_s / dt)), len(values) // 3)
    ac = autocorr(detrended, max_lag)

    segment_s = max(8.0, min(64.0, len(values) * dt / 4.0))
    freqs, power, used_segment = welch(detrended, dt, segment_s)
    band_lo, band_hi = 1.0 / band[1], 1.0 / band[0]  # 周期带 -> 频率带
    in_band = (freqs >= band_lo) & (freqs <= band_hi)
    if not in_band.any():
        in_band = np.ones_like(freqs, dtype=bool)

    peaks = [(freqs[i], power[i]) for i in range(1, len(freqs) - 1)
             if in_band[i] and power[i] > power[i - 1] and power[i] >= power[i + 1]]
    if not peaks:
        index = int(np.argmax(np.where(in_band, power, -1.0)))
        peaks = [(freqs[index], power[index])]
    f_coarse, p_coarse = max(peaks, key=lambda item: item[1])

    # Welch 的分辨率被段长卡住，主周期用它做粗定位、再用细扫定准。
    grid_t = np.arange(len(detrended)) * dt
    f_dom = refine_freq(detrended, grid_t, f_coarse)
    lag_dom = int(round(1.0 / f_dom / dt))
    lo = max(0, int(lag_dom * 0.85))
    hi = min(len(ac) - 1, int(lag_dom * 1.15) + 1)
    acf_peak = float(np.max(ac[lo:hi + 1])) if hi > lo else float(ac[min(lag_dom, len(ac) - 1)])

    # 「峰 / 中位」在 1/f^2 型背景上恒为几百倍，不能当显著性用。改成与**带内均匀**比：
    # 一个白噪序列的每个带内 bin 期望各占 1/nbins，超出这个基准多少倍才是可比的读数。
    band_bins = int(in_band.sum())
    band_share = float(p_coarse / power[in_band].sum()) if band_bins else float("nan")
    excess = band_share * band_bins if band_bins else float("nan")
    significant = acf_peak >= 0.2 and excess >= 5.0

    tag = "" if series.valid else "  ⚠ 尾部数据：下面的周期/相位读数无效，只可看分布"
    if series.valid and outlier_ratio > OUTLIER_RATIO:
        tag = (f"  ⚠ 方差由离群帧主导（RMS/稳健σ = {outlier_ratio:.1f}x）：带内周期与相位结论"
               "全是开局加载/退出那几帧的余振，不参与相位表；用 --trim 去掉首尾过渡段重跑")
    print(f"\n  [{series.name}]  ← {series.source}  单位 {series.unit}  模式 {series.mode}{tag}")
    print(f"    去趋势后 RMS = {rms:.4g} {series.unit}（稳健σ = {robust:.4g}，比值 {outlier_ratio:.1f}x），"
          f"跨度 {len(values) * dt:.1f}s，网格 {dt * 1000:.0f}ms")

    # 事件型序列是插值上网格的：插值把相邻两点之间填成直线，于是滞后小于一个采样间隔的那段，
    # 两条曲线在数值上无法分辨（对 1.5–5 s 的带内成分，20 ms 与 0 的平坦度差异约 0.2%）。
    # 这不是「必须排除」，而是「只能表述为同时」——见 locate_lag。
    min_lag = 0.0
    sample_times = series.t[np.isfinite(series.t) & np.isfinite(series.y)]
    if series.mode == "interp" and sample_times.size >= 8:
        gaps = np.diff(np.sort(sample_times))
        median_gap = float(np.median(gaps))
        min_lag = median_gap
        print(f"    采样 {1.0 / median_gap:.1f} 次/s（间隔中位 {median_gap * 1000:.0f}ms）⇒ "
              f"滞后 < {min_lag * 1000:.0f}ms 只能表述为「同时」（分辨率内）")

    print(f"    目标带 {band[0]:.2f}–{band[1]:.2f}s 内主周期 = {1.0 / f_dom:.3f}s "
          f"（{f_dom * 1000:.1f} mHz，Welch 分辨率 {1.0 / used_segment * 1000:.1f} mHz）")
    print(f"    该周期处 ACF r = {acf_peak:+.3f}   带内占比 = {band_share:.1%}（均匀背景 {1.0 / band_bins:.1%}"
          f" ⇒ 高出 {excess:.1f}x）   {'← 显著' if significant else '← 弱/不显著'}")

    print("    ACF 主峰（lag → r；~ = 落在分辨率内，与「同时」不可区分）：", end="")
    for lag, value in local_peaks(ac, int(0.05 / dt), max_lag)[:6]:
        mark = "~" if lag * dt < min_lag else " "
        print(f" {mark}{lag * dt:.2f}s→{value:+.3f}", end="")
    print()

    print("    PSD 主峰（周期 → 占全谱）：", end="")
    for lag, value in local_peaks(power, 1, len(power) - 1)[:6]:
        print(f"  {1.0 / freqs[lag]:.2f}s→{value / power.sum():.1%}", end="")
    print()

    print(f"    波形（去趋势，按时间）：{sparkline(detrended)}")
    band_wave = bandpass(detrended, dt, band_lo, band_hi)
    print(f"    带内成分（{band[0]:.1f}–{band[1]:.1f}s）：{sparkline(band_wave)}")
    return Analyzed(series, values, dt, f_dom, 1.0 / f_dom, acf_peak, excess, significant,
                    min_lag=min_lag, outlier_ratio=outlier_ratio)


def sliding_windows(item: Analyzed, band: tuple[float, float], verbose: bool) -> None:
    analyzed = item
    if not analyzed.phase_usable:
        return  # 带内结论本身不可用，滑窗只会放大那个脉冲的余振
    dt = analyzed.dt
    wave = bandpass(detrend(analyzed.grid), dt, 1.0 / band[1], 1.0 / band[0])
    window_s = max(4.0, 3.0 * analyzed.p_dom)
    step_s = window_s / 4.0
    window = int(round(window_s / dt))
    step = max(1, int(round(step_s / dt)))
    if window >= len(wave) // 2:
        print(f"    滑窗跳过（窗口 {window_s:.1f}s 相对跨度 {len(wave) * dt:.1f}s 过大）")
        return

    rows = []
    prev_phase = None
    accumulated = 0.0
    for start in range(0, len(wave) - window + 1, step):
        t = (np.arange(start, start + window) * dt)
        amp, phase = sinfit(t, wave[start:start + window], analyzed.f_dom)
        if prev_phase is not None:
            delta = phase - prev_phase
            while delta > math.pi:
                delta -= 2 * math.pi
            while delta < -math.pi:
                delta += 2 * math.pi
            accumulated += delta
        prev_phase = phase
        rows.append((start * dt, amp, phase, accumulated))

    amplitudes = np.array([r[1] for r in rows])
    drift_hz = 0.0
    if len(rows) >= 3:
        span = (rows[-1][0] - rows[0][0])
        if span > 0:
            drift_hz = (rows[-1][3] - rows[0][3]) / (2 * math.pi * span)

    mean_amp = float(amplitudes.mean())
    cv = float(amplitudes.std() / mean_amp) if mean_amp > 0 else float("nan")
    print(f"    滑窗 窗口={window_s:.1f}s 步长={step_s:.2f}s：幅度均值={mean_amp:.4g} 变异系数={cv:.2f} "
          f"{'⇒ 幅度稳定' if cv < 0.5 else '⇒ 幅度不恒定，更像阵发而非纯正弦'}")
    print(f"    相位累计漂移 ⇒ 实测频率 {drift_hz * 1000:+.2f} mHz ⇒ 实测周期 "
          f"{1.0 / (analyzed.f_dom + drift_hz):.3f}s（对 {analyzed.p_dom:.3f}s）")
    if verbose:
        for start, amp, phase, _ in rows:
            print(f"      t={start:8.2f}s  幅度={amp:8.4g}  相位={phase:+.3f} rad")


def null_max_r_threshold(count: int, dt: float, band: tuple[float, float], max_lag: int,
                         pair_count: int, family: float = 0.05, draws: int = 400,
                         seed: int = 20260924) -> tuple[float, float]:
    """独立带通噪声下「单对在滞后扫描内的最大 |r|」的分位数。返回 (家族性阈值, 单对 p95)。

    **为什么必须有这个**：把序列带通到 1.5–5 s 后，独立样本数只有约「跨度 / 周期」≈ 13 个，
    于是 |r| 的偶然水平极高。实测（同 span、同 dt，2000+ 次独立对）：单对 |r| ≥ 0.47 的概率 7.1%、
    ≥ 0.50 约 4%、p99.9 = 0.66。而一张表有几十对、每对还扫上百个滞后 ⇒ **出现 0.5–0.6 的「显著」
    读数几乎是必然的**（66 对里至少一对 ≥0.47 的概率 99.2%）。没有这条基线，看表的人一定会把
    偶然峰当成相位来源。

    零模型用「白噪过同一带通」，与「该带内没有任何真实结构」等价；它只依赖 (count, dt, band, max_lag)，
    与具体是哪一对无关，因此一次抽样对全表通用。
    """
    rng = np.random.default_rng(seed)
    f_lo, f_hi = 1.0 / band[1], 1.0 / band[0]
    maxes = np.empty(draws)
    for i in range(draws):
        a = bandpass(rng.normal(size=count), dt, f_lo, f_hi)
        b = bandpass(rng.normal(size=count), dt, f_lo, f_hi)
        curve = corr_curve(a, b, max_lag)
        maxes[i] = max((abs(v) for v in curve.values()), default=0.0)
    per_pair = family / max(1, pair_count)
    return float(np.quantile(maxes, 1.0 - per_pair)), float(np.percentile(maxes, 95))


def print_phase_table(items: list[Analyzed], band: tuple[float, float]) -> None:
    """只报唯一对、只报带内一个周期以内的滞后。

    滞后搜索超过一个周期就没有信息量：周期信号在 k 倍周期处有等价峰，而较长重叠窗口会让某个
    偶然的远滞后 r 略高于真峰，于是「Drift 比 PreColumnMs 早 10540ms」这种读数会出现。限制在
    ±最短周期（band[0]）内即不可能跨周期，也就不会有混叠。
    """
    if len(items) < 2:
        return

    lag_limit = band[0]
    rows = []
    blind = []
    derived = []
    skipped = [a for a in items if not a.phase_usable]
    for i in range(len(items)):
        for j in range(i + 1, len(items)):
            a, b = items[i], items[j]
            if not (a.phase_usable and b.phase_usable):
                continue
            # 定义性派生对（SpikeRate 由 ElapsedMs 按阈值构造、InterpRate 由两条时钟差分构造）之间必然高相关，不是发现。
            if a.series.name in b.series.derived_from or b.series.name in a.series.derived_from:
                derived.append((a.series.name, b.series.name))
                continue
            if a.dt != b.dt or len(a.grid) != len(b.grid):
                continue
            wave_a = bandpass(detrend(a.grid), a.dt, 1.0 / band[1], 1.0 / band[0])
            wave_b = bandpass(detrend(b.grid), b.dt, 1.0 / band[1], 1.0 / band[0])
            # 两侧的分辨率取大者：任一侧分辨不出更小的滞后，这一对就给不出更细的先后。
            resolution = int(round(max(a.min_lag, b.min_lag) / a.dt))
            curve = corr_curve(wave_a, wave_b, int(round(lag_limit / a.dt)))
            located = locate_lag(curve, int(round(lag_limit / a.dt)), resolution)
            if located is None:
                blind.append((a.series.name, b.series.name))
                continue
            kind, lag, r = located
            rows.append((abs(r), a, b, lag, r, kind))

    print(f"\n== 跨序列相位关系（同一带内成分；滞后搜索限 ±{lag_limit:.2f}s = 最短周期内） ==")
    if skipped:
        reasons = []
        tails = sorted({a.series.name for a in skipped if not a.series.valid})
        spikes = sorted({a.series.name for a in skipped if a.series.valid})
        if tails:
            reasons.append(f"尾部 frame 数据（会伪造极强虚相关）：{', '.join(tails)}")
        if spikes:
            reasons.append(f"离群帧主导方差（RMS/稳健σ > {OUTLIER_RATIO:.0f}x）：{', '.join(spikes)}")
        print(f"  已排除 {len(skipped)} 条序列 —— " + "；".join(reasons) +
              "\n      后者请用 --trim 去掉开局/退出过渡段后重跑，否则它们之间的高相关只是同一个脉冲的余振")
    if blind:
        print(f"  另有 {len(blind)} 对完全搜不出峰（重叠不足）而略过，例如 {blind[0][0]} × {blind[0][1]}")
    rows = [row for row in sorted(rows, key=lambda row: -row[0]) if row[4] and abs(row[4]) >= 0.25]
    if not rows:
        print("  没有任何一对 |r| >= 0.25：这一带内各路信号彼此不锁相。")
        return

    # 偶然水平基线：带通后的独立样本太少，|r| 的偶然峰很高（见 null_max_r_threshold 的说明）。
    sample = rows[0][1]
    threshold, p95 = null_max_r_threshold(len(sample.grid), sample.dt, band,
                                          int(round(lag_limit / sample.dt)), len(rows))
    print(f"  偶然水平基准（{len(rows)} 对 × 独立带通噪声，{len(sample.grid)} 点同网格）："
          f"单对 p95 = {p95:.3f}，家族性 5% 阈值 = **{threshold:.3f}**"
          f" ⇒ 低于它的行无法与偶然区分")
    if derived:
        print(f"  已跳过 {len(derived)} 对定义性派生关系（构造相关，非发现）："
              f" {', '.join(f'{x}×{y}' for x, y in derived)}")

    print(f"  {'A':<15} {'B':<15} {'A 相对 B':>13}  {'r':>7}  滞后可信性")
    survivors = 0
    for _, a, b, lag, r, kind in rows[:20]:
        offset = lag * a.dt * 1000.0
        when = "晚" if offset > 0 else "早"
        res_ms = max(a.min_lag, b.min_lag) * 1000.0
        if kind == "simultaneous":
            cell = "同时"
            note = f"峰在分辨率内（±{res_ms:.0f}ms，采样间隔）⇒ **先后关系不可定**，只可读 r（符号即正负相关）"
        elif kind == "interior":
            cell = f"{when}{abs(offset):.0f}ms"
            note = "内部峰 ⇒ 先后关系可读"
        else:
            cell = f"贴边{abs(offset):.0f}ms"
            note = f"峰贴 ±{lag_limit * 1000:.0f}ms（最短周期）⇒ 被截断，**滞后不可定**，只可读 r"
        if abs(r) < threshold:
            note += "；**未越偶然阈值**"
        else:
            survivors += 1
        print(f"  {a.series.name:<15} {b.series.name:<15} {cell:>13}  {r:+7.3f}  {note}")
    if survivors == 0:
        print(f"  ⇒ 这一带内**没有任何一对越过偶然阈值**：表中所有相关性都可由噪声解释。")


def plot(items: list[Analyzed], band: tuple[float, float], out_dir: str) -> None:
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except Exception as exc:  # noqa: BLE001 - 画图是可选增值
        print(f"\n(跳过画图：{exc})")
        return

    for item in items:
        values = detrend(item.grid)
        dt = item.dt
        max_lag = min(int(round(12.0 / dt)), len(values) // 3)
        ac = autocorr(values, max_lag)
        freqs, power, _ = welch(values, dt, max(8.0, min(64.0, len(values) * dt / 4.0)))
        wave = bandpass(values, dt, 1.0 / band[1], 1.0 / band[0])
        t = np.arange(len(values)) * dt

        figure, axes = plt.subplots(4, 1, figsize=(13, 10), sharex=False)
        axes[0].plot(t, values, lw=0.5)
        axes[0].set_title(f"{item.series.name}  raw/detrended  ({item.series.unit})")
        axes[1].plot(t, wave, lw=0.6)
        axes[1].set_title(f"band-passed {band[0]:.2f}-{band[1]:.2f}s  (dominant {item.p_dom:.3f}s, "
                          f"ACF r={item.r_acf:+.3f}, band excess={item.excess:.1f}x)")
        axes[2].plot(np.arange(len(ac)) * dt, ac, lw=0.8)
        axes[2].axvline(item.p_dom, color="tab:red", ls="--", lw=0.8)
        axes[2].set_title("autocorrelation"); axes[2].set_xlim(0, 12)
        axes[3].semilogy(1.0 / freqs[freqs > 0], power[freqs > 0], lw=0.8)
        axes[3].axvline(item.p_dom, color="tab:red", ls="--", lw=0.8)
        axes[3].set_title("Welch PSD vs period (s)"); axes[3].set_xlim(0.2, 20)

        figure.tight_layout()
        path = os.path.join(out_dir, f"period_{item.series.source.replace('.csv', '')}_{item.series.name}.png")
        figure.savefig(path, dpi=110)
        plt.close(figure)
        print(f"  已写图 {path}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("paths", nargs="*", help="诊断 CSV（自动识别 kind）")
    parser.add_argument("--judgment", action="append", default=[], help="judgment_*.csv")
    parser.add_argument("--frame", action="append", default=[], help="framestall_*.csv（必须是全集）")
    parser.add_argument("--press", action="append", default=[], help="presslatency_*.csv")
    parser.add_argument("--dt", type=float, default=0.02, help="重采样网格步长（秒），默认 0.02")
    parser.add_argument("--band", default="1.5,5.0", help="关注的周期带（秒，lo,hi），默认 1.5,5.0")
    parser.add_argument("--max-lag", type=float, default=12.0, help="滞后 / ACF 搜索上限（秒），默认 12")
    parser.add_argument("--trim", help="先裁掉首尾过渡段（秒，A,B），如 --trim 3,3。"
                                       "开局加载 / 退出帧是已知的大离群点，会把带内能量变成同一个脉冲的余振")
    parser.add_argument("--only", default="", help="只分析逗号分隔的这些序列名")
    parser.add_argument("--windows", action="store_true", help="对所有序列都打印滑窗明细（默认只打印显著的）")
    parser.add_argument("--plot", action="store_true", help="额外输出 matplotlib PNG")
    args = parser.parse_args()

    band = tuple(sorted(float(x) for x in args.band.split(",")))
    if len(band) != 2:
        parser.error("--band 需要 lo,hi")
    only = {name.strip() for name in args.only.split(",") if name.strip()}

    if args.trim:
        parts = [p for p in args.trim.split(",")]
        if len(parts) != 2:
            parser.error("--trim 需要 A,B")
        try:
            args.trim = (float(parts[0]), float(parts[1]))
        except ValueError:
            parser.error("--trim 需要两个数字（秒），如 --trim 3,3")
        if min(args.trim) < 0:
            parser.error("--trim 不接受负数")

    paths = list(args.paths) + list(args.judgment) + list(args.frame) + list(args.press)
    if not paths:
        parser.error("至少要给一个 CSV")
    for path in paths:
        if not os.path.exists(path):
            parser.error(f"找不到 {path}")

    collected: list[Series] = []
    spans: list[tuple[str, float, float]] = []
    print("== 输入 ==")
    for path in paths:
        header, rows = read_table(path)
        kind = detect_kind(header)
        note = ""
        full_capture = True
        if kind == "frame":
            note, full_capture = frame_capture_state(path)
        series = build_series(path, (header, rows), full_capture)
        if series:
            starts = [float(np.nanmin(s.t)) for s in series if np.isfinite(s.t).any()]
            ends = [float(np.nanmax(s.t)) for s in series if np.isfinite(s.t).any()]
            spans.append((os.path.basename(path), min(starts), max(ends)))
        print(f"  {os.path.basename(path):<40} kind={kind:<8} n={len(rows):<7} {note}")
        collected += series

    if spans:
        global_span = max(e for _, _, e in spans) - min(s for _, s, _ in spans)
        widest = max(e - s for _, s, e in spans)
        if widest > 0 and global_span > widest * 1.5:
            print(f"  注意：合并跨度 {global_span:.1f}s 远大于单文件最大跨度 {widest:.1f}s ⇒ "
                  "把多局拼到了一条时间轴上，跨局互相关无意义。")

    if only:
        collected = [s for s in collected if s.name in only]

    if args.trim:
        head, tail = args.trim
        raw0 = min(float(np.nanmin(s.t)) for s in collected)
        raw1 = max(float(np.nanmax(s.t)) for s in collected)
        lo, hi = raw0 + head, raw1 - tail
        if hi - lo < 8 * args.dt:
            parser.error(f"--trim {head},{tail} 把可分析区间削到 {hi - lo:.1f}s，不足 8 个网格点")
        kept = [trim_series(s, lo, hi) for s in collected]
        collected = [s for s in kept if s is not None]
        print(f"\n== 裁剪过渡段 ==\n  掉首 {head:.1f}s + 尾 {tail:.1f}s ⇒ "
              f"分析区间 {lo - raw0:.1f}s–{hi - raw0:.1f}s（相对原始起点）")
        if not collected:
            parser.error("--trim 后没有序列剩下")

    t0 = min(float(np.nanmin(s.t)) for s in collected)
    t1 = max(float(np.nanmax(s.t)) for s in collected)
    dt = args.dt
    count = int(math.ceil((t1 - t0) / dt)) + 1
    print(f"\n== 统一网格 ==\n  t0={t0:.3f}s  t1={t1:.3f}s  跨度={t1 - t0:.1f}s  dt={dt * 1000:.0f}ms  点数={count}")

    analyzed: list[Analyzed] = []
    print("\n== 逐序列周期 ==")
    for series in collected:
        item = analyze(series, t0, dt, count, band, args.max_lag)
        if item is None:
            print(f"\n  [{series.name}] 跳过（有效样本不足或为常量）")
            continue
        analyzed.append(item)

    for item in analyzed:
        print()
        sliding_windows(item, band, verbose=item.significant or args.windows)
    print_phase_table(analyzed, band)

    if args.plot:
        out_dir = os.path.dirname(os.path.abspath(paths[0])) or "."
        print("\n== 画图 ==")
        plot(analyzed, band, out_dir)

    print("\n提示：判读顺序 = ① frame 的 summary 必须 threshold=0（否则只看分布） → ② 各序列的 "
          "RMS/稳健σ 比值要在 5x 以内（否则带内结论是开局加载帧的余振，用 --trim 去掉首尾各 3s） → "
          "③ 看目标带内主周期的 ACF r 与「带内高出均匀背景的倍数」 → ④ 再看跨序列滞后定相位来源。"
          "ACF r < 0.2 或高出 < 5x 基本可判为「没有稳定周期」。"
          "相位表只有「内部峰」那几行能给出先后关系；「同时」只说明两者锁相（先后在采样分辨率内不可分），"
          "「贴边」说明峰被搜索区间截断、滞后数值不可信 —— 这两类都只看 r。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
