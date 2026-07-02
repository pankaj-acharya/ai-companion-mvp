# AI Companion MVP - Core Conversational Engine

## Prerequisites
- .NET 8 SDK

## Setup
1. Create environment file:
   - `Copy-Item .env.example .env`
2. Set `OPENAI_API_KEY` in `.env`.

## Run locally
- `dotnet run --project src/AiCompanion.Api --launch-profile http`

## API docs
- `http://localhost:8000/docs`

## Run tests
- `dotnet test AiCompanionMvp.sln`

