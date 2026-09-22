/*
  OBSOLETO: usar docs/Facturas_eNCF_Bitacora.sql (tabla sin trigger; la DLL registra los cambios).
*/
USE [REST];
GO

IF OBJECT_ID(N'dbo.TR_Facturas_eNCF_Bitacora', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Facturas_eNCF_Bitacora;
GO
