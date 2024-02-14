CREATE TABLE clientes
(
    id     SERIAL,
    saldo  INTEGER     NOT NULL,
    limite INTEGER     NOT NULL
);
CREATE INDEX ON clientes USING HASH(id);

CREATE UNLOGGED TABLE transacoes
(
    id           SERIAL,
    cliente_id   INTEGER     NOT NULL,
    valor        INTEGER     NOT NULL,
    tipo         CHAR(1)     NOT NULL,
    descricao    VARCHAR(10) NOT NULL,
    realizada_em TIMESTAMP   NOT NULL DEFAULT NOW()
);
CREATE INDEX ON transacoes (id DESC);
DO
$$
    BEGIN
        INSERT INTO clientes (limite, saldo)
        VALUES (1000 * 100, 0),
               (800 * 100, 0),
               (10000 * 100, 0),
               (100000 * 100, 0),
               (5000 * 100, 0);
    END;
$$;

CREATE OR REPLACE PROCEDURE CREATE_TRANSACTION(cid integer, value integer, type char, description text, newBalance integer)
AS
$$
BEGIN
    UPDATE clientes SET saldo = newBalance WHERE id = cid;
    INSERT INTO transacoes (cliente_id, valor, tipo, descricao) VALUES (cid, value, type, description);
END;
$$
LANGUAGE plpgsql;