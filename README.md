# Lily of the Valley (LOTV)

A SaaS Social Services Coordination Platform built on .NET 9.

## Overview

LOTV is a multi-project .NET 9 solution designed to coordinate social services workflows. It exposes a RESTful API backend, a Blazor WebAssembly frontend, and a shared core domain library.

## Tech Stack

- **Runtime:** .NET 9
- **API:** ASP.NET Core Web API (`Lotv.Api`)
- **Frontend:** Blazor WebAssembly (`Lotv.Web`)
- **Domain:** Class Library (`Lotv.Core`)
- **Testing:** xUnit (`tests/`)
- **Solution File:** `Lotv.slnx`

## Project Structure

```
LOTV/
├── src/
│   ├── Lotv.Api/       # ASP.NET Core Web API
│   ├── Lotv.Core/      # Shared domain / business logic
│   └── Lotv.Web/       # Blazor WebAssembly frontend
├── tests/              # xUnit test projects
├── docs/               # Documentation
├── Lotv.slnx           # .NET solution file
└── MASTER_TODO.md      # Phase tracking and task list
```

## Getting Started

### Prerequisites

- .NET 9 SDK

### Build

```bash
dotnet build Lotv.slnx
```

### Run API

```bash
cd src/Lotv.Api
dotnet run
```

### Run Tests

```bash
dotnet test
```

## Development Status

See `MASTER_TODO.md` for current phase and open tasks.

| Phase | Name | Status |
|---|---|---|
| 0 | Foundation | Complete |
| 1 | Architecture & Design | Pending |
| 2 | Core Domain | Pending |
| 3 | API | Pending |
| 4 | Frontend | Pending |
| 5 | Testing | Pending |
| 6 | Deployment & Launch | Pending |
