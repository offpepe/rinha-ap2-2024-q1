CREATE TABLE clientes
(
    id     SERIAL PRIMARY KEY,
    nome   VARCHAR(50) NOT NULL,
    saldo  INTEGER     NOT NULL,
    limite INTEGER     NOT NULL
);

CREATE UNLOGGED TABLE transacoes
(
    id           SERIAL PRIMARY KEY,
    cliente_id   INTEGER     NOT NULL,
    valor        INTEGER     NOT NULL,
    tipo         CHAR(1)     NOT NULL,
    descricao    VARCHAR(10) NOT NULL,
    realizada_em TIMESTAMP   NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_clientes_transacoes_id
        FOREIGN KEY (cliente_id) REFERENCES clientes (id)
);

DO
$$
    BEGIN
        INSERT INTO clientes (nome, limite, saldo)
        VALUES ('o barato sai caro', 1000 * 100, 0),
               ('zan corp ltda', 800 * 100, 0),
               ('les cruders', 10000 * 100, 0),
               ('padaria joia de cocaia', 100000 * 100, 0),
               ('kid mais', 5000 * 100, 0);
    END;
$$;

CREATE OR REPLACE FUNCTION UPDATE_BALANCE(cid integer, value integer, type char, description text) RETURNS INT[2]
AS
$$
DECLARE
    balance    int;
    newBalance int;
    climit     int;
BEGIN
    SELECT c.saldo, c.limite INTO balance, climit FROM clientes c WHERE c.id = cid;
    IF NOT FOUND THEN
        RETURN ARRAY [0,0];
    END IF;
    IF type = 'c' THEN
        newBalance = balance + value;
    ELSE
        newBalance = balance - value;
        IF -newBalance > climit THEN
            RETURN ARRAY [0, -1];
        END IF;
    END IF;
    UPDATE clientes SET saldo = newBalance WHERE id = cid;
    INSERT INTO transacoes (cliente_id, valor, tipo, descricao) VALUES (cid, value, type, description);
    RETURN ARRAY [newBalance, climit];
END;
$$
LANGUAGE plpgsql;