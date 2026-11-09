CREATE SCHEMA kraken;

CREATE TABLE kraken.asset_pairs
(
    name VARCHAR(32) NOT NULL PRIMARY KEY
);

CREATE TABLE kraken.order_types
(
    id   INT         NOT NULL PRIMARY KEY,
    name VARCHAR(32) NOT NULL UNIQUE
);

CREATE TABLE kraken.trade_types
(
    id   INT         NOT NULL PRIMARY KEY,
    name VARCHAR(32) NOT NULL UNIQUE
);

CREATE TABLE kraken.ohlcvt
(
    id               BIGINT           GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    asset_pair       VARCHAR(32)      NOT NULL REFERENCES kraken.asset_pairs (name),
    interval_minutes INT              NOT NULL,
    timestamp        BIGINT           NOT NULL,
    open             DOUBLE PRECISION NOT NULL,
    high             DOUBLE PRECISION NOT NULL,
    low              DOUBLE PRECISION NOT NULL,
    close            DOUBLE PRECISION NOT NULL,
    volume           DOUBLE PRECISION NOT NULL,
    trades           INT              NOT NULL,
    UNIQUE (asset_pair, interval_minutes, timestamp)
);

CREATE TABLE kraken.time_and_sales
(
    asset_pair    VARCHAR(32)      NOT NULL REFERENCES kraken.asset_pairs (name),
    trade_id      BIGINT           NOT NULL,
    timestamp     DOUBLE PRECISION NOT NULL,
    price         DOUBLE PRECISION NOT NULL,
    volume        DOUBLE PRECISION NOT NULL,
    trade_type_id INT              NOT NULL REFERENCES kraken.trade_types (id),
    order_type_id INT              NOT NULL REFERENCES kraken.order_types (id),
    PRIMARY KEY (asset_pair, trade_id)
);

CREATE INDEX ix_time_and_sales_asset_pair_timestamp
    ON kraken.time_and_sales (asset_pair, timestamp);

INSERT INTO kraken.order_types (id, name)
VALUES (1, 'market'),
       (2, 'limit');

INSERT INTO kraken.trade_types (id, name)
VALUES (1, 'buy'),
       (2, 'sell');
