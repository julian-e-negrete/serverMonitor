-- idempotent init for market tables (orders, ticks)

-- public.orders
CREATE TABLE IF NOT EXISTS public.orders (
    "time" timestamptz NOT NULL,
    price numeric(18,6) NOT NULL,
    volume int8 NOT NULL,
    side bpchar(1) NOT NULL,
    instrument varchar(50) NULL,
    CONSTRAINT unique_trade UNIQUE ("time", price, volume, side)
);

CREATE INDEX IF NOT EXISTS orders_time_idx ON public.orders USING btree ("time" DESC);

-- Create trigger only if timescaledb helper exists and trigger not already present
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_proc WHERE proname = '_timescaledb_internal.insert_blocker') THEN
        IF NOT EXISTS (
            SELECT 1 FROM pg_trigger t
            JOIN pg_class c ON t.tgrelid = c.oid
            WHERE t.tgname = 'ts_insert_blocker' AND c.relname = 'orders'
        ) THEN
            EXECUTE 'CREATE TRIGGER ts_insert_blocker BEFORE INSERT ON public.orders FOR EACH ROW EXECUTE FUNCTION _timescaledb_internal.insert_blocker();';
        END IF;
    END IF;
END$$;


-- public.ticks
CREATE TABLE IF NOT EXISTS public.ticks (
    "time" timestamptz DEFAULT now() NOT NULL,
    instrument text NOT NULL,
    bid_volume int8 NOT NULL,
    bid_price numeric(18,6) NOT NULL,
    ask_price numeric(18,6) NOT NULL,
    ask_volume int8 NOT NULL,
    last_price numeric(18,6) NOT NULL,
    total_volume int8 NOT NULL,
    low numeric(18,6) NOT NULL,
    high numeric(18,6) NOT NULL,
    prev_close numeric(18,6) NOT NULL
);

CREATE INDEX IF NOT EXISTS ticks_time_idx ON public.ticks USING btree ("time" DESC);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_proc WHERE proname = '_timescaledb_internal.insert_blocker') THEN
        IF NOT EXISTS (
            SELECT 1 FROM pg_trigger t
            JOIN pg_class c ON t.tgrelid = c.oid
            WHERE t.tgname = 'ts_insert_blocker' AND c.relname = 'ticks'
        ) THEN
            EXECUTE 'CREATE TRIGGER ts_insert_blocker BEFORE INSERT ON public.ticks FOR EACH ROW EXECUTE FUNCTION _timescaledb_internal.insert_blocker();';
        END IF;
    END IF;
END$$;
