/*
  Columna tipo en dbo.BitacoraFacturacionElectronicaDGII
  (tabla de origen del documento enviado a DGII).

  Valores: FACTURAS, PROV_FACTURAS, DESEMBOLSOS, DEVOLUCION, NOTA_CREDITO,
           NOTAS_DEBITO, MONTOS_TICKET, RESTBAR

  Ejecutar en la base de la aplicación (ej. [REST]).
*/
USE [REST];
GO

IF COL_LENGTH(N'dbo.BitacoraFacturacionElectronicaDGII', N'tipo') IS NULL
    ALTER TABLE dbo.BitacoraFacturacionElectronicaDGII ADD tipo VARCHAR(30) NULL;
GO

/* Registros antiguos sin tipo: asumir facturas (comportamiento histórico) */
UPDATE dbo.BitacoraFacturacionElectronicaDGII
SET tipo = N'FACTURAS'
WHERE tipo IS NULL
  AND (usu_codigo IS NULL OR LTRIM(RTRIM(CONVERT(VARCHAR(50), usu_codigo))) = '')
  AND (caja IS NULL OR LTRIM(RTRIM(CONVERT(VARCHAR(50), caja))) = '');
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_BitacoraFacturacionElectronicaDGII_Tipo'
      AND object_id = OBJECT_ID(N'dbo.BitacoraFacturacionElectronicaDGII')
)
    CREATE NONCLUSTERED INDEX IX_BitacoraFacturacionElectronicaDGII_Tipo
        ON dbo.BitacoraFacturacionElectronicaDGII (emp_codigo, fac_numero, tipo, Fecha_Envio DESC)
        INCLUDE (Estado, MensajeError, usu_codigo, caja);
GO

PRINT 'Columna tipo lista en dbo.BitacoraFacturacionElectronicaDGII';
GO
