CREATE TABLE clientes
(
    id     SERIAL PRIMARY KEY,
    nome   VARCHAR(50) NOT NULL,
    limite INTEGER     NOT NULL
);

CREATE TABLE transacoes
(
    id           SERIAL PRIMARY KEY,
    cliente_id   INTEGER     NOT NULL,
    valor        INTEGER     NOT NULL,
    tipo         CHAR(1)     NOT NULL,
    descricao    VARCHAR(10) NOT NULL,
    realizada_em TIMESTAMP   NOT NULL   DEFAULT NOW(),
    CONSTRAINT fk_clientes_transacoes_id
        FOREIGN KEY (cliente_id) REFERENCES clientes (id)
);

CREATE TABLE saldos
(
    id         SERIAL PRIMARY KEY,
    cliente_id INTEGER NOT NULL,
    valor      INTEGER NOT NULL,
    CONSTRAINT fk_clientes_saldos_id
        FOREIGN KEY (cliente_id) REFERENCES clientes (id)
);

DO
$$
    BEGIN
        INSERT INTO clientes (nome, limite)
        VALUES ('o barato sai caro', 1000 * 100),
               ('zan corp ltda', 800 * 100),
               ('les cruders', 10000 * 100),
               ('padaria joia de cocaia', 100000 * 100),
               ('kid mais', 5000 * 100);

        INSERT INTO saldos (cliente_id, valor)
        SELECT id, 0
        FROM clientes;
    END;
$$;

CREATE OR REPLACE PROCEDURE CREATE_CREDIT_TRANSACTION(cid integer, value integer, description varchar(10))
AS
$$
DECLARE
    balance numeric;
BEGIN
    SELECT (valor + value) INTO balance FROM saldos s WHERE cliente_id = cid ORDER BY s.id DESC LIMIT 1;
    INSERT INTO transacoes (cliente_id, valor, tipo, descricao) VALUES (cid, value, 'c', description);
    INSERT INTO saldos (cliente_id, valor) VALUES (cid, balance);
END;
$$
    LANGUAGE plpgsql;

CREATE OR REPLACE PROCEDURE CREATE_DEBIT_TRANSACTION(cid integer, value integer, description varchar(10))
AS
$$
DECLARE
    data record;
BEGIN
    SELECT (valor - value) balance, c.limite
    FROM saldos s
             INNER JOIN clientes c on c.id = s.cliente_id
    WHERE cliente_id = cid
    ORDER BY s.id DESC
    LIMIT 1
    INTO data;
    IF data.balance * -1 > data.limite THEN
        RAISE EXCEPTION '422';
    END IF;
    INSERT INTO transacoes (cliente_id, valor, tipo, descricao) VALUES (cid, value, 'd', description);
    INSERT INTO saldos (cliente_id, valor) VALUES (cid, data.balance);
END;
$$
    LANGUAGE plpgsql;