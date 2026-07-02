# AI Companion MVP - Core Conversational Engine

## Prerequisites
- Python 3.11+

## Setup
1. Install dependencies:
   - `python -m pip install -e .[dev]`
2. Create environment file:
   - `Copy-Item .env.example .env`
3. Set `OPENAI_API_KEY` in `.env`.

## Run locally
- `python -m uvicorn app.main:app --reload`

## Run tests
- `python -m pytest`

## Performance smoke
- `python tools/perf_smoke.py`

