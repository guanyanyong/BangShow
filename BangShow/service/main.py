"""BangShow 称重数据服务。

接口：
    POST /api/weight             保存/更新某个榜的数值（数值变化时才修改）
    GET  /api/weight/{bangname}  查询某个榜的当前数值
    GET  /health                 健康检查

运行：
    python main.py               # 默认监听所有网卡（0.0.0.0:8765）
                                 # → http://localhost:8765、http://127.0.0.1:8765、http://<本机IP>:8765
    BANGSHOW_HOST=127.0.0.1 python main.py   # 只允许本机访问
    BANGSHOW_PORT=9000 python main.py

数据保存在同目录的 weights.db（SQLite），删除文件即可清空数据。
"""

from __future__ import annotations

import os
import socket
import sqlite3
import threading
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from decimal import Decimal
from pathlib import Path

import uvicorn
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field, field_validator

DB_PATH = Path(__file__).resolve().with_name("weights.db")

_connection = sqlite3.connect(DB_PATH, check_same_thread=False, timeout=5.0)
_connection.row_factory = sqlite3.Row
_connection.execute(
    """
    CREATE TABLE IF NOT EXISTS weights (
        bangname   TEXT PRIMARY KEY,
        weight     INTEGER NOT NULL,
        updated_at TEXT NOT NULL
    )
    """
)
_connection.commit()

_lock = threading.Lock()


def _now() -> str:
    """UTC 时间（ISO 8601，精确到秒）。"""

    return datetime.now(timezone.utc).isoformat(timespec="seconds")


class WeightUpdate(BaseModel):
    """接口 1 的请求体。"""

    bangname: str = Field(min_length=1, max_length=64, description="榜名称，例如 bang1")
    weight: Decimal = Field(
        max_digits=12,
        decimal_places=0,
        description="数值，对应数据库 decimal(12,0)",
    )

    @field_validator("bangname")
    @classmethod
    def _strip_bangname(cls, value: str) -> str:
        value = value.strip()
        if not value:
            raise ValueError("bangname 不能为空")
        return value


class WeightUpdateResult(BaseModel):
    bangname: str
    weight: int
    updated: bool = Field(description="本次请求是否修改了数值")
    updated_at: str


class WeightQueryResult(BaseModel):
    bangname: str
    weight: int
    updated_at: str


@asynccontextmanager
async def lifespan(_: FastAPI):
    yield
    with _lock:
        _connection.close()


app = FastAPI(
    title="BangShow 称重数据服务",
    version="1.0.0",
    description="保存/查询榜的数值；只有数值发生变化时才修改记录。",
    lifespan=lifespan,
)

# 跨域（CORS）配置：
# 浏览器前端页面（如 http://localhost:8080）与本服务（如 http://127.0.0.1:8765）不同源，
# 响应里没有 Access-Control-Allow-Origin 时，浏览器会拦下 fetch 结果并提示
# “has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header”。
#
# 默认放行任意来源（回显请求的 Origin，因此带 Cookie 的请求也能正常跨域）；
# 如需收紧来源，可用环境变量指定（逗号分隔）：
#     BANGSHOW_CORS_ORIGINS=http://localhost:8080,http://192.168.1.20:8080 python main.py
_cors_env = os.environ.get("BANGSHOW_CORS_ORIGINS", "").strip()
_cors_origins = [item.strip() for item in _cors_env.split(",") if item.strip()]
_allow_all_origins = not _cors_origins or _cors_origins == ["*"]

app.add_middleware(
    CORSMiddleware,
    # 允许任意来源时用正则回显 Origin（比 "*" 兼容性更好，浏览器带凭据时也不会被拦）；
    # 指定了来源列表时只放行列表内的来源。
    allow_origins=[] if _allow_all_origins else _cors_origins,
    allow_origin_regex=r".*" if _allow_all_origins else None,
    allow_credentials=True,
    allow_methods=["GET", "POST", "OPTIONS"],
    allow_headers=["*"],
    expose_headers=["*"],
    max_age=600,
)


@app.get("/health")
def health() -> dict[str, str]:
    """健康检查，同时返回数据库文件位置。"""

    return {"status": "ok", "database": str(DB_PATH)}


@app.post("/api/weight", response_model=WeightUpdateResult)
def save_weight(payload: WeightUpdate) -> WeightUpdateResult:
    """保存数值：不存在则新增，存在且数值不同则修改；数值相同则不修改。"""

    if payload.weight != payload.weight.to_integral_value():
        raise HTTPException(status_code=422, detail="weight 必须是整数（decimal(12,0)）")

    value = int(payload.weight)
    now = _now()

    with _lock:
        row = _connection.execute(
            "SELECT weight, updated_at FROM weights WHERE bangname = ?",
            (payload.bangname,),
        ).fetchone()

        if row is None:
            _connection.execute(
                "INSERT INTO weights (bangname, weight, updated_at) VALUES (?, ?, ?)",
                (payload.bangname, value, now),
            )
            _connection.commit()
            return WeightUpdateResult(
                bangname=payload.bangname, weight=value, updated=True, updated_at=now
            )

        if int(row["weight"]) != value:
            _connection.execute(
                "UPDATE weights SET weight = ?, updated_at = ? WHERE bangname = ?",
                (value, now, payload.bangname),
            )
            _connection.commit()
            return WeightUpdateResult(
                bangname=payload.bangname, weight=value, updated=True, updated_at=now
            )

        # 数值未发生变化：不修改记录，updated_at 保持原值
        return WeightUpdateResult(
            bangname=payload.bangname,
            weight=value,
            updated=False,
            updated_at=str(row["updated_at"]),
        )


@app.get("/api/weight/{bangname}", response_model=WeightQueryResult)
def get_weight(bangname: str) -> WeightQueryResult:
    """按 bangname 查询当前数值；不存在时返回 404。"""

    name = bangname.strip()
    with _lock:
        row = _connection.execute(
            "SELECT weight, updated_at FROM weights WHERE bangname = ?",
            (name,),
        ).fetchone()

    if row is None:
        raise HTTPException(status_code=404, detail=f"bangname '{name}' 不存在")

    return WeightQueryResult(
        bangname=name, weight=int(row["weight"]), updated_at=str(row["updated_at"])
    )


def _local_ipv4_addresses() -> list[str]:
    """本机可用的 IPv4 地址（排除回环与 169.254.* 链路本地地址）。"""

    addresses: set[str] = set()
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            addresses.add(info[4][0])
    except OSError:
        pass

    # 主机名解析不到某些网卡（多网卡/无线/VPN）时，用“连一下外部地址”的方式兜底
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.connect(("8.8.8.8", 80))  # 只用来让系统选出默认出口网卡，不会真的发包
            addresses.add(probe.getsockname()[0])
    except OSError:
        pass

    return sorted(
        ip for ip in addresses if not ip.startswith("127.") and not ip.startswith("169.254.")
    )


def _print_listen_info(host: str, port: int) -> None:
    """启动时打印可访问地址（localhost / 127.0.0.1 / 本机 IP），方便前端直接复制。"""

    lines = ["", "=" * 64, f"监听地址：{host}:{port}"]

    if host in {"0.0.0.0", "::"}:
        lines.append(f"  本机（localhost）: http://localhost:{port}")
        lines.append(f"  本机（回环）     : http://127.0.0.1:{port}")
        for ip in _local_ipv4_addresses():
            lines.append(f"  局域网/本机 IP   : http://{ip}:{port}")
        lines.append(f"  接口文档         : http://localhost:{port}/docs")
        lines.append(f"提示：局域网内其它机器访问需放行 Windows 防火墙的 TCP {port} 端口（首次运行会弹窗询问）。")
    else:
        lines.append(f"  本机             : http://{host}:{port}")
        lines.append(f"  接口文档         : http://{host}:{port}/docs")
        lines.append("提示：当前只监听该地址，局域网内其它机器无法访问；需要时用 BANGSHOW_HOST=0.0.0.0 启动。")

    lines.append("=" * 64)
    print("\n".join(lines), flush=True)


if __name__ == "__main__":
    host = os.environ.get("BANGSHOW_HOST", "0.0.0.0").strip() or "0.0.0.0"
    port = int(os.environ.get("BANGSHOW_PORT", "8765"))
    _print_listen_info(host, port)
    uvicorn.run(app, host=host, port=port)
