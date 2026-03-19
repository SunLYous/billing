CREATE TABLE IF NOT EXISTS billing_batches (
                                               id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at    TIMESTAMPTZ,
    status          VARCHAR(20) NOT NULL DEFAULT 'Pending',
    total_calls     INT NOT NULL DEFAULT 0,
    processed_calls INT NOT NULL DEFAULT 0,
    error_message   VARCHAR(2000)
    );

CREATE TABLE IF NOT EXISTS calls (
                                     id               BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                     start_time       TIMESTAMPTZ NOT NULL,
                                     end_time         TIMESTAMPTZ NOT NULL,
                                     calling_party    VARCHAR(30) NOT NULL,
    called_party     VARCHAR(30) NOT NULL,
    call_direction   VARCHAR(20) NOT NULL,
    disposition      VARCHAR(20) NOT NULL,
    duration         INT NOT NULL DEFAULT 0,
    billable_seconds INT NOT NULL DEFAULT 0,
    pbx_charge       NUMERIC(12,4) NOT NULL DEFAULT 0,
    account_code     VARCHAR(100),
    call_id          VARCHAR(100) NOT NULL,
    trunk_name       VARCHAR(100),
    batch_id         UUID NOT NULL REFERENCES billing_batches(id) ON DELETE CASCADE
    );

CREATE INDEX IF NOT EXISTS ix_calls_batch_id ON calls (batch_id);
CREATE INDEX IF NOT EXISTS ix_calls_calling_party ON calls (calling_party);
CREATE UNIQUE INDEX IF NOT EXISTS ix_calls_batch_callid
    ON calls (batch_id, call_id);

CREATE TABLE IF NOT EXISTS tariffs (
                                       id              INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                       prefix          VARCHAR(20) NOT NULL,
    destination     VARCHAR(200) NOT NULL,
    rate_per_minute NUMERIC(12,4) NOT NULL,
    connection_fee  NUMERIC(12,4) NOT NULL DEFAULT 0,
    timeband        VARCHAR(20),
    weekday         VARCHAR(20),
    priority        INT NOT NULL DEFAULT 0,
    effective_date  DATE,
    expiry_date     DATE,
    batch_id        UUID NOT NULL REFERENCES billing_batches(id) ON DELETE CASCADE
    );

CREATE INDEX IF NOT EXISTS ix_tariffs_batch_id ON tariffs (batch_id);
CREATE INDEX IF NOT EXISTS ix_tariffs_batch_prefix ON tariffs (batch_id, prefix);

CREATE TABLE IF NOT EXISTS subscribers (
                                           id           INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                           phone_number VARCHAR(30) NOT NULL,
    client_name  VARCHAR(200) NOT NULL,
    batch_id     UUID NOT NULL REFERENCES billing_batches(id) ON DELETE CASCADE
    );

CREATE INDEX IF NOT EXISTS ix_subscribers_batch_id ON subscribers (batch_id);
CREATE INDEX IF NOT EXISTS ix_subscribers_batch_phone ON subscribers (batch_id, phone_number);

CREATE TABLE IF NOT EXISTS rated_calls (
                                           id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                           call_id         BIGINT NOT NULL REFERENCES calls(id) ON DELETE CASCADE,
    tariff_id       INT NOT NULL REFERENCES tariffs(id) ON DELETE CASCADE,
    connection_fee  NUMERIC(12,4) NOT NULL DEFAULT 0,
    minutes_cost    NUMERIC(14,4) NOT NULL,
    calculated_cost NUMERIC(14,4) NOT NULL,
    batch_id        UUID NOT NULL REFERENCES billing_batches(id) ON DELETE CASCADE
    );

CREATE INDEX IF NOT EXISTS ix_rated_calls_batch_id ON rated_calls (batch_id);
CREATE INDEX IF NOT EXISTS ix_rated_calls_batch_call ON rated_calls (batch_id, call_id);