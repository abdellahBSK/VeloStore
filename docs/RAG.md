# VeloStore Ollama RAG Assistant: Technical Overview

Welcome to the internal documentation for the **VeloStore AI RAG (Retrieval-Augmented Generation) Assistant**.

Converts your question into a vector → finds semantically similar products in pgvector → builds a safe mini-context → feeds it into a local LLM → generates an answer strictly grounded in your catalog.

This document details exactly how the AI retrieves data safely and reliably within our local, strict .NET 8 production environment using **Ollama** and **pgvector**.

---

## The Core Concept

Retrieval-Augmented Generation means we don't rely on the LLM's (`phi3:mini`) internal, frozen knowledge to answer questions about our catalogs. Instead, we:

1. Turn the user's question into a mathematical vector (an **Embedding**).
2. Compare that vector against pre-calculated vectors in our PostgreSQL database using `pgvector`.
3. Fetch the **Top-K** most mathematically similar products.
4. Pass those exact products as strict *context* to the LLM and ask it to answer the question using **ONLY** that info.

---

## Full Pipeline at a Glance

```
┌──────────────────────────────────────────────────────────────────────┐
│                         USER SENDS A QUERY                           │
└───────────────────────────────┬──────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────────┐
│  PHASE 0 — PROMPT SANITIZER                                          │
│  Strip injection tokens: <|system|>, <|user|>, etc.                  │
└───────────────────────────────┬──────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────────┐
│  PHASE 1 — QUERY EMBEDDING                                           │
│  POST /api/embeddings → nomic-embed-text → 768-dim float vector      │
└───────────────────────────────┬──────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────────┐
│  PHASE 2 — HYBRID RETRIEVAL (pgvector + Full-Text Search)            │
│  Semantic Score × 0.70 + Keyword Score × 0.30 = Final Score         │
│  Filter: Final Score > SIMILARITY_THRESHOLD                          │
└───────────────────────────────┬──────────────────────────────────────┘
                                │
                                ▼
┌──────────────────────────────────────────────────────────────────────┐
│  PHASE 3 — LLM GENERATION                                            │
│  POST /api/generate → phi3:mini                                      │
│  num_predict=150 | temperature=0.05 | num_ctx=2048                   │
└───────────────────────────────┬──────────────────────────────────────┘
                                │
                                ▼
                    ┌───────────────────────┐
                    │   RESPONSE TO USER    │
                    └───────────────────────┘
```

---

## Phase 0 — Background Indexing Worker

**File:** `ProductIndexingWorker.cs`

When the server starts, a background service spins up and loops every **5 minutes**. It reads `VeloStoreDbContext` to find any products missing from the `product_embeddings` pgvector table, identified by a version string (e.g. `v1-nomic-embed-text`).

When it finds a missing product, it builds a metadata text cluster:

```
Product: Trail Blazer 5000
Category: Mountain Bikes
Price: $1,200.00
Description: A high performance bike...
```

It submits this text to the local Ollama instance (`http://localhost:11434/api/embeddings`) running `nomic-embed-text`. Ollama returns a dense floating-point array of **768 dimensions**. The Worker then `UPSERT`s this array into the database alongside the product ID and search strings.

> **Why pre-index?** Computing embeddings at query time for thousands of products would be too slow. Pre-indexing means retrieval is near-instant.

---

## Phase 1 — Query Embedding

**File:** `RetrievalService.cs`

When a user types a query (e.g. *"I want a blue mountain bike under 600"*), the query is evaluated by the same `nomic-embed-text` model to produce its own **768-dimension vector**. This allows apples-to-apples mathematical comparison with the stored product vectors.

```
Query text → nomic-embed-text → [0.023, -0.412, 0.881, ..., 0.154]  (768 floats)
```

---

## Phase 2 — Hybrid Retrieval

**File:** `RetrievalService.cs` → `IVectorRepository.SearchHybridAsync()`

The `IVectorRepository` fires a `SearchHybridAsync` SQL query containing two complementary search strategies:

### 2a. Semantic Search (Vector Similarity)

```sql
(1.0 - (embedding <=> $1)) AS semantic_score
```

The `<=>` operator computes **cosine distance** between the query vector and every stored product embedding. Products semantically related to the query score higher, even if they don't share exact keywords.

### 2b. Keyword Search (Full-Text Search)

```sql
ts_rank(search_vector, websearch_to_tsquery('english', $2)) AS keyword_score
```

Native PostgreSQL Full-Text Search ranks products by exact keyword relevance.

### 2c. Blended Final Score

```
Final Score = (Semantic Score × 0.70) + (Keyword Score × 0.30)
```

| Weight | Signal | Purpose |
|--------|--------|---------|
| **70%** | Semantic | Captures intent, synonyms, context |
| **30%** | Keyword | Anchors on exact product terms |

Only results exceeding the `SIMILARITY_THRESHOLD` (Semantic > 0.40 or Final > 0.30) are passed forward. The matched products are then hydrated from the `IProductCacheService` to avoid redundant database roundtrips.

---

## Phase 3 — Generative Phase

**Files:** `RagOrchestrator.cs`, `PromptSanitizer.cs`

### 3a. Prompt Sanitization

Before anything reaches the LLM, the user's input is stripped of prompt-injection characters like `<|system|>` or `<|user|>` by the `PromptSanitizer`. This prevents jailbreak attempts from manipulating the model's behavior.

### 3b. System Prompt Construction

A strict `SystemPrompt` is forged using the retrieved products as context:

```
You are an AI assistant for VeloStore. Answer the user's question ONLY
using the following product context. Do not use any outside knowledge.

[Retrieved Products Injected Here]
```

### 3c. LLM Generation via Ollama

The prompt is submitted to Ollama running `phi3:mini` with strict generation constraints:

| Parameter | Value | Purpose |
|-----------|-------|---------|
| `num_predict` | `150` | Caps response length |
| `temperature` | `0.05` | Near-deterministic, context-faithful output |
| `num_ctx` | `2048` | Bounds memory/sliding window footprint |

### 3d. Resilience (Polly)

The entire Ollama call chain is wrapped with a **Polly resilience pipeline**:

```
┌─────────────────────────────────────┐
│  TotalRequestTimeout: 2 min         │  ← Outer hard deadline
│  ┌───────────────────────────────┐  │
│  │  CircuitBreaker               │  │  ← Opens after repeated failures
│  │  ┌─────────────────────────┐  │  │
│  │  │  Retry (1 attempt)      │  │  │  ← Retries on attempt timeout
│  │  │  ┌───────────────────┐  │  │  │
│  │  │  │ AttemptTimeout    │  │  │  │  ← 1 min per attempt
│  │  │  │  HTTP POST        │  │  │  │
│  │  │  └───────────────────┘  │  │  │
│  │  └─────────────────────────┘  │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

If Ollama fails to respond within the total budget, the circuit breaker opens and the user receives an automatic maintenance message rather than an indefinite hang.

> **Cold-start note:** On the very first request after server launch, the LLM model may not yet be loaded into GPU/RAM, causing the initial attempt to time out. Subsequent requests complete normally once the model is warm (~20s).

---

## Component Summary

| Component | Responsibility |
|-----------|---------------|
| `ProductIndexingWorker.cs` | Background pre-computation of all product embeddings |
| `ProductCacheService.cs` | In-memory cache of product data to avoid repeated DB queries |
| `RetrievalService.cs` | Hybrid semantic + keyword search against pgvector |
| `PromptSanitizer.cs` | Strips prompt injection tokens from user input |
| `RagOrchestrator.cs` | Coordinates retrieval → prompt construction → LLM call |
| **Ollama** (`nomic-embed-text`) | Local embedding model (768-dim vectors) |
| **Ollama** (`phi3:mini`) | Local generative LLM for final answer synthesis |
| **pgvector** | PostgreSQL extension for vector similarity search |
| **Polly** | Retry, timeout, and circuit-breaker resilience policies |

---

*Last updated: March 2026 — VeloStore Internal Engineering Docs*