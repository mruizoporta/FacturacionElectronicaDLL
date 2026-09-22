/*
  dbo.spFacturasDetalleXMLRESBARGet

  Encapsula lectura del detalle RESBAR para la DLL
  reemplazando SELECT directo a vwFacturasDetalleXMLRESBAR.
*/
USE [l1];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[spFacturasDetalleXMLRESBARGet]
    @emp_codigo INT,
    @suc_codigo INT,
    @fac_numero INT,
    @usu_codigo VARCHAR(50),
    @caja       VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.vwFacturasDetalleXMLRESBAR
    WHERE fac_numero = @fac_numero
      AND usu_codigo = @usu_codigo
      AND caja = @caja
      AND emp_codigo = @emp_codigo
      AND suc_codigo = @suc_codigo;
END;
GO
