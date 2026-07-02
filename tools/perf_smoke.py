import asyncio
import statistics
import time

import httpx


BASE_URL = "http://127.0.0.1:8000"
HEADERS = {"Authorization": "Bearer perf-user"}


async def run_non_streaming_latency(n: int = 20) -> None:
    latencies_ms: list[float] = []
    async with httpx.AsyncClient(base_url=BASE_URL, timeout=10.0) as client:
        for idx in range(n):
            started = time.perf_counter()
            response = await client.post(
                "/api/v1/chat",
                headers=HEADERS,
                json={"session_id": "perf-session", "message": f"hello-{idx}"},
            )
            response.raise_for_status()
            elapsed_ms = (time.perf_counter() - started) * 1000.0
            latencies_ms.append(elapsed_ms)

    p95 = statistics.quantiles(latencies_ms, n=100)[94]
    print(f"Non-streaming latency p95: {p95:.2f}ms")


async def main() -> None:
    await run_non_streaming_latency()


if __name__ == "__main__":
    asyncio.run(main())

