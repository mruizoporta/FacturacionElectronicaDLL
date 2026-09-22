/*
================================================================================
  Parámetros LUGANIS en dbo.Parametros (BD acero / DASHA)
================================================================================
  Columnas ya existentes en Parametros:
    integracion_luganis, par_luganis_*

  Ejecutar sobre la base deseada (sin USE fijo).
  Ajustar @emp_codigo si no es 1.
================================================================================
*/

SET NOCOUNT ON;

DECLARE @emp_codigo INT = 1;

IF OBJECT_ID(N'dbo.Parametros', N'U') IS NULL
BEGIN
    RAISERROR(N'No existe dbo.Parametros en la base actual.', 16, 1);
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Parametros WHERE emp_codigo = @emp_codigo)
BEGIN
    RAISERROR(N'No hay fila en Parametros para emp_codigo=%d.', 16, 1, @emp_codigo);
    RETURN;
END;

/* Valores de prueba (stage Luganis) */
UPDATE dbo.Parametros
SET
    Usa_FacturacionElectronica = 1,
    integracion_luganis        = 1,
    par_luganis_baseurl        = 'https://rd.stage-api.tech-luganis.net',
    par_luganis_companycode    = 'COMPANY',
    par_luganis_username       = 'usuario_prueba',
    par_luganis_password       = 'clave_prueba',
    par_luganis_appversion     = '1.0.0',
    par_luganis_os             = 'Windows',
    par_luganis_deviceid       = 'PC-PRUEBA-001',
    par_luganis_latitude       = '18.4861',
    par_luganis_longitude      = '-69.9312',
    par_luganis_providerip     = '127.0.0.1'
WHERE emp_codigo = @emp_codigo;

PRINT N'Parametros LUGANIS (prueba) actualizados para emp_codigo='
    + CAST(@emp_codigo AS VARCHAR(10));

SELECT
    emp_codigo,
    Usa_FacturacionElectronica,
    integracion_luganis,
    par_luganis_baseurl,
    par_luganis_companycode,
    par_luganis_username,
    par_luganis_password,
    par_luganis_appversion,
    par_luganis_os,
    par_luganis_deviceid,
    par_luganis_latitude,
    par_luganis_longitude,
    par_luganis_providerip
FROM dbo.Parametros
WHERE emp_codigo = @emp_codigo;
GO
