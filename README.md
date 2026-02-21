# ApiStudy — MTG Scanner & Collection Manager

> API backend para gerenciamento de coleções, decks e partidas de **Magic: The Gathering**, com scanner de cartas baseado em visão computacional e busca vetorial por similaridade.

---

## Sumário

- [Visão Geral](#visão-geral)
- [Funcionalidades](#funcionalidades)
- [Stack Tecnológica](#stack-tecnológica)
- [Arquitetura](#arquitetura)
  - [Pipeline do Scanner](#pipeline-do-scanner)
  - [Estrutura de Pastas](#estrutura-de-pastas)
  - [Modelo de Dados](#modelo-de-dados)
- [Pré-requisitos](#pré-requisitos)
- [Configuração](#configuração)
- [Executando o Projeto](#executando-o-projeto)
  - [Via Docker Compose](#via-docker-compose)
  - [Via dotnet CLI](#via-dotnet-cli)
- [Endpoints da API](#endpoints-da-api)
  - [Autenticação](#autenticação)
  - [Usuários](#usuários)
  - [Coleções e Cartas](#coleções-e-cartas)
  - [Busca Scryfall](#busca-scryfall)
  - [Scanner](#scanner)
  - [Partidas](#partidas)
- [Seed do Catálogo](#seed-do-catálogo)
- [Segurança](#segurança)
- [Status do Projeto](#status-do-projeto)
- [O que não pode acontecer](#o-que-não-pode-acontecer)

---

## Visão Geral

O ApiStudy é uma API RESTful desenvolvida em **.NET Core 8** que oferece:

- Autenticação com **JWT**
- CRUD completo de **coleções de cartas** (cartas que o usuário possui)
- CRUD de **decks** montados para formatos específicos do MTG
- Registro completo de **partidas** com rastreamento de pontos de vida, contadores e efeitos
- **Busca de cartas** via API do Scryfall com suporte a paginação, autocomplete e busca por nome
- **Scanner de cartas** por câmera com identificação por visão computacional (Emgu.CV), OCR (Tesseract) e busca vetorial por similaridade (pgvector + ResNet18)

---

## Funcionalidades

### Implementadas

| Módulo | Status |
|---|---|
| Autenticação JWT | ✅ Completo |
| CRUD de Coleções | ✅ Completo |
| CRUD de Decks | ✅ Completo |
| CRUD de Partidas (Matches) | ✅ Completo |
| Consulta de cartas via Scryfall | ✅ Completo |
| Estrutura do banco de dados | ✅ Completo |
| Scanner — Detecção e recorte da carta | ✅ Completo |
| Scanner — Embedding por impressão (ResNet18 + ONNX) | ✅ Completo |
| Scanner — OCR do título (Tesseract) | ✅ Completo |
| Scanner — Busca vetorial no pgvector | ✅ Completo |
| Scanner — Motor de decisão (DecisionEngine) | ✅ Completo |
| Scanner — Seed automático do catálogo | ✅ Completo |

### Em Desenvolvimento

| Módulo | Status |
|---|---|
| Exportação de decks em múltiplos formatos | 🔄 Em desenvolvimento |
| Front-end multiplataforma (mobile/web/desktop) | 🔄 Em desenvolvimento |

---

## Stack Tecnológica

| Tecnologia | Versão | Uso |
|---|---|---|
| .NET Core | 8.0.11 | Runtime e framework web |
| Entity Framework Core | 8.0.11 | ORM e migrations |
| Npgsql (EF Core) | 8.0.11 | Driver PostgreSQL |
| Pgvector.EntityFrameworkCore | 0.2.2 | Busca vetorial no PostgreSQL |
| Microsoft.ML.OnnxRuntime | 1.24.1 | Inferência do modelo ResNet18 |
| Emgu.CV | 4.12.0.5764 | Detecção de bordas e perspectiva da carta |
| SixLabors.ImageSharp | 3.1.12 | Processamento de imagem (fallback e pré-processamento) |
| Tesseract | 5.2.0 | OCR do título da carta |
| Refit | 8.0.0 | Cliente tipado para a API do Scryfall |
| Swashbuckle.AspNetCore | 6.6.2 | Swagger/OpenAPI |
| Newtonsoft.Json | 13.0.4 | Deserialização JSON |
| JwtBearer | 8.0.0 | Autenticação JWT |
| PostgreSQL (via Docker) | 17 (pgvector image) | Banco de dados principal |

---

## Arquitetura

### Pipeline do Scanner

O scanner executa em ~250–450ms por tentativa seguindo este fluxo:

```
Imagem bruta (câmera)
        │
        ▼
[0] CardDetectionService.DetectAndCrop()         ~40–80ms
    ├─ Emgu.CV: Canny → FindContours → WarpPerspective
    └─ Fallback: ImageSharp crop central por proporção MTG
        │
        ▼  imagem 488×680px (proporção MTG)
        │
   ┌────┴────┐  Task.WhenAll (paralelo)
   ▼         ▼
[A] VectorService     [B] OcrService
    .GenerateEmbedding()   .ReadCardTitle()
    ~150–300ms             ~30–70ms
    ResNet18 via ONNX      Tesseract LSTM
    Crop apenas na arte    Região do título
    (8%–85% da altura)     (0–8% da altura)
   └────┬────┘
        │ float[512] + OcrResult(título, score)
        ▼
[C] IScannerRepository.FindClosestCardsAsync()   ~5–15ms
    pgvector HNSW — distância cossenoidal
    top-10 CardPrintings mais próximos
        │
        ▼  OCR hit (score ≥ 0.70)?
        │  → injeta candidato Distance=0.0 no topo
        ▼
[D] DecisionEngine.Decide()                      ~1ms
    Vota entre vetor e OCR
        │
        ▼
   ScannerResult {status, confidence, card, alternatives}
```

**Por que somente a arte é processada pelo ResNet18?**

Uma carta MTG tem regiões com conteúdo visual muito distinto. O título, custo de mana, tipo e texto de regras são idênticos entre impressões diferentes da mesma carta. Processar a carta inteira fazia o modelo confundir cartas com frame similar (ex: Plains ONE full art com Drake-Skull Cameo, ambos com fundo escuro). Processando apenas a arte (região y=8%–85%), o modelo compara paisagens vs criaturas vs artefatos, eliminando falsas correspondências.

**Tabela de Decisão do DecisionEngine:**

| Condição | Status | Confiança |
|---|---|---|
| OCR injetou top-0 com dist=0 (hit por nome) | Confirmed | High |
| dist < 0.30 | Confirmed | High |
| dist < 0.42 | Confirmed | Medium |
| dist < 0.42 e OCR discorda (score ≥ 0.90) | Ambiguous | Medium |
| dist < 0.52 e OCR falhou | Confirmed | Low |
| dist ≥ 0.42 e OCR acertou | Confirmed | High |
| dist ≥ 0.52 e OCR falhou e attempt ≥ MaxAttempts | NotFound | — |
| dist ≥ 0.42 e OCR falhou e attempt < Max | RescanRequired | — |
| Todos os outros casos | RescanRequired | — |

---

### Estrutura de Pastas

```
ApiStudy/
├── Assets/
│   └── resnet18.onnx              # Modelo de embedding (512 dims) — não versionado
├── Controllers/
│   ├── AuthController.cs          # Login e reset de senha
│   ├── CardController.cs          # CRUD de coleções e cartas
│   ├── CardSearchController.cs    # Proxy tipado para a API do Scryfall
│   ├── MatchController.cs         # CRUD de partidas e contadores
│   ├── ScannerController.cs       # Scanner de cartas + seed do catálogo
│   └── UsersController.cs         # CRUD de usuários
├── Filters/
│   └── EnsureUserFilter.cs        # Action filter — valida usuário logado
├── Mappers/
│   ├── CardMap.cs
│   ├── CardPrintMap.cs            # Configuração EF Core de CardPrinting
│   ├── CollectionMap.cs
│   ├── DeckMap.cs
│   ├── FeatureMap.cs
│   ├── MatchMap.cs
│   ├── OracleCardMap.cs
│   └── UsersMap.cs
├── Migrations/                    # Histórico de migrations EF Core
├── Models/
│   ├── Auth/                      # User, Feature, Login, ResetLogin
│   ├── Cards/                     # Card, Collection, Deck
│   ├── Match/                     # Match, Player, enums de formato e contador
│   ├── Scanner/                   # OracleCard, CardPrinting, DTOs do scanner
│   └── Scryfall/                  # Modelos da API Scryfall (IScryfallApi via Refit)
├── Repository/
│   ├── Context/
│   │   └── DatabaseContext.cs     # DbContext com pgvector habilitado
│   ├── BaseRepository.cs          # Repositório genérico CRUD
│   ├── IRepository.cs
│   ├── IScannerRepository.cs      # Contrato do repositório do scanner
│   ├── ScannerRepository.cs       # Implementação: pgvector + EF Core
│   └── UserRepository.cs
├── Services/
│   ├── CardDetectionService.cs    # Estágio 0: recorte da carta (Emgu.CV + fallback)
│   ├── CardServices.cs            # Lógica de negócio de coleções
│   ├── CryptingService.cs         # SHA-256 para senhas
│   ├── DecisionEngine.cs          # Motor de votação vetor + OCR
│   ├── MatchService.cs            # Lógica de negócio de partidas
│   ├── OcrService.cs              # Leitura do título (Tesseract)
│   ├── ScannerService.cs          # Orquestrador da pipeline + seed
│   ├── SeedBackgroundService.cs   # Background service: seed automático a cada 24h
│   ├── TokenServices.cs           # Geração e validação JWT
│   └── VectorServices.cs          # Embedding ResNet18 via ONNX
├── tessdata/                      # Modelos de idioma do Tesseract — não versionados
│   └── eng.traineddata
├── wwwroot/
│   └── scanner.html               # Cliente de teste do scanner (browser)
├── appsettings.json
├── docker-compose.yml
├── Dockerfile
└── Program.cs
```

---

### Modelo de Dados

```
Users ──────────────────────────┐
  │                              │
  ├── Collections                │
  │     └── Cards                │
  │                              │
  ├── Decks                      │
  │     └── Cards                │
  │                              │
  └── Matches ───────────────────┘
        └── Players
              ├── LifeTotal
              ├── PoisonCounters
              ├── EnergyCounters
              ├── ExperienceCounters
              └── CommanderDamageSuffered[]

OracleCards (1 por oracle_id Scryfall)
  └── CardPrintings (N por OracleCard)
        ├── SetCode + CollectorNumber
        ├── ImageUrl
        ├── IsLatestPrinting
        └── Embedding: vector(512)  ← índice HNSW pgvector
```

**Formatos de partida suportados:** Commander/EDH, Commander 2HG, Draft/Sealed, Brawl 2 Jogadores, Brawl Multiplayer, Pauper, Standard, Pioneer, Modern, Legacy, Vintage, Oathbreaker, Alchemy, Historic, Timeless, Penny Dreadful.

**Tipos de contador por jogador:** Veneno, Experiência, Energia, Carga, Atordoamento, Tempo e Customizados.

---

## Pré-requisitos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker + Docker Compose](https://docs.docker.com/get-docker/) (recomendado para o PostgreSQL)
- Modelo ONNX `resnet18.onnx` em `Assets/` (não versionado — ver abaixo)
- Dados de idioma do Tesseract em `tessdata/eng.traineddata` (não versionados — ver abaixo)

### Obtendo os arquivos não versionados

**Modelo ResNet18 (ONNX):**
```bash
# O modelo deve ser exportado do PyTorch com saída de embedding de 512 dims
# e colocado em Assets/resnet18.onnx com CopyToOutputDirectory = Always
```

**Tesseract eng.traineddata:**
```bash
mkdir -p tessdata
curl -L https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata \
     -o tessdata/eng.traineddata
```

---

## Configuração

Crie o arquivo `appsettings.Development.json` (ignorado pelo `.gitignore`) com base no exemplo abaixo:

```json
{
  "ConnectionStrings": {
    "DatabaseConnection": "Host=localhost;Port=5432;Database=mtg_scanner;Username=postgres;Password=SUA_SENHA"
  },
  "JwtSettings": {
    "Secret": "sua-chave-secreta-com-pelo-menos-32-caracteres",
    "Issuer": "ApiStudy",
    "Audience": "ApiStudyClients",
    "ExpiresInMinutes": 60
  },
  "ScryfallConfigs": {
    "ScryfallApiUrl": "https://api.scryfall.com",
    "ScryfallApiRequiredHeaders": {
      "Accept": "application/json",
      "User-Agent": "ApiStudy/v1.0 (seuemail@dominio.com)"
    }
  }
}
```

> **Atenção:** O User-Agent enviado ao Scryfall deve conter informações de contato conforme a [política de uso da API](https://scryfall.com/docs/api). Requisições sem User-Agent são rejeitadas com HTTP 400.

---

## Executando o Projeto

### Via Docker Compose

O `docker-compose.yml` sobe a API e o PostgreSQL com pgvector juntos:

```bash
docker-compose up -d
```

A API ficará disponível em `http://localhost:5000` e o Swagger em `http://localhost:5000/swagger`.

O PostgreSQL fica em `localhost:5432` com:
- Database: `mtg_scanner`
- User: `postgres`
- Password: conforme definido no `docker-compose.yml`

> **Atenção em produção:** altere as credenciais do PostgreSQL no `docker-compose.yml` antes de subir em ambiente público. O arquivo atual contém a senha padrão de desenvolvimento.

### Via dotnet CLI

```bash
# 1. Suba o PostgreSQL (Docker)
docker-compose up -d postgres-db

# 2. Restaure os pacotes
dotnet restore

# 3. Aplique as migrations
dotnet ef database update

# 4. Execute a API
dotnet run
```

A API estará disponível em `http://localhost:5000` (ou conforme `launchSettings.json`).

### Migrations

```bash
# Criar nova migration após alterar os modelos
dotnet ef migrations add NomeDaMigration

# Aplicar migrations pendentes
dotnet ef database update

# Reverter última migration
dotnet ef database update NomeDaMigrationAnterior
```

---

## Endpoints da API

Todos os endpoints (exceto login e criação de usuário) requerem o header:
```
Authorization: Bearer <token>
```

### Autenticação

| Método | Rota | Autenticação | Descrição |
|---|---|---|---|
| `POST` | `/api/auth/login` | ❌ Público | Login — retorna JWT |
| `POST` | `/api/auth/reset-pwd` | ✅ JWT | Altera senha do usuário |

**Login — Request:**
```json
{
  "email": "usuario@email.com",
  "senha": "minhasenha"
}
```

**Login — Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs..."
}
```

---

### Usuários

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/users` | Lista todos os usuários |
| `GET` | `/api/users/{id}` | Busca usuário por ID |
| `POST` | `/api/users` | Cria novo usuário |
| `PUT` | `/api/users` | Atualiza usuário |
| `DELETE` | `/api/users/{id}` | Remove usuário |

---

### Coleções e Cartas

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/card` | Lista coleções do usuário logado |
| `POST` | `/api/card/new-collection` | Cria nova coleção |
| `POST` | `/api/card/cards-collection` | Adiciona carta a uma coleção |

**Criar coleção — Request:**
```json
{
  "name": "Minha Coleção Principal",
  "description": "Cartas do formato Commander"
}
```

**Adicionar carta — Request:**
```json
{
  "card": "uuid-da-carta",
  "collectionId": "uuid-da-colecao"
}
```

---

### Busca Scryfall

Proxy tipado para a API do Scryfall. Os parâmetros seguem a [documentação oficial do Scryfall](https://scryfall.com/docs/api/cards/search).

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/card-search?query=...` | Busca fulltext de cartas |
| `GET` | `/api/card-named?exact=...` | Busca carta por nome exato ou fuzzy |
| `GET` | `/api/card-id/{id}` | Busca carta pelo Scryfall ID |
| `GET` | `/api/cards-autocomplete?q=...` | Autocomplete de nomes de cartas |

**Exemplo de busca:**
```
GET /api/card-search?query=lightning+bolt&unique=prints&order=released
```

---

### Scanner

| Método | Rota | Autenticação | Descrição |
|---|---|---|---|
| `POST` | `/api/scanner/identify` | ⚠️ Desabilitado em dev | Identifica carta por imagem |
| `GET` | `/api/scanner/seed/{setCode}` | ❌ Público | Popula catálogo com um set |

#### Identificar carta

**Request:** `multipart/form-data`
- `file`: imagem da carta (JPEG, PNG ou WebP, máx. ~5MB recomendado)
- `attempt` (query, opcional): número da tentativa anterior (default: 0)

```bash
curl -X POST http://localhost:5000/api/scanner/identify \
  -F "file=@foto_da_carta.jpg"
```

**Response:**
```json
{
  "status": "confirmed",
  "confidence": "high",
  "confidenceScore": 0.9823,
  "detectionMethod": "ocr+vector",
  "processingTimeMs": 312,
  "rescanAttempt": 1,
  "card": {
    "oracleId": "3b4e3f8d-...",
    "name": "Lightning Bolt",
    "setCode": "M11",
    "collectorNumber": "149",
    "imageUrl": "https://cards.scryfall.io/normal/...",
    "releasedAt": "2010-07-16",
    "confidenceScore": 0.9823
  },
  "alternativeCandidates": []
}
```

**Valores de `status`:**

| Valor | Significado |
|---|---|
| `confirmed` | Carta identificada com confiança suficiente |
| `rescan_required` | Imagem insuficiente — capture nova foto e reenvie com `attempt` incrementado |
| `ambiguous` | Vetor e OCR divergem — `alternativeCandidates` contém as opções |
| `not_found` | Não foi possível identificar após máximo de tentativas |

#### Seed de set

Popula o banco com metadados e embeddings de um set completo do Scryfall. Operação idempotente.

```bash
# Seed do set Modern Horizons 3
GET /api/scanner/seed/mh3

# Seed das Foundations
GET /api/scanner/seed/fdn
```

**Response:**
```json
{
  "status": "success",
  "set": "MH3",
  "cardsProcessed": 303,
  "embeddingsGenerated": 303,
  "message": "303 embeddings gerados. O set está pronto para uso."
}
```

---

### Partidas

| Método | Rota | Descrição |
|---|---|---|
| `GET` | `/api/match` | Lista partidas do usuário logado |
| `POST` | `/api/match` | Cria nova partida |
| `PATCH` | `/api/match` | Atualiza jogadores da partida |
| `PATCH` | `/api/match/lifepoints` | Atualiza pontos de vida e placar |
| `PATCH` | `/api/match/update-counters` | Atualiza contadores de um jogador |

**Criar partida — Request:**
```json
{
  "matchFormat": 0,
  "matchType": 0,
  "players": [
    { "name": "Jogador 1", "lifeTotal": 40 },
    { "name": "Jogador 2", "lifeTotal": 40 }
  ]
}
```

> `matchFormat`: 0=CommanderEDH, 6=Standard, 7=Pioneer, 8=Modern, 9=Legacy...
> `matchType`: 0=Casual, 1=Competitive, 2=Tournament

**Atualizar pontos de vida — Request:**
```json
{
  "matchId": "uuid-da-partida",
  "playerId": "uuid-do-jogador",
  "lifepoints": 35
}
```

**Atualizar contadores — Request:**
```json
{
  "matchId": "uuid-da-partida",
  "playerId": "uuid-do-jogador",
  "counterType": 0,
  "counterValue": 3
}
```

> `counterType`: 0=Poison, 1=Experience, 2=Energy, 3=Charge, 4=Stun, 5=Time, 6=Custom

---

## Seed do Catálogo

O catálogo de cartas (necessário para o scanner funcionar) pode ser populado de duas formas:

**1. Manualmente via endpoint (recomendado para desenvolvimento):**
```bash
# Seeds individuais por set
GET /api/scanner/seed/fdn   # Foundations
GET /api/scanner/seed/dsk   # Duskmourn
GET /api/scanner/seed/mh3   # Modern Horizons 3
GET /api/scanner/seed/one   # Phyrexia: All Will Be One
```

**2. Automaticamente via SeedBackgroundService:**

O `SeedBackgroundService` executa em background ao iniciar a aplicação, varrendo todos os sets conhecidos e processando automaticamente aqueles ainda não semeados. O ciclo se repete a cada 24 horas.

O serviço processa mais de 600 set codes históricos do Scryfall. Sets inexistentes no catálogo atual do Scryfall são detectados automaticamente (HTTP 404/400) e ignorados.

> **Tempo estimado:** Seeds completos levam tempo considerável devido ao rate limit respeitado de 100ms entre páginas e 150ms entre downloads de imagem. Para desenvolvimento, semeie apenas os sets que pretende testar.

---

## Segurança

- Senhas armazenadas com **SHA-256** (sem salt — melhoria futura recomendada: bcrypt/Argon2)
- Autenticação via **JWT Bearer Token** com validação de issuer, audience e expiração
- Filter `[EnsureUser]` garante que o `UserId` do token corresponde a um usuário real antes de executar a action
- `RequireHttpsMetadata = false` nas configurações JWT — **alterar para `true` em produção com HTTPS**
- CORS configurado como `AllowAll` para desenvolvimento — **restringir em produção**

---

## Status do Projeto

| Sprint | Módulo | Status |
|---|---|---|
| Sprint 1 | JWT Auth + CRUD base | ✅ Concluído |
| Sprint 2 | Scanner v1 (AKAZE/BFMatcher) | ✅ Substituído |
| Sprint 2 | Scanner v2 (ResNet18 + pgvector) | ✅ Concluído |
| Sprint 3 | Calibração de thresholds do scanner | ✅ Concluído |
| Sprint 3 | Seed automático (SeedBackgroundService) | ✅ Concluído |
| Sprint 4 | Exportação de decks | 🔄 Em desenvolvimento |
| Sprint 4 | Front-end multiplataforma | 🔄 Em desenvolvimento |

---

## O que não pode acontecer

- **Scanner retornar carta errada** — a pipeline prioriza precisão sobre velocidade. `RescanRequired` é preferível a uma identificação incorreta.
- **Partidas não registradas corretamente** — o `EnsureUserFilter` valida autenticação antes de qualquer operação em partidas.
- **Acesso não autenticado a rotas protegidas** — todos os controllers (exceto `AuthController` e o scanner em modo de desenvolvimento) exigem token JWT válido.