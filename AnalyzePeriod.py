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

用法：
    python AnalyzePeriod.py diagnostics/judgment_20260924_222233.csv
    python AnalyzePeriod.py --judgment diagnostics/judgment_X.csv --frame diagnostics/framestall_X.csv --press diagnostics/presslatency_X.csv
    python AnalyzePeriod.py diagnostics/*.csv --dt 0.02 --band 1.5,5 --max-lag 12 --plot

依赖：numpy（必需）、matplotlib（仅 --plot）。
"""

from __future__ import annotations

import argparse
import csv
import math
import os
import sys
from dataclasses import dataclass

import numpy as np

SPARK = "▁▂▃▄▅▆▇█"


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
    if {"TimeOffset", "Drift", "BassSource"} <= names:
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
        audio_lag = col(header, "BassSource", rows) - col(header, "GameTime", rows)
        out += [
            Series("Drift", t, drift, "interp", "ms", src),
            Series("TimeOffset", t, offset, "interp", "ms", src),
            Series("AudioLag", t, audio_lag, "interp", "ms", src),
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
            Series("SpikeRate", t, spike, "block", "1", src, full_capture),
            Series("GcPauseDeltaMs", t, gc, "block", "ms", src, full_capture),
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
            "需 EZ_FRAME_PROBE_MS=0 重跑"), False


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


def lag_correlation(a: np.ndarray, b: np.ndarray, max_lag: int) -> tuple[int, float]:
    """扫描整数滞后：返回使 |Pearson r| 最大的 lag（样本）。lag>0 ⇒ A 落后 B 该样本数。

    按 |lag| 从小到大扫描并只在**严格更大**时更新，避免周期信号在 k 倍周期处产生等价峰时
    选中一个毫无信息量的远滞后（±3 s 周期在 ±12 s 内有 8 个 r≈1 的位置）。
    """
    best = (0, 0.0)
    for lag in sorted(range(-max_lag, max_lag + 1), key=abs):
        if lag >= 0:
            x, y = a[lag:], b[: len(b) - lag]
        else:
            x, y = a[: len(a) + lag], b[-lag:]
        if x.size < 16:
            continue
        xs, ys = x - x.mean(), y - y.mean()
        denom = math.sqrt(float(xs @ xs) * float(ys @ ys))
        if denom <= 0:
            continue
        r = float(xs @ ys) / denom
        if abs(r) > abs(best[1]) + 1e-9:
            best = (lag, r)
    return best


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


def analyze(series: Series, grid_t0: float, dt: float, count: int, band: tuple[float, float],
            max_lag_s: float) -> Analyzed | None:
    values = to_grid(series, grid_t0, dt, count)
    if values is None or np.allclose(values, values[0], equal_nan=True):
        return None

    detrended = detrend(values)
    rms = float(np.sqrt(np.mean(detrended ** 2)))
    if rms <= 0:
        return None

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
    print(f"\n  [{series.name}]  ← {series.source}  单位 {series.unit}  模式 {series.mode}{tag}")
    print(f"    去趋势后 RMS = {rms:.4g} {series.unit}，跨度 {len(values) * dt:.1f}s，网格 {dt * 1000:.0f}ms")

    # 事件型序列是插值上网格的：插值把相邻两点之间填成直线，于是滞后小于采样间隔的那段 ACF
    # 是插值本身造出来的（读数恒偏高）。必须把这条可读下限印出来，否则会把 0.3 s 的假峰当真。
    min_readable_lag = 0.0
    sample_times = series.t[np.isfinite(series.t) & np.isfinite(series.y)]
    if series.mode == "interp" and sample_times.size >= 8:
        gaps = np.diff(np.sort(sample_times))
        median_gap = float(np.median(gaps))
        min_readable_lag = 3.0 * median_gap
        print(f"    采样 {1.0 / median_gap:.1f} 次/s（间隔中位 {median_gap * 1000:.0f}ms）⇒ "
              f"滞后 < {min_readable_lag * 1000:.0f}ms 的 ACF/相位不可读（插值产物）")

    print(f"    目标带 {band[0]:.2f}–{band[1]:.2f}s 内主周期 = {1.0 / f_dom:.3f}s "
          f"（{f_dom * 1000:.1f} mHz，Welch 分辨率 {1.0 / used_segment * 1000:.1f} mHz）")
    print(f"    该周期处 ACF r = {acf_peak:+.3f}   带内占比 = {band_share:.1%}（均匀背景 {1.0 / band_bins:.1%}"
          f" ⇒ 高出 {excess:.1f}x）   {'← 显著' if significant else '← 弱/不显著'}")

    print("    ACF 主峰（lag → r；~ = 落在不可读区）：", end="")
    for lag, value in local_peaks(ac, int(0.05 / dt), max_lag)[:6]:
        mark = "~" if lag * dt < min_readable_lag else " "
        print(f" {mark}{lag * dt:.2f}s→{value:+.3f}", end="")
    print()

    print("    PSD 主峰（周期 → 占全谱）：", end="")
    for lag, value in local_peaks(power, 1, len(power) - 1)[:6]:
        print(f"  {1.0 / freqs[lag]:.2f}s→{value / power.sum():.1%}", end="")
    print()

    print(f"    波形（去趋势，按时间）：{sparkline(detrended)}")
    band_wave = bandpass(detrended, dt, band_lo, band_hi)
    print(f"    带内成分（{band[0]:.1f}–{band[1]:.1f}s）：{sparkline(band_wave)}")
    return Analyzed(series, values, dt, f_dom, 1.0 / f_dom, acf_peak, excess, significant)


def sliding_windows(item: Analyzed, band: tuple[float, float], verbose: bool) -> None:
    analyzed = item
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
    skipped = [a for a in items if not a.series.valid]
    for i in range(len(items)):
        for j in range(i + 1, len(items)):
            a, b = items[i], items[j]
            if not (a.series.valid and b.series.valid):
                continue
            if a.dt != b.dt or len(a.grid) != len(b.grid):
                continue
            wave_a = bandpass(detrend(a.grid), a.dt, 1.0 / band[1], 1.0 / band[0])
            wave_b = bandpass(detrend(b.grid), b.dt, 1.0 / band[1], 1.0 / band[0])
            lag, r = lag_correlation(wave_a, wave_b, int(round(lag_limit / a.dt)))
            rows.append((abs(r), a, b, lag, r))

    print(f"\n== 跨序列相位关系（同一带内成分；滞后搜索限 ±{lag_limit:.2f}s = 最短周期内） ==")
    if skipped:
        print(f"  已排除 {len(skipped)} 条无效序列（尾部 frame 数据会伪造极强虚相关）："
              f" {', '.join(sorted({a.series.name for a in skipped}))}")
    rows = [row for row in sorted(rows, key=lambda row: -row[0]) if row[4] and abs(row[4]) >= 0.25]
    if not rows:
        print("  没有任何一对 |r| >= 0.25：这一带内各路信号彼此不锁相。")
        return
    print(f"  {'A':<15} {'B':<15} {'A 相对 B':>12}  {'r':>7}")
    for _, a, b, lag, r in rows[:20]:
        offset = lag * a.dt * 1000.0
        when = "晚" if offset > 0 else "早"
        print(f"  {a.series.name:<15} {b.series.name:<15} {when}{abs(offset):8.0f}ms  {r:+7.3f}")


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
    parser.add_argument("--only", default="", help="只分析逗号分隔的这些序列名")
    parser.add_argument("--windows", action="store_true", help="对所有序列都打印滑窗明细（默认只打印显著的）")
    parser.add_argument("--plot", action="store_true", help="额外输出 matplotlib PNG")
    args = parser.parse_args()

    band = tuple(sorted(float(x) for x in args.band.split(",")))
    if len(band) != 2:
        parser.error("--band 需要 lo,hi")
    only = {name.strip() for name in args.only.split(",") if name.strip()}

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

    print("\n提示：判读顺序 = ① frame 的 summary 必须 threshold=0（否则只看分布） → ② 看目标带内主周期的 "
          "ACF r 与「带内高出均匀背景的倍数」 → ③ 再看跨序列滞后定相位来源。"
          "ACF r < 0.2 或高出 < 5x 基本可判为「没有稳定周期」；事件序列还要看那个可读滞后下限。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
