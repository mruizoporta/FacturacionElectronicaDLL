/*
  Formas de pago e-CF para flujo RESBAR (equivalente a vwFormasPagoXML + fila crédito default).

  Origen: dbo.RestBar_Factura_Forma_Pago. Filtro DLL: ticket, usu_codigo, caja, emp_codigo, suc_codigo.
  GenerarXML lee: FormaPago, MontoPago.

  Compatible con SQL Server 2008 / 2008 R2 (sin TRY_CONVERT, TRY_CAST ni CREATE OR ALTER VIEW).
  En SQL Server 2016+ puede usar CREATE OR ALTER VIEW y omitir DROP si lo prefieren.

  Mapeo Forma: '1'..'4' o Efectivo/Cheque/Depósito Banco/Tarjeta; crédito sintético FormaPago=4 desde vwFacturasXMLRESBAR.
*/
USE [coral];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.vwFormasPagoXMLRESBAR', N'V') IS NOT NULL
    DROP VIEW dbo.vwFormasPagoXMLRESBAR;
GO

CREATE VIEW dbo.vwFormasPagoXMLRESBAR
AS
/* 1) Formas registradas en RestBar */
SELECT
    fp.EMP_CODIGO AS emp_codigo,
    fp.SUC_CODIGO AS suc_codigo,
    CAST(N'RB' AS VARCHAR(10)) AS fac_forma,
    CAST(1 AS INT) AS tfa_codigo,
    fp.FacturaID AS fac_numero,
    fp.FacturaID AS ticket,
    CAST(fp.CajeroID AS VARCHAR(50)) AS usu_codigo,
    CAST(fp.CajaID AS VARCHAR(50)) AS caja,

    CASE
        /* Forma guardada como texto '1'..'4' o entero que se castea a varchar */
        WHEN LTRIM(RTRIM(CAST(fp.Forma AS VARCHAR(20)))) IN (N'1', N'2', N'3', N'4')
        THEN CAST(LTRIM(RTRIM(CAST(fp.Forma AS VARCHAR(20)))) AS INT)
        WHEN LTRIM(RTRIM(CAST(fp.Forma AS NVARCHAR(80)))) = N'Efectivo' THEN 1
        WHEN LTRIM(RTRIM(CAST(fp.Forma AS NVARCHAR(80)))) = N'Cheque' THEN 2
        WHEN LTRIM(RTRIM(CAST(fp.Forma AS NVARCHAR(80)))) = N'Depósito Banco' THEN 2
        WHEN LTRIM(RTRIM(CAST(fp.Forma AS NVARCHAR(80)))) = N'Tarjeta' THEN 3
        ELSE 4
    END AS FormaPago,

    CAST(ISNULL(fp.Monto, 0) AS DECIMAL(18, 2)) AS MontoPago
FROM dbo.RestBar_Factura_Forma_Pago AS fp

UNION ALL

/* 2) Crédito sin líneas en RestBar_Factura_Forma_Pago */
SELECT
    F.emp_codigo,
    F.suc_codigo,
    CAST(N'RB' AS VARCHAR(10)) AS fac_forma,
    CAST(1 AS INT) AS tfa_codigo,
    CAST(LTRIM(RTRIM(F.fac_numero)) AS INT) AS fac_numero,
    CAST(LTRIM(RTRIM(F.fac_numero)) AS INT) AS ticket,
    F.usu_codigo,
    F.caja,
    4 AS FormaPago,
    CAST(ISNULL(F.ValorPagar, 0) AS DECIMAL(18, 2)) AS MontoPago
FROM dbo.vwFacturasXMLRESBAR AS F
WHERE F.TipoPago = 2
  AND LTRIM(RTRIM(F.fac_numero)) NOT LIKE '%[^0-9]%'
  AND LEN(LTRIM(RTRIM(F.fac_numero))) > 0
  AND NOT EXISTS (
        SELECT 1
        FROM dbo.RestBar_Factura_Forma_Pago AS FP
        WHERE FP.EMP_CODIGO = F.emp_codigo
          AND FP.SUC_CODIGO = F.suc_codigo
          AND CONVERT(VARCHAR(50), FP.FacturaID) = LTRIM(RTRIM(F.fac_numero))
          AND CONVERT(VARCHAR(50), FP.CajeroID) = F.usu_codigo
          AND CONVERT(VARCHAR(50), FP.CajaID) = F.caja
    );
GO
