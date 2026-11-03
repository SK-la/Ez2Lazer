# Mania 下落式音乐游戏成绩计算公式研究 V1

## 1. 研究目标

现有总分制由基础分、Combo 分、Accuracy 分组成，但单纯按传统判定结果计分，无法充分反映同一种错误在不同谱面难度位置上的价值。

例如：

- 在很简单的低密度区域 Miss 1 次；
- 在高密度、爆发区域 Miss 1 次。

两者都只是“1 Miss”，但后者应该具有更高的价值损失。

本研究的目标是引入局部 KPS（Keys Per Second）作为外部难度权重，使：

- 高 KPS 区域的精准判定更有价值；
- 高 KPS 区域的 Miss 损失更大；
- KPS 权重单调递增，不出现 KPS 越高价值反而下降；
- 40 KPS 以后进入接近极限的爆发区，不再继续显著增加价值；
- Offset 在 11～16ms 附近出现明显的价值坠落，并在 16ms 后趋近于 0。

---

## 2. 实际计算架构

### 2.1 KPS 外部独立计算

谱面在计分系统外部先按照 **每 1/4 拍一个区间** 计算 KPS，得到完整的 KPS List。

记：

\[
K_i = \text{第 }i\text{ 个 1/4 拍区间的 KPS}
\]

实际 Note 不实时计算 KPS，而是根据 Note 所在区间取得：

\[
K_j = K_{section(j)}
\]

因此计分系统使用的是：

> Note 所在 1/4 拍区间的 KPS，而不是该 Note 瞬时实时 KPS。

这种方式可以视为对谱面局部操作密度的离散化描述。

### 2.2 Note 的判定变量

对于成功输入的 Note：

\[
offset_j = inputTime_j - note.startTime_j
\]

建议实际进入价值公式时使用绝对误差：

\[
E_j = |offset_j|
\]

原因：不能让正负误差相互抵消。例如：

\[
(-8ms + 8ms)/2 = 0ms
\]

但两个 Note 都并不准确。

---

## 3. 系统职责划分

不要把所有东西都合并到一个单一的 Note 分数函数里。

建议保持三个层次：

### 原有正常分数

继续使用已有：

\[
S_{original}=S_{base}+S_{combo}+S_{acc}
\]

### 新增 Offset 精度系统

后台独立统计，用于表达：

> 在当前难度环境下，这次 timing 有多精准。

### 新增 Miss 惩罚系统

后台独立统计，用于表达：

> 这次 Miss 发生在多困难的位置。

这样 KPS 同时作为两个系统的外部输入，但两个系统仍保持独立。

---

# 4. KPS 基础权重

## 4.1 设计要求

KPS 具有以下语义：

- 15 KPS 以下：额外难度价值基本可以忽略；
- 15 KPS 以上：开始有意义；
- 40 KPS：进入接近极限的爆发区；
- 40 KPS 以上：视为饱和区。

定义标准化变量：

\[
z(K)=\operatorname{clamp}\left(\frac{K-15}{25},0,1\right)
\]

其中：

\[
\operatorname{clamp}(x,0,1)=\min(1,\max(0,x))
\]

---

## 4.2 推荐 V1：Smoothstep KPS 权重

定义：

\[
\boxed{W(K)=3z^2-2z^3}
\]

完整写法：

\[
W(K)=
\begin{cases}
0,&K\le15\\
3z^2-2z^3,&15<K<40\\
1,&K\ge40
\end{cases}
\]

满足：

\[
W(15)=0
\]

\[
W(40)=1
\]

\[
K>40\Rightarrow W(K)=1
\]

并且整个区间满足单调递增。

### V1 数值参考

| KPS | W(K) |
|---:|---:|
| 10 | 0.000 |
| 15 | 0.000 |
| 20 | 0.104 |
| 25 | 0.352 |
| 30 | 0.648 |
| 35 | 0.896 |
| 40 | 1.000 |
| 50 | 1.000 |

### 解释

这条曲线表达：

> 15 KPS 以下基本不关心爆发价值；20 KPS 开始出现明显权重；30 KPS 已进入重要区；35 KPS 接近爆发；40 KPS 进入极限区。

---

# 5. Miss 罚分系统

## 5.1 设计目标

Miss 的主要目标是体现：

> 高 KPS 区域的失误应该比低 KPS 区域的失误更贵。

此外，在 40 KPS 以后，系统应该明显偏向“无 Miss”的爆发能力。

因此 Miss 权重应该比 Offset 更偏向高 KPS。

---

## 5.2 推荐 V1：高 KPS 偏向幂函数

定义：

\[
\boxed{W_m(K)=z(K)^\gamma}
\]

推荐初始参数：

\[
\boxed{\gamma=1.8}
\]

因此：

\[
W_m(K)=
\begin{cases}
0,&K\le15\\
z^{1.8},&15<K<40\\
1,&K\ge40
\end{cases}
\]

### V1 数值参考

| KPS | Wm(K) |
|---:|---:|
| 15 | 0.000 |
| 20 | 0.055 |
| 25 | 0.192 |
| 30 | 0.399 |
| 35 | 0.669 |
| 40 | 1.000 |
| 50 | 1.000 |

---

## 5.3 Miss 罚分公式

设最高 Miss 额外罚分单位为：

\[
P_{max}
\]

则第 j 个 Note 的 Miss 罚分为：

\[
\boxed{P_{miss,j}=P_{max}\cdot W_m(K_j)}
\]

即：

- KPS ≤ 15：额外 Miss 罚分为 0；
- KPS 越高：罚分越大；
- KPS ≥ 40：达到最高额外 Miss 罚分。

---

# 6. Combo 加成 vs Miss 罚分

## 6.1 结论

如果必须二选一：

\[
\boxed{推荐 KPS 加权 Miss 罚分}
\]

原因是二者实际解决的问题不同。

### Combo 表达

> 连续发挥了多久。

### Miss 罚分表达

> 这一个错误发生在多困难的位置。

研究目标是后者，因此不应让 Combo 承担“不同 KPS 的错误价值差异”这个职责。

---

## 6.2 Combo 的结构性问题

Combo 是一个依赖历史的全局变量。

同样只出现 1 次 Miss：

- Miss 出现在开头；
- Miss 出现在高 Combo 的中后段；
- Miss 出现在结尾；

实际造成的分数影响可能完全不同。

因此使用 Combo 来体现局部难度，会把：

\[
\text{局部难度}
\]

与：

\[
\text{此前连续成功长度}
\]

耦合。

这与当前研究目标不完全一致。

---

## 6.3 建议

Combo 可以继续保留在原有评分系统中，用来表达连续发挥能力。

但是不要让 Combo 主要负责：

> “困难地方的 Miss 应该更贵”。

这个职责交给 Difficulty-weighted Miss Penalty。

---

# 7. Offset 精度系统

## 7.1 定义

对每个成功判定：

\[
E=|offset|
\]

定义：

\[
F(E)=\text{Offset Quality}
\]

目标：

\[
F(0)=1
\]

随着 E 增大，F(E) 单调下降。

在 11～16ms 中间选择一个边界点 b：

\[
11\le b\le16
\]

边界以内：

- 误差越小，价值越高；
- 前半段下降相对平缓；
- 越接近边界，下降更明显。

超过边界：

- 价值快速下降；
- 16ms 附近接近 0。

超过 16ms：

\[
F(E)=0
\]

---

# 8. 推荐 V1 Offset 曲线

## 8.1 边界参数

第一版建议先测试：

\[
\boxed{b=13ms}
\]

另外保留 11、12、14、15ms 作为实验候选值。

定义边界处仍保留的相对价值：

\[
\boxed{c=0.5}
\]

定义边界后的衰减速度：

\[
\boxed{\lambda=1.2}
\]

---

## 8.2 0～b：类对数缓降

定义：

\[
\boxed{
F(E)=
 c+(1-c)
 \frac{\ln(1+k(1-E/b))}{\ln(1+k)}
}
\]

适用范围：

\[
0\le E\le b
\]

其中 V1：

\[
\boxed{k=9}
\]

因此：

\[
F(0)=1
\]

\[
F(b)=c=0.5
\]

---

## 8.3 b～16：快速指数衰减

\[
\boxed{
F(E)=c\cdot e^{-\lambda(E-b)}
}
\]

适用范围：

\[
b<E<16
\]

V1：

\[
\boxed{b=13,\ c=0.5,\ \lambda=1.2}
\]

---

## 8.4 16ms 以后

\[
\boxed{F(E)=0,\qquad E\ge16ms}
\]

这样可以保证不会出现 16ms 后又重新增加价值的问题。

---

## 8.5 V1 近似数值

| Offset | F(E) |
|---:|---:|
| 0ms | 1.000 |
| 2ms | ≈0.968 |
| 4ms | ≈0.930 |
| 6ms | ≈0.883 |
| 8ms | ≈0.825 |
| 10ms | ≈0.744 |
| 11ms | ≈0.689 |
| 12ms | ≈0.614 |
| 13ms | 0.500 |
| 13.5ms | ≈0.274 |
| 14ms | ≈0.151 |
| 15ms | ≈0.045 |
| 16ms | ≈0 |
| >16ms | 0 |

这不是最终参数，只作为第一轮仿真的基准曲线。

---

# 9. KPS 与 Offset 的组合

Offset 本身只代表 timing 精度，KPS 决定这个精度在当前环境中的价值。

因此对每个成功 Note 定义：

\[
\boxed{Q_j=W(K_j)\cdot F(E_j)}
\]

其中：

- \(W(K_j)\)：该 Note 所在区域的 KPS 难度权重；
- \(F(E_j)\)：该 Note 的 Offset 精度价值。

性质：

\[
KPS\uparrow\Rightarrow Q\uparrow
\]

\[
|offset|\uparrow\Rightarrow Q\downarrow
\]

\[
KPS\ge40\Rightarrow W(K)=1
\]

---

# 10. Avg Error 的计算方式

不建议直接使用：

\[
F\left(\frac1N\sum E_j\right)
\]

原因是：

```text
A: 0, 0, 0, 0, 10  → avg = 2ms
B: 2, 2, 2, 2, 2   → avg = 2ms
```

两者平均误差一样，但实际 timing 稳定性不同。

因此更建议先计算每个 Note 的 Offset 价值，再进行平均。

---

# 11. 推荐的全谱 Offset 指标

定义：

\[
\boxed{
Q_{avg}=
\frac{\sum_jW(K_j)F(E_j)}
{\sum_jW(K_j)}
}
\]

含义：

> **难度加权后的平均 Timing Quality。**

它不会让大量低 KPS Note 把高密度爆发段的重要性稀释掉。

如果某张图有：

- 大量 8～10 KPS 区域；
- 少量 30～40 KPS 爆发；

那么高 KPS 爆发中的 timing 表现会拥有更合理的影响力。

---

# 12. 推荐的全谱 Miss 指标

定义：

\[
I_j=
\begin{cases}
1,&\text{Miss}\\
0,&\text{非 Miss}
\end{cases}
\]

则：

\[
\boxed{
R_{miss}=
\frac{\sum_jW_m(K_j)I_j}
{\sum_jW_m(K_j)}
}
\]

这是：

> **难度加权 Miss Rate**

普通区域的 Miss 权重较低，40 KPS 爆发区的 Miss 权重接近 1。

---

# 13. 最终新增分数

假设原系统为：

\[
S_{original}=S_{base}+S_{combo}+S_{acc}
\]

新系统增加：

\[
\boxed{
S_{new}=B_oQ_{avg}-P_mR_{miss}
}
\]

最终：

\[
\boxed{
S_{final}=S_{original}+S_{new}
}
\]

其中：

- \(B_o\)：Offset 额外奖励最大值；
- \(P_m\)：Miss 额外罚分最大值。

---

# 14. 一个重要的防重复原则

## 14.1 Offset 不要变成第二套 Accuracy

原系统已经有 Accuracy。

因此新 Offset 系统不应该简单地重复奖励：

> Perfect → 已经通过 Accuracy 奖励 → 再奖励同样的 Perfect。

新的 Offset 系统应该承担更窄的职责：

> **在高 KPS 环境中奖励稳定、精确的 timing。**

---

## 14.2 Miss 不要形成三重惩罚

Miss 可能已经造成：

1. 原有判定/Accuracy 损失；
2. 原有 Combo 影响；
3. 新增 KPS 加权 Miss 罚分。

所以 \(P_m\) 不能一开始设置得过大。

新增 Miss 罚分只负责一件事情：

> **困难区域的 Miss 要比简单区域的 Miss 更严重。**

---

# 15. 1/4 拍 KPS 分区的边界问题

当前方案：

```text
谱面
  ↓
每 1/4 拍计算 KPS
  ↓
KPS List
  ↓
Note 查找所属区间
  ↓
读取该区间 KPS
  ↓
计算 Offset / Miss 权重
```

这个方案可以保留，不需要实时 KPS。

但需要记录一个潜在问题：

如果相邻两个区间：

```text
Section A = 18 KPS
Section B = 39 KPS
```

则边界两侧 Note 的权重可能突然变化。

这不是公式单调性错误，而是 KPS 区间离散化造成的边界跳变。

第一版建议先不增加插值复杂度，直接通过真实谱面数据观察问题是否显著。

如确有必要，再考虑相邻区间 KPS 插值。

---

# 16. 当前 V1 参数表

| 参数 | V1 值 | 作用 |
|---|---:|---|
| KPS 起效点 | 15 | 15 以下不产生额外难度权重 |
| KPS 饱和点 | 40 | 40 以后视为爆发极限区 |
| KPS 基础曲线 | Smoothstep | 控制整体难度权重 |
| Miss 指数 γ | 1.8 | 让 Miss 更偏向高 KPS |
| Offset 边界 b | 13ms | 正常精度区与危险区分界 |
| Offset 边界价值 c | 0.5 | 边界点剩余价值 |
| Offset 类对数参数 k | 9 | 控制前段下降形状 |
| Offset 衰减 λ | 1.2 | 控制边界后快速坠落速度 |
| Offset 归零点 | 16ms | 16ms 后无价值 |
| Offset 总奖励 | B_o | 后续根据总分比例确定 |
| Miss 最大额外罚分 | P_m | 后续根据总分比例确定 |

---

# 17. 第一轮仿真必须测试的二维矩阵

建议至少使用以下 KPS：

\[
15,20,25,30,35,40,50
\]

以及以下 Offset：

\[
0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16
\]

计算：

\[
W(K)
\]

\[
W_m(K)
\]

\[
F(E)
\]

\[
Q(K,E)=W(K)F(E)
\]

这样可以形成完整的 KPS × Offset 价值矩阵。

---

# 18. 第一轮需要重点观察的交叉点

不要只看曲线本身，更重要的是看不同条件之间的相对价值。

例如：

### 问题 A

30 KPS 的 5ms：

\[
Q(30,5)
\]

与 20 KPS 的 0ms：

\[
Q(20,0)
\]

谁应该更值？

### 问题 B

40 KPS 的 10ms：

\[
Q(40,10)
\]

与 25 KPS 的 0ms：

\[
Q(25,0)
\]

谁应该更值？

### 问题 C

一次 20 KPS Miss：

\[
W_m(20)
\]

与一次 40 KPS Miss：

\[
W_m(40)
\]

差距是否足够明显？

这些交叉点比单纯看“公式长得好不好看”更重要，因为它们直接对应实际游戏体验。

---

# 19. 后续参数研究顺序

建议严格按以下顺序做：

## Phase 1：KPS 曲线

研究：

- 15 是否是合理起效点；
- 40 是否是合理饱和点；
- 15～40 是线性、Smoothstep、幂函数还是 Sigmoid 更合适。

## Phase 2：Miss 曲线

研究：

- 20/25/30/35/40 KPS 的一次 Miss 分别应该有多大价值；
- 高 KPS 是否需要明显加速惩罚；
- γ = 1.5 / 1.8 / 2.0 等参数比较。

## Phase 3：Offset 边界

测试：

\[
b=11,12,13,14,15
\]

判断哪个边界最符合实际手感。

## Phase 4：Offset 下降曲线

研究：

- 边界前下降速度；
- 边界点剩余价值 c；
- 边界后的指数坠落速度 λ；
- 16ms 是否直接归零。

## Phase 5：总分比例

最后才确定：

\[
B_o
\]

与：

\[
P_m
\]

占整个成绩的比例。

---

# 20. 当前推荐的核心公式汇总

### KPS 基础权重

\[
\boxed{
z=\operatorname{clamp}\left(\frac{K-15}{25},0,1\right)
}
\]

\[
\boxed{
W(K)=3z^2-2z^3
}
\]

### Miss

\[
\boxed{
W_m(K)=z^{1.8}
}
\]

\[
\boxed{
P_{miss}=P_{max}W_m(K)
}
\]

### Offset

令：

\[
E=|offset|,
\qquad b=13ms,
\qquad c=0.5,
\qquad k=9,
\qquad \lambda=1.2
\]

则：

\[
\boxed{
F(E)=
\begin{cases}
 c+(1-c)
 \dfrac{\ln(1+k(1-E/b))}{\ln(1+k)},&0\le E\le b\\[8pt]
 ce^{-\lambda(E-b)},&b<E<16\\[4pt]
 0,&E\ge16
\end{cases}
}
\]

### 单 Note Offset 价值

\[
\boxed{
Q_j=W(K_j)F(E_j)
}
\]

### 全谱 Offset Quality

\[
\boxed{
Q_{avg}=\frac{\sum_jW(K_j)F(E_j)}{\sum_jW(K_j)}
}
\]

### 全谱 Difficulty-weighted Miss Rate

\[
\boxed{
R_{miss}=\frac{\sum_jW_m(K_j)I_j}{\sum_jW_m(K_j)}
}
\]

### 新增评分

\[
\boxed{
S_{new}=B_oQ_{avg}-P_mR_{miss}
}
\]

### 最终成绩

\[
\boxed{
S_{final}=S_{original}+S_{new}
}
\]

---

# 21. 当前阶段结论

V1 的核心思想是：

> **KPS 描述“这个区域有多难”，Offset 描述“这个 Note 打得有多准”，Miss 描述“这个困难区域是否出现爆发失误”。**

三者职责独立。

最终希望得到的不是“所有高判定都多加一点分”，而是：

> **简单位置的好成绩不要被过度放大，高密度爆发中的稳定性和无 Miss 能力应当获得明显更高的评价。**

下一步应以真实谱面/成绩数据进行 V1 仿真，重点验证相对价值，而不是继续增加公式复杂度。
