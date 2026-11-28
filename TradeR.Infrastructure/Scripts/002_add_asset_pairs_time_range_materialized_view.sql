CREATE MATERIALIZED VIEW IF NOT EXISTS kraken.asset_pairs_time_range
AS
SELECT asset_pair,
	   interval_minutes,
	   MIN("timestamp") AS oldest_time,
	   MAX("timestamp") AS newest_time
FROM kraken.ohlcvt
GROUP BY asset_pair, interval_minutes;

CREATE INDEX ON kraken.asset_pairs_time_range(asset_pair) INCLUDE (interval_minutes, oldest_time, newest_time);