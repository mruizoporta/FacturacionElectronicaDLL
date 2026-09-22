/*
  Agrega a dbo.Factura_RestBar las columnas de seguimiento DGII
  (sin Luganis ni columna dgi en RestBar).

  Ejecutar en [coral] ANTES de desplegar la vista vwFacturasXMLRESBAR actualizada.

  Idempotente: solo agrega si la columna [Enviado_DGII] aún no existe.
  (Sin columna [dgi] en RestBar: el flag DGI del encabezado XML sigue viniendo de Facturas_infoDGI.)
*/
USE [coral];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.Factura_RestBar', N'Enviado_DGII') IS NULL
BEGIN
    ALTER TABLE dbo.Factura_RestBar ADD
        [Enviado_DGII]           BIT            NOT NULL CONSTRAINT [DF_Factura_RestBar_Enviado_DGII] DEFAULT ((0)),
        [eNCF]                   VARCHAR(100)   NULL,
        [codigoseguridad]        VARCHAR(30)    NULL,
        [fechafirma]             DATETIME       NULL,
        [Error_DGII]             BIT            NOT NULL CONSTRAINT [DF_Factura_RestBar_Error_DGII] DEFAULT ((0)),
        [FechaLimitePago]        DATETIME       NULL,
        [AceptadoDGII]           BIT            NULL,
        [DGII_MontoEnviado]      DECIMAL(18, 2) NULL,
        [DGII_XmlEnviado]        NVARCHAR(MAX)  NULL,
        [DGII_FechaEnvio]        DATETIME       NULL,
        [secuenciaUtilizadaDGI]  BIT            NULL;
END
GO
