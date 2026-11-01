-- Vector store for the AI shopping assistant.
-- Runs once, when the PostgreSQL container starts with an empty data volume.

CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS product_embeddings (
    product_id        INTEGER       PRIMARY KEY,
    name              TEXT          NOT NULL,
    description       TEXT          NOT NULL DEFAULT '',
    price             NUMERIC(18,2) NOT NULL,
    category          TEXT          NOT NULL DEFAULT '',
    embedding         VECTOR(768)   NOT NULL,   -- nomic-embed-text produces 768 dimensions
    embedding_version TEXT          NOT NULL,   -- re-index when the embedding model changes
    is_active         BOOLEAN       NOT NULL DEFAULT TRUE,
    updated_at        TIMESTAMPTZ   NOT NULL DEFAULT CURRENT_TIMESTAMP,

    -- Full-text column used by the keyword half of the hybrid search.
    search_vector     TSVECTOR GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(name, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(category, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(description, '')), 'C')
    ) STORED
);

-- Approximate nearest-neighbour index for cosine similarity.
CREATE INDEX IF NOT EXISTS idx_product_embeddings_cosine
    ON product_embeddings USING hnsw (embedding vector_cosine_ops);

CREATE INDEX IF NOT EXISTS idx_product_embeddings_search
    ON product_embeddings USING gin (search_vector);
