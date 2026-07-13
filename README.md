# AI Companion MVP - Core Conversational Engine

## Prerequisites
- .NET 8 SDK

## Setup
1. Create environment file:
   - `Copy-Item .env.example .env`
2. For local development without OpenAI credits, keep `OPENAI_USE_MOCK=true` in `.env`.
3. To use real OpenAI responses, set `OPENAI_USE_MOCK=false` and set `OPENAI_API_KEY` in `.env`.

## Run locally
- `dotnet run --project src/AiCompanion.Api --launch-profile http`

## API docs
- `http://localhost:8000/docs`

## Web UI
- `http://localhost:8000/app/`
- Enter any `user_id` and `session_id` values to exercise the local MVP flows.
- Toggle streaming mode to use the WebSocket endpoint instead of the REST chat endpoint.

## Run tests
- `dotnet test AiCompanionMvp.sln`

