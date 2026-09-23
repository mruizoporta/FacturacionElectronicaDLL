/*
  AsignarSecuenciaFacturasProvDGII — compras (ProvFacturas), SOLO e-CF 41 y 47.

  Ejecutar en la base del cliente. Reemplaza o crea el SP usado por la DLL.

  Requisitos:
  - ProvFacturas.tip_codigo → TipoNCF.cod_dgii debe ser 41 o 47
  - Si tip_codigo apunta a otro (p.ej. 31 consumo), se fuerza 41 (o 47 si @tipoECF=47)
  - SecuenciaDGII con fila Tipo = 41 y/o Tipo = 47 por emp_codigo
  - Para E47 desde Delphi: EnviarPagosExterior o @tipoECF=47

  Parámetros opcionales:
  - @sucursal: filtra por suc_codigo si la tabla lo tiene
  - @tipoECF: fuerza el tipo DGII (47 pagos al exterior; 41 compras)
*/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'dbo.AsignarSecuenciaFacturasProvDGII', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.AsignarSecuenciaFacturasProvDGII AS RETURN 0;');
GO

ALTER PROCEDURE [dbo].[AsignarSecuenciaFacturasProvDGII]
    @empresa   INT,
    @proveedor INT,
    @numero    VARCHAR(15),
    @eNCF      VARCHAR(50) OUTPUT,
    @sucursal  INT = NULL,
    @tipoECF   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @eNCF = NULL;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @TipoeNCF INT;
        DECLARE @eNCFActual VARCHAR(50);
        DECLARE @TieneError BIT;
        DECLARE @Aceptada BIT;
        DECLARE @EnviadaDGII BIT;
        DECLARE @SecuenciaUsada BIT;
        DECLARE @sucFactura INT;
        DECLARE @FacturaExiste BIT = 0;

        SELECT
            @FacturaExiste   = 1,
            @TipoeNCF       = t.cod_dgii,
            @TieneError     = pf.Error_DGII,
            @Aceptada       = pf.AceptadoDGII,
            @EnviadaDGII    = pf.Enviado_DGII,
            @SecuenciaUsada = ISNULL(pf.secuenciaUtilizadaDGI, 0),
            @sucFactura     = pf.suc_codigo
        FROM dbo.ProvFacturas pf
        LEFT JOIN dbo.TipoNCF t
            ON t.tip_codigo = pf.tip_codigo
           AND t.emp_codigo = pf.emp_codigo
        WHERE pf.emp_codigo = @empresa
          AND pf.sup_codigo = @proveedor
          AND LTRIM(RTRIM(pf.fac_numero)) = LTRIM(RTRIM(@numero));

        IF @FacturaExiste = 0
        BEGIN
            COMMIT TRAN;
            RETURN;
        END;

        IF @tipoECF IS NOT NULL AND @tipoECF > 0
            SET @TipoeNCF = @tipoECF;

        -- Compras/servicios: solo E41 o E47. Evita E31 (u otros) por tip_codigo mal mapeado en TipoNCF.
        IF @TipoeNCF IS NULL OR @TipoeNCF NOT IN (41, 47)
        BEGIN
            IF @tipoECF = 47
                SET @TipoeNCF = 47;
            ELSE
                SET @TipoeNCF = 41;
        END;

        IF @sucursal IS NOT NULL AND @sucFactura IS NOT NULL AND @sucursal <> @sucFactura
        BEGIN
            COMMIT TRAN;
            RETURN;
        END;

        SELECT @eNCFActual = NULLIF(LTRIM(RTRIM(pf.eNCF)), '')
        FROM dbo.ProvFacturas AS pf WITH (UPDLOCK, HOLDLOCK)
        WHERE pf.emp_codigo = @empresa
          AND pf.sup_codigo = @proveedor
          AND LTRIM(RTRIM(pf.fac_numero)) = LTRIM(RTRIM(@numero));

        IF @eNCFActual IS NOT NULL
        BEGIN
            /* Ya hay eNCF: reutilizar SIEMPRE (evita duplicados). */
            SET @eNCF = @eNCFActual;
            COMMIT TRAN;
            RETURN;
        END;

        DECLARE @nuevoENCF VARCHAR(50);
        SET @nuevoENCF = dbo.fn_obtenerSecuenciaDGI(@empresa, @TipoeNCF);

        IF @nuevoENCF IS NULL OR LTRIM(RTRIM(@nuevoENCF)) = ''
        BEGIN
            COMMIT TRAN;
            RETURN;
        END;

        UPDATE dbo.ProvFacturas
        SET
            eNCF = @nuevoENCF,
            secuenciaUtilizadaDGI = 0
        WHERE emp_codigo = @empresa
          AND sup_codigo = @proveedor
          AND LTRIM(RTRIM(fac_numero)) = LTRIM(RTRIM(@numero));

        IF @@ROWCOUNT = 1
        BEGIN
            UPDATE dbo.SecuenciaDGII
            SET Ultima_secuencia_DGII = Ultima_secuencia_DGII + 1
            WHERE emp_codigo = @empresa
              AND Tipo = @TipoeNCF;

            SET @eNCF = @nuevoENCF;
        END
        ELSE
        BEGIN
            SELECT @eNCF = NULLIF(LTRIM(RTRIM(eNCF)), '')
            FROM dbo.ProvFacturas
            WHERE emp_codigo = @empresa
              AND sup_codigo = @proveedor
              AND LTRIM(RTRIM(fac_numero)) = LTRIM(RTRIM(@numero));
        END

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        DECLARE @ErrMsg NVARCHAR(4000), @ErrSev INT, @ErrState INT;
        SELECT @ErrMsg = ERROR_MESSAGE(), @ErrSev = ERROR_SEVERITY(), @ErrState = ERROR_STATE();
        RAISERROR(@ErrMsg, @ErrSev, @ErrState);
    END CATCH
END
GO
