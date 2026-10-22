# BangShow 称重数据服务

用 Python（FastAPI + SQLite）实现的称重数值服务，供 WinForms 端上报与查询。

## 1. 安装依赖

```powershell
cd service
python -m pip install -r requirements.txt
```

## 2. 启动服务

```powershell
python main.py
```

默认监听 `0.0.0.0:8765`（所有网卡），启动时会打印本机可用的地址：

```text
================================================================
监听地址：0.0.0.0:8765
  本机（localhost）: http://localhost:8765
  本机（回环）     : http://127.0.0.1:8765
  局域网/本机 IP   : http://192.168.8.62:8765
  接口文档         : http://localhost:8765/docs
================================================================
```

- 本机访问用 `http://localhost:8765` 或 `http://127.0.0.1:8765`；局域网/其它机器用 `http://<本机IP>:8765`（上面的地址照抄即可）；
- 只允许本机访问：`$env:BANGSHOW_HOST="127.0.0.1"; python main.py`
- 换端口：`$env:BANGSHOW_PORT="9000"; python main.py`
- 开发热重载：`uvicorn main:app --host 0.0.0.0 --port 8765 --reload`
- 局域网其它机器连不上时通常是 Windows 防火墙，用**管理员** PowerShell 放行端口：

```powershell
New-NetFirewallRule -DisplayName "BangShow 8765" -Direction Inbound -Protocol TCP -LocalPort 8765 -Action Allow
```

> 部分 Windows 上 `localhost` 会优先解析到 IPv6 的 `::1`，本服务只监听 IPv4；浏览器/curl 会自动回退到 `127.0.0.1`，个别不回退的客户端请直接用 `http://127.0.0.1:8765` 或本机 IP。

数据保存在 `service/weights.db`（SQLite），删除该文件即可清空所有记录。

## 3. 接口说明

### 3.1 保存数值（数值变化才修改）

```
POST /api/weight
Content-Type: application/json

{ "bangname": "bang1", "weight": 123 }
```

- `bangname`：字符串，1~64 个字符，首尾空白会被去掉
- `weight`：`decimal(12,0)`，即最多 12 位、0 位小数的整数

响应：

```json
{ "bangname": "bang1", "weight": 123, "updated": true, "updated_at": "2026-10-21T06:00:00+00:00" }
```

| 场景 | 行为 | `updated` |
| --- | --- | --- |
| 该 bangname 不存在 | 新增记录 | `true` |
| 存在且数值不同 | 修改数值与更新时间 | `true` |
| 存在且数值相同 | 不修改（`updated_at` 保持原值） | `false` |

### 3.2 查询数值

```
GET /api/weight/{bangname}
```

```json
{ "bangname": "bang1", "weight": 123, "updated_at": "2026-10-21T06:00:00+00:00" }
```

不存在时返回 `404`：

```json
{ "detail": "bangname 'bang1' 不存在" }
```

### 3.3 健康检查

```
GET /health
```

```json
{ "status": "ok", "database": "D:\\...\\service\\weights.db" }
```

### 3.4 跨域（CORS）

服务已开启 CORS，浏览器前端（例如页面在 `http://localhost:8080`、服务在 `http://localhost:8765` 或 `http://<本机IP>:8765`）可以直接 `fetch`：

- 响应头会回显请求的 `Origin`（`Access-Control-Allow-Origin: http://localhost:8080`），并带 `Access-Control-Allow-Credentials: true`；
- 预检请求 `OPTIONS` 返回 `200`，允许 `GET / POST / OPTIONS`，允许任意请求头，预检结果缓存 600 秒；
- 默认放行**任意来源**；如需收紧，用环境变量指定允许的来源（逗号分隔）：

```powershell
$env:BANGSHOW_CORS_ORIGINS="http://localhost:8080,http://192.168.1.20:8080"
python main.py
```

> 修改 `main.py` 后必须**重启服务**（`python main.py` 不会热重载），否则运行中的旧进程仍返回不带 CORS 头的响应，浏览器会继续报 `No 'Access-Control-Allow-Origin' header`。
>
> 用 `uvicorn main:app --reload` 启动可自动重载。

快速自检（返回头里应包含 `Access-Control-Allow-Origin`）：

```powershell
Invoke-WebRequest -Uri http://localhost:8765/api/weight/bang1 `
    -Headers @{ Origin = 'http://localhost:8080' } -UseBasicParsing |
    Select-Object -ExpandProperty Headers
```

## 4. 调用示例

```powershell
# 保存
Invoke-RestMethod -Method Post -Uri http://localhost:8765/api/weight `
    -ContentType 'application/json; charset=utf-8' `
    -Body '{"bangname":"bang1","weight":123}'

# 查询
Invoke-RestMethod -Uri http://localhost:8765/api/weight/bang1

# 中文 bangname 需要 URL 编码
Invoke-RestMethod -Uri "http://localhost:8765/api/weight/$([uri]::EscapeDataString('一号榜'))"
```

```bash
# curl
curl -X POST http://localhost:8765/api/weight -H "Content-Type: application/json" -d '{"bangname":"bang1","weight":123}'
curl http://localhost:8765/api/weight/bang1
```

> 换成本机 IP（如 `http://192.168.8.62:8765`）即可供局域网内其它机器调用。

## 5. 与 WinForms 端的配合

WinForms 端在 `src/BangShow/appsettings.json` 里配置服务地址与 `bangname`：

```json
{
  "Service": {
    "BaseUrl": "http://localhost:8765",
    "BangName": "bang1",
    "MaxReportsPerSecond": 2,
    "TimeoutSeconds": 5
  }
}
```

串口解析出的数值发生变化时，客户端最多每秒上报 2 次（间隔不小于 500ms）；相同数值不会重复发送。
