# Unravel

Full-stack application built with ASP.NET Core 8 (Hexagonal Architecture) + Angular 20.

## Tech Stack

- **Backend:** ASP.NET Core 8, EF Core 8, PostgreSQL (Docker)
- **Architecture:** Hexagonal (Ports & Adapters)
- **Auth:** JWT + Refresh Tokens, BCrypt
- **Frontend:** Angular 20 (standalone components, signals)

---

## Getting Started

### Prerequisites

- [Docker](https://www.docker.com/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) and [Angular CLI 20](https://angular.io/cli)

### 1. Configure environment variables

Copie os arquivos de exemplo e preencha com seus valores — **nunca versione os arquivos reais**:

```bash
# Docker (banco + api container)
cp .env.example .env

# API local (desenvolvimento sem Docker)
cp backend/src/Unravel.API/appsettings.Development.json.example \
   backend/src/Unravel.API/appsettings.Development.json
```

> `.env` e `appsettings.Development.json` estão no `.gitignore` e nunca serão commitados.

### 2. Start the database

```bash
docker-compose up postgres -d
```

### 3. Create the initial migration

```bash
cd backend
dotnet ef migrations add InitialCreate -p src/Unravel.Infrastructure -s src/Unravel.API
```

### 4. Run the API

```bash
dotnet run --project src/Unravel.API
```

> Migrations are applied automatically on startup. Swagger is available at `http://localhost:5000/swagger`.

### 5. Run the frontend

```bash
cd frontend
npm install
ng serve
```

Frontend runs at `http://localhost:4200`.

---

## API Endpoints

| Method | Endpoint          | Auth   | Description            |
| ------ | ----------------- | ------ | ---------------------- |
| POST   | /api/users        | No     | Register a new user    |
| POST   | /api/auth/login   | No     | Login, returns JWT     |
| POST   | /api/auth/refresh | No     | Refresh access token   |
| GET    | /api/users/me     | Bearer | Get authenticated user |

---

## Running with Docker (full stack)

```bash
docker-compose up --build
```

This starts both the PostgreSQL database and the API container.
