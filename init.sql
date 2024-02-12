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

CREATE OR REPLACE FUNCTION CREATE_TRANSACTION(cid integer, value integer, type char,description varchar(10)) RETURNS INT[]
AS
$$
DECLARE
    client clientes%rowtype;
    balance int;
    client_limit int;
BEGIN
    SELECT
        *
    FROM clientes c WHERE c.id = cid INTO client;
    IF NOT found THEN
        return ARRAY[1, 0, 0, 0];
    END IF;
    IF type = 'c' THEN
        balance := client.saldo::integer + value::integer;
    ELSE
        balance := (client.saldo - value)::INT;
        IF -balance > client.limite THEN
            return ARRAY[0, 1, 0, 0];
        END IF;
    END IF;
    client_limit = client.limite;
    INSERT INTO transacoes (cliente_id, valor, tipo, descricao) VALUES (cid, value, type, description);
    UPDATE clientes SET saldo = balance WHERE id = cid;
    return ARRAY[0,0, balance, client_limit];
END
$$
LANGUAGE plpgsql;