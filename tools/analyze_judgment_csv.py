#!/usr/bin/env python3
# analyze_judgment_csv.py
# 交互式脚本：运行后提示输入 CSV 路径和输出目录
import os

import numpy as np
import pandas as pd


def safe_read(path):
    encodings = ["utf-8", "utf-8-sig", "latin1", "cp1252"]
    last_err = None
    for e in encodings:
        try:
            return pd.read_csv(path, encoding=e)
        except Exception as ex:
            last_err = ex
    raise RuntimeError(f'无法读取 CSV：尝试过多种编码均失败，最后错误：{last_err}')


def find_candidate_columns(df):
    cols_lower = {c: c.lower() for c in df.columns}
    def find_any(keys):
        return [orig for orig, low in cols_lower.items() if any(k in low for k in keys)]

    return {
        'offset': find_any(['offset', 'timeoffset', 'time_offset', 'hit_offset', 'judgment']),
        'correction': find_any(['correction', 'correction_ms', 'subframe', 'sub_frame']),
        'key_ts': find_any(['key', 'input', 'press', 'wall', 'timestamp', 'ts', 'key_ts']),
        'fsc_ts': find_any(['fsc', 'frame_stability', 'fsc_update', 'fsc_ts', 'fsc_timestamp'])
    }


def main():
    print('交互式 osu 判定诊断 CSV 分析脚本')
    # 交互式输入 CSV 路径
    while True:
        csv_path = input('请输入 CSV 文件路径（或粘贴并回车；输入 q 退出）： ').strip().strip('"').strip("'")
        if not csv_path:
            print('路径不能为空，请重试或输入 q 退出。')
            continue
        if csv_path.lower() in ('q', 'quit', 'exit'):
            print('已取消。')
            return
        csv_path = os.path.expanduser(csv_path)
        if not os.path.isfile(csv_path):
            print(f'文件不存在: {csv_path}\n请检查路径后重试，或输入 q 退出。')
            continue
        break

    # 输出目录，默认为 CSV 所在目录下的 analysis_output
    default_outdir = os.path.join(os.path.dirname(csv_path), 'analysis_output')
    outdir_input = input(f'输出目录（回车使用默认: {default_outdir}）： ').strip().strip('"').strip("'")
    outdir = outdir_input or default_outdir
    outdir = os.path.expanduser(outdir)
    os.makedirs(outdir, exist_ok=True)

    print('读取:', csv_path)
    df = safe_read(csv_path)
    print('shape:', df.shape)
    print('\n列名:')
    for c in df.columns:
        print(' -', c)

    print('\n前 8 行示例:')
    print(df.head(8).to_string(index=False))

    # numeric summary
    num = df.select_dtypes(include=[np.number])
    if num.shape[1] > 0:
        print('\n数值列描述统计:')
        with pd.option_context('display.max_rows', None, 'display.width', 200):
            print(num.describe(percentiles=[0.01,0.05,0.1,0.25,0.5,0.75,0.9,0.95,0.99]).T)
    else:
        print('\n未检测到数值列，跳过数值统计。')

    candidates = find_candidate_columns(df)
    print('\n检测到候选列:')
    print(' offset columns:', candidates['offset'])
    print(' correction columns:', candidates['correction'])
    print(' key/input timestamp columns:', candidates['key_ts'])
    print(' FSC timestamp columns:', candidates['fsc_ts'])

    # plotting setup
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt

    # offset analysis
    if candidates['offset']:
        col = candidates['offset'][0]
        s = pd.to_numeric(df[col], errors='coerce').dropna()
        total = len(s)
        early = (s < 0).sum()
        late = (s > 0).sum()
        exact = (s == 0).sum()
        print(f"\nOffset 列 '{col}' 统计 (共 {total} 个有效): early={early} ({early/total:.1%}), late={late} ({late/total:.1%}), exact={exact}")
        print(s.describe())

        plt.figure(figsize=(7,4))
        plt.hist(s, bins=100, color='#4C72B0', alpha=0.9)
        plt.title(f'Offset histogram ({col})')
        plt.xlabel('ms')
        plt.ylabel('count')
        hist_path = os.path.join(outdir, 'offset_hist.png')
        plt.tight_layout()
        plt.savefig(hist_path)
        print('已保存直方图:', hist_path)
    else:
        print('\n未找到 offset 列，跳过 offset 统计。')

    # correction stats
    if candidates['correction']:
        col = candidates['correction'][0]
        s = pd.to_numeric(df[col], errors='coerce').dropna()
        print(f"\nCorrection 列 '{col}' 描述:")
        print(s.describe())
        corr_path = os.path.join(outdir, 'correction_hist.png')
        plt.figure(figsize=(6,3))
        plt.hist(s, bins=80, color='#55A868', alpha=0.9)
        plt.title(f'Correction histogram ({col})')
        plt.tight_layout()
        plt.savefig(corr_path)
        print('已保存 correction 直方图:', corr_path)
    else:
        print('\n未找到 correction 列，跳过。')

    # time diff between key_ts and fsc_ts
    if candidates['key_ts'] and candidates['fsc_ts']:
        key_col = candidates['key_ts'][0]
        fsc_col = candidates['fsc_ts'][0]
        try:
            a = pd.to_numeric(df[key_col], errors='coerce')
            b = pd.to_numeric(df[fsc_col], errors='coerce')
            diff = (a - b).dropna()
            print(f"\n时间差 ('{key_col}' - '{fsc_col}') 描述:")
            print(diff.describe())
            plt.figure(figsize=(10,3))
            plt.plot(diff.values, marker='.', linestyle='none', alpha=0.6)
            plt.title(f'Time diff: {key_col} - {fsc_col}')
            plt.ylabel('diff')
            t_path = os.path.join(outdir, 'time_diff.png')
            plt.tight_layout()
            plt.savefig(t_path)
            print('已保存时序差图:', t_path)
        except Exception as ex:
            print('计算时间差时出错:', ex)
    else:
        print('\n无法同时找到 key_ts 与 fsc_ts 列，跳过时间差计算。')

    # save summary
    summary_path = os.path.join(outdir, 'analysis_summary.txt')
    with open(summary_path, 'w', encoding='utf-8') as f:
        f.write('File: ' + csv_path + '\n')
        f.write('shape: ' + str(df.shape) + '\n\n')
        f.write('columns:\n')
        for c in df.columns:
            f.write(' - ' + c + '\n')
        f.write('\nNumeric describe:\n')
        if num.shape[1] > 0:
            f.write(str(num.describe().T))
        else:
            f.write('No numeric columns found.\n')
    print('\n已生成文本摘要:', summary_path)
    print('完成。请将 analysis_summary.txt 或生成的图片贴回以便我进一步分析。')


if __name__ == '__main__':
    main()
