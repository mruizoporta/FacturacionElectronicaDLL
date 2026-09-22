/*
  Alerta de vencimiento del certificado FE (.p12).
  Correo destino parametrizable + umbral de días.
  Ejecutar en cada BD de cliente (sin USE fijo).
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Parametros', N'U') IS NULL
BEGIN
    RAISERROR(N'No existe dbo.Parametros.', 16, 1);
    RETURN;
END;

IF COL_LENGTH('dbo.Parametros', 'par_fe_correo_alerta') IS NULL
    ALTER TABLE dbo.Parametros ADD par_fe_correo_alerta VARCHAR(120) NULL;

IF COL_LENGTH('dbo.Parametros', 'par_fe_dias_alerta_cert') IS NULL
    ALTER TABLE dbo.Parametros ADD par_fe_dias_alerta_cert INT NULL;

IF COL_LENGTH('dbo.Parametros', 'par_fe_ultima_alerta_cert') IS NULL
    ALTER TABLE dbo.Parametros ADD par_fe_ultima_alerta_cert DATETIME NULL;
GO

UPDATE dbo.Parametros
SET par_fe_dias_alerta_cert = 30
WHERE par_fe_dias_alerta_cert IS NULL;
GO

PRINT N'Columnas de alerta certificado FE listas. Complete par_fe_correo_alerta en Parametros.';
GO

-- Ejemplo (descomentar y ajustar):
-- UPDATE dbo.Parametros
-- SET par_fe_correo_alerta = 'soporte@empresa.com',
--     par_fe_dias_alerta_cert = 30
-- WHERE emp_codigo = 1;
