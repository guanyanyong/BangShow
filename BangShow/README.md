# BangShow — 串口数据接收工具

WinForms 桌面程序：通过串口接收固定长度（12 字节）的数据帧，缓冲原始字节、拆包解析并实时显示数值、单位与原始帧。

## 协议

每帧固定 12 字节：

| 偏移 | 长度 | 说明 |
| --- | --- | --- |
| 0 | 1 | 帧头，固定 `0x02` (STX) |
| 1 | 1 | 符号字节：`'+'` / `0x00` 表示正，`'-'` / `0x01` / `0xFF` 表示负 |
| 2 | 8 | 数值字段，ASCII 文本（如 `00123.45`）或 BCD（16 位十进制数字） |
| 10 | 1 | 单位/状态字节：可打印字符（`0x21`~`0x7E`）按单位显示，否则按状态码显示 |
| 11 | 1 | 帧尾，固定 `0x03` (ETX) |

解析规则：

- 数值字段先按所选格式解码，再取绝对值，最后按符号字节施加正负号（符号以独立字节为准）。
- ASCII 格式会去掉首尾的空格与 `\0`；无法解析为数字时界面用红色显示十六进制文本。
- BCD 格式每字节表示两位十进制数字；出现非 BCD 半字节时回退为十六进制文本。
- 字节流中出现噪声、半包、错误帧尾时会自动重新同步：跳过无效字节并计入“丢弃”统计。

## 处理流程

```
SerialPort.DataReceived
        │  只做一件事：读取字节
        ▼
ByteQueueBuffer（线程安全环形缓存，写满时覆盖最旧字节）
        │  后台解析线程按块读取
        ▼
SensorFrameParser（处理半包 / 粘连帧 / 噪声重同步）
        │  完整帧
        ▼
ConcurrentQueue<SensorFrame>
        │  界面 50ms 定时器清空队列（只保留最新一帧）
        ▼
FrameRateLimiter（每秒最多处理 10 帧，超出部分过滤）
        ▼
MainForm（显示最新一帧 + 记录日志 + 统计过滤数量）
        │  数值变化时
        ▼
WeightReporter（每秒最多上报 2 次，失败自动重试）
        ▼
WeightApiClient → Python 服务 POST /api/weight
```

把“读串口”和“解析”分离到不同线程，界面只用定时器轮询队列，因此不会出现跨线程访问控件的问题，也不会因为界面卡顿导致串口缓冲区溢出。

### 高频限流

设备发送频率较高时，界面不会跟着刷屏：`FrameRateLimiter` 保证两次处理之间至少间隔 `1/10` 秒（约 100ms），即**每秒最多处理 10 帧**；未到间隔的帧直接丢弃，只保留每个处理周期内最新的一帧显示。

- “有效帧”统计的是后台实际解析出的帧数（不丢数据）；
- “显示”是真正刷新到界面的次数（≤ 10 次/秒）；
- “过滤”是被限流丢弃、未显示的帧数。

限流只作用于界面刷新和日志记录，解析线程仍会读完缓存中的全部字节，保证串口缓冲区不会因为界面慢而溢出。

### 数值上报（Python 服务）

数值变化时会自动上报到 `service/` 下的 Python 服务（详见 [service/README.md](service/README.md)）：

- 只在数值**发生变化**时发送；相同数值不会重复上报；
- 每秒最多发送 **2 次**（两次之间至少间隔 500ms），高频变化时只补发最新值（丢弃中间值，保证最终一致）；
- 发送失败（服务未启动、超时等）会保留最新值并自动重试，日志只在首次失败时提示，恢复后给出累计失败次数；
- 上报失败只影响日志提示，不影响串口接收与界面显示；
- `bangname`、服务地址、上报频率、超时时间都在 `src/BangShow/appsettings.json` 中配置（随程序复制到输出目录）。
- 服务已开启 **CORS**：浏览器前端（例如页面在 `http://localhost:8080`）可以直接 `fetch` 本服务接口，响应会回显请求的 `Origin`；如需限定来源，启动前设置环境变量 `BANGSHOW_CORS_ORIGINS`（详见 [service/README.md](service/README.md) 第 3.4 节）。

### 地磅数值大屏

点“实时数据”区域右上角的 **显示数值** 按钮，会打开一个无边框全屏的“地磅数值”大屏（`WeightDisplayForm`）：

- 画面里只有一个大号数值（原始值 **÷ 100** 后舍去小数，带发光效果）和它正下方的绿色指示球；
- 数值**发生变化**时指示球开始闪烁：0.1 秒一次、共 3 次（约 0.5 秒），交替“暗 / 亮”；数值**稳定不变**时停止闪烁，保持常亮；
- 数值持续变化时每次变化都会重新计时，因此会一直在闪；
- 指示球下方的小字显示“等待数据 / 数值变化中 / 数值稳定”，右下角显示最近一次变化的时刻；
- 默认显示在**主窗口所在屏幕**，按 `F11`（或 `←` / `→`）在多个显示器之间切换，按 `Esc` 或双击退出；
- 关闭大屏后，再点按钮会重新打开；关闭主窗口时大屏一并关闭。

闪烁逻辑由 `BangShow.Core.Display.BlinkIndicator` 实现（间隔 100ms 由界面定时器驱动），已有单元测试覆盖闪烁次数与“稳定后常亮”的行为。

## 项目结构

```
BangShow.sln
src/BangShow/                     WinForms 界面（net8.0-windows）
  MainForm.cs / MainForm.Designer.cs
  WeightDisplayForm.cs            地磅数值大屏（大号数值 + 绿色指示球）
  Program.cs
  AppSettings.cs                  读取 appsettings.json
  appsettings.json                服务地址 / bangname / 上报频率
src/BangShow.Core/                与界面无关的核心逻辑（net8.0）
  Api/WeightApiClient.cs          Python 服务 HTTP 客户端（接口 1、2）
  Api/WeightReporter.cs           数值变化检测 + 每秒最多 2 次 + 失败重试
  Api/WeightServiceOptions.cs     服务配置与校验
  Buffers/ByteQueueBuffer.cs      线程安全环形字节缓存
  Display/BlinkIndicator.cs       指示球闪烁状态机（0.1s × 3 次，稳定后常亮）
  FrameRateLimiter.cs             固定最小间隔限流器
  Protocol/SensorFrame.cs         单帧解析与展示
  Protocol/SensorFrameParser.cs   字节流拆帧、重同步
  Protocol/NumericFormat.cs       数值编码：Ascii / Bcd
  Serial/SerialPortService.cs     串口采集服务
  Serial/SerialPortSettings.cs    串口参数
  Serial/SerialPortFaultEventArgs.cs
service/                          Python 称重数据服务（FastAPI + SQLite）
  main.py                         POST /api/weight、GET /api/weight/{bangname}
  requirements.txt
  README.md                       安装、启动与接口说明
tests/BangShow.Core.Tests/        xUnit 单元测试
```

## 构建与运行

需要 .NET 8 SDK 或更高版本（Windows）。

```powershell
dotnet build BangShow.sln
dotnet run --project src/BangShow/BangShow.csproj
```

如需数值上报，先启动 Python 服务（需要 Python 3.10+）：

```powershell
cd service
python -m pip install -r requirements.txt
python main.py            # 默认监听 0.0.0.0:8765（http://localhost:8765、http://<本机IP>:8765 都能访问）
```

服务地址、`bangname`、上报频率在 `src/BangShow/appsettings.json` 中配置（本机测试可改成 `http://localhost:8765` 或 `http://<本机IP>:8765`）；服务未启动时程序照常收数，仅在日志中提示上报失败。

运行测试：

```powershell
dotnet test tests/BangShow.Core.Tests/BangShow.Core.Tests.csproj
```

## 界面使用

1. 在“串口设置”中选择 COM 口（可点“刷新端口”重新枚举）、波特率、数据位、校验位、停止位。
2. 根据设备实际编码选择“数值格式”：`ASCII 文本` 或 `BCD 编码`。该选项在打开串口后不可修改。
3. 点击“打开串口”开始接收；再次点击“关闭串口”停止接收。打开状态下串口参数会被锁定。
4. “实时数据”区域显示最新一帧的数值（含单位）、符号字节、单位/状态字节、原始帧十六进制，以及接收字节数、有效帧数、显示次数、过滤帧数、上报次数、丢弃字节数、当前缓存字节数和缓存溢出字节数。
5. “通信日志”每秒最多记录 10 帧（与界面刷新频率一致）；数值变化时还会记录上报结果（最多 2 行/秒），超过 400 行时自动清理最早内容。
6. 需要远距离观看或投到副屏时，点“实时数据”右上角的 **显示数值**：只显示地磅数值（原始值 ÷ 100，不保留小数）、固定单位 `kg` 与大号绿色指示球（数值变化时闪烁 3 次，稳定后常亮）；`Esc` / 双击退出，`F11` 切换屏幕。

## 说明与限制

- 帧长固定为 12 字节，帧头帧尾固定为 `0x02` / `0x03`；若设备协议不同，需要同步修改 `SensorFrameParser` 中的常量。
- 数值字段上限为 8 字节（ASCII 最多 8 个字符，BCD 为 16 位十进制）；超出范围由 `decimal` 解析结果决定，解析失败时显示十六进制。
- 接收缓存默认 64 KB（`SerialPortService` 构造函数可调），溢出时覆盖最旧数据并计入“溢出”统计。
- 未校验校验和/CRC（协议中无校验字段）；如需校验请在 `SensorFrame.Parse` 中扩展。
- 服务端 `weight` 为 `decimal(12,0)`：上报前按四舍五入取整（如 `123.6` → `124`）；绝对值超过 `999999999999` 的值不上报。
- 上报是“最终一致”的：高频变化时中间值可能被丢弃，但最后一次变化一定会补发到服务端。
- 地磅数值大屏显示的数值 = 设备原始值 **÷ 100** 后舍去小数（如 `2836099` → `28360`，设备以 1/100 为单位）；因此原始值变化不到 100 时大屏数字不变、也不会触发闪烁。上报到服务的数值仍是原始值四舍五入后的整数。
- 大屏的单位**固定显示 `kg`**（常量 `MainForm.DisplayWeightUnit`），不跟随帧里的单位字节；帧里的单位仍会显示在“实时数据”区域，需要换单位时改这个常量即可。
- 超出 `decimal(12,0)` 范围的值既不显示也不上报。
