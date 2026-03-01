# VeloStore Ollama RAG Assistant: Technical Overview

Welcome to the internal documentation for the VeloStore Artificial Intelligence RAG (Retrieval-Augmented Generation) Assistant.

This document details exactly how the AI retrieves data safely and reliably within our local, strict .NET 8 production environment using **Ollama** and **pgvector**.

## The Core Concept
Retrieval-Augmented Generation means we don't rely on the LLM's ("phi3:mini") internal, frozen knowledge to answer questions about our catalogs. Instead, we:
1. Turn the user's question into a mathematical vector (an Embedding).
2. Compare that vector against pre-calculated vectors in our PostgreSQL database using `pgvector`.
3. Fetch the Top-K most mathematically similar products.
4. Pass those exact products as strict *context* to the LLM and ask it to answer the question using ONLY that info.

## Pipeline Breakdown

### 1. Vector Population (The Background Worker)
**Where:** `ProductIndexingWorker.cs`
**How it Works:** 
When the server starts, a background service spins up and loops every 5 minutes. It reads `VeloStoreDbContext` to find any products missing from the `product_embeddings` pgvector tables based on a version string (e.g. `v1-nomic-embed-text`).
When it finds a missing product, it builds a metadata text cluster:
```text
Product: Trail Blazer 5000
Category: Mountain Bikes
Price: $1,200.00
Description: A high performance bike...
```
It submits this text to our local Ollama instance (`http://localhost:11434/api/embeddings`) running `nomic-embed-text`. Ollama returns a dense floating-point array of length 768. The Worker then `UPSERT`s this array into the database alongside the product ID and search strings.

### 2. The Retrieval Phase (Hybrid Search)
**Where:** `RetrievalService.cs`
**How it Works:**
When a user types a query (e.g. "I want a blue mountain bike under 600"), the Query is evaluated by the same `nomic` embedding model to get its 768-dimension vector.
The `IVectorRepository` fires a `SearchHybridAsync` SQL query containing:
1. **Semantic Search**: `(1.0 - (embedding <=> $1))` computes the cosine distance nearest-neighbors.
2. **Keyword Search**: `ts_rank(search_vector, websearch_to_tsquery('english', $2))` runs native Postgres Full-Text Search.
3. **Blended Score**: It multiplies the semantic search by 0.70 and the keyword by 0.30 to determine the most relevant items across both intents.

If the returned matches exceed our `SIMILARITY_THRESHOLD` (e.g., Semantic > 0.40 or Final > 0.30), they are bundled up from the heavy `IProductCacheService` and sent to the Orchestrator.

### 3. The Generative Phase (Orchestrator & Prompt Shield)
**Where:** `RagOrchestrator.cs` and `PromptSanitizer.cs`
**How it Works:**
First, the user's prompt is stripped of injection characters like `<|system|>` or `<|user|>` through the `PromptSanitizer` to prevent prompt-jailbreaking against the LLM.

Next, a super-strict `SystemPrompt` is forged containing the context discovered from Phase 2:
> "You are an AI assistant for VeloStore... Answer the user's question ONLY using the following product context: [Products Injected Here]"

Finally, we submit this prompt to Ollama running `phi3:mini` for generative completion. Memory on the GPU/RAM is strictly protected by setting constraints in the generative HTTP Request:
- `num_predict = 150` (limits response length).
- `temperature = 0.05` (creates highly deterministic, less creative outputs that adhere directly to the context).
- `num_ctx = 2048` (bounds the sliding window memory footprint).

Polly `CircuitBreaker` and timeout fallbacks surround the pipeline; if Ollama fails to respond, it automatically short-circuits and alerts the user of maintenance.
