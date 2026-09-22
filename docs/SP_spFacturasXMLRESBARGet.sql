/*
  dbo.spFacturasXMLRESBARGet

  Encapsula lectura del encabezado RESBAR para la DLL
  reemplazando SELECT directo a vwFacturasXMLRESBAR.
*/
USE [l1];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spFacturasXMLRESBARGet]
    @emp_codigo INT,
    @suc_codigo INT,
    @fac_numero INT,
    @usu_codigo VARCHAR(50),
    @caja       VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.vwFacturasXMLRESBAR
    WHERE fac_numero = @fac_numero
      AND usu_codigo = @usu_codigo
      AND caja = @caja
      AND emp_codigo = @emp_codigo
      AND suc_codigo = @suc_codigo;
END;
GO
