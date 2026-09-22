USE [acueductos];
GO

/* 
  Ajuste temporal para LUGANIS:
  - Si la factura es Crédito (TipoPago = '2')
  - y NO existe ningún registro en FacFormaPago para esa factura
  - entonces el view debe devolver al menos una fila para construir FPAG en el TXT:
      FPAG|4|ValorPagar
*/

ALTER VIEW [dbo].[vwFormasPagoXML]
AS
SELECT 
    FP.emp_codigo,
    FP.suc_codigo,
    FP.fac_forma,
    FP.tfa_codigo,
    FP.fac_numero,
    CASE P.fpa_nombre 
        WHEN 'Efectivo' THEN 1
        WHEN 'Cheque' THEN 2
        WHEN 'Depósito Banco' THEN 2
        WHEN 'Tarjeta' THEN 3
        ELSE 4 
    END AS FormaPago,
    FP.for_monto AS MontoPago
FROM 
    [dbo].[FacFormaPago] FP
INNER JOIN 
    [dbo].[FormasPago] P 
    ON P.fpa_codigo = FP.fpa_codigo AND P.emp_codigo = FP.emp_codigo

UNION ALL

SELECT
    F.emp_codigo,
    F.suc_codigo,
    F.fac_forma,
    F.tfa_codigo,
    F.fac_numero,
    4 AS FormaPago,                                -- default temporal cuando no hay FacFormaPago (LUGANIS)
    CAST(ISNULL(F.ValorPagar, 0) AS DECIMAL(18,2)) AS MontoPago
FROM
    dbo.vwFacturasXML F
WHERE
    F.TipoPago = '2'                               -- Crédito
    AND NOT EXISTS (
        SELECT 1
        FROM dbo.FacFormaPago FP
        WHERE FP.emp_codigo = F.emp_codigo
          AND FP.suc_codigo = F.suc_codigo
          AND FP.fac_forma = F.fac_forma
          AND FP.tfa_codigo = F.tfa_codigo
          AND FP.fac_numero = F.fac_numero
    );
GO

