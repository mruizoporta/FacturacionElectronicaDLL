USE [friusa]
GO
/****** Object:  StoredProcedure [dbo].[AsignarSecuenciaFacturasDGII] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[AsignarSecuenciaFacturasDGII]
    @empresa   INT,
    @sucursal  INT,
    @tipo      INT,
    @forma     VARCHAR(10),
    @numero    INT,
    @eNCF      VARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @TipoeNCF INT;
        DECLARE @eNCFActual VARCHAR(50);
        DECLARE @TieneError BIT;
        DECLARE @Aceptada BIT;
        DECLARE @EnviadaDGII BIT;
        DECLARE @SecuenciaUsada BIT;

        SELECT 
            @TipoeNCF       = t.cod_dgii,
            @TieneError     = f.Error_DGII,
            @Aceptada       = f.AceptadoDGII,
            @EnviadaDGII    = f.Enviado_DGII,
            @SecuenciaUsada = ISNULL(f.secuenciaUtilizadaDGI, 0)
        FROM dbo.Facturas f
        INNER JOIN dbo.TipoNCF t
            ON t.tip_codigo = f.tip_codigo
           AND t.emp_codigo = f.emp_codigo
        WHERE f.emp_codigo = @empresa
          AND f.suc_codigo = @sucursal
          AND f.tfa_codigo = @tipo
          AND f.fac_forma  = @forma
          AND f.fac_numero = @numero;

        IF @TipoeNCF IS NULL
        BEGIN
            SET @eNCF = NULL;
            COMMIT TRAN;
            RETURN;
        END;

        SELECT @eNCFActual = NULLIF(LTRIM(RTRIM(f.eNCF)),'')
        FROM dbo.Facturas AS f WITH (UPDLOCK, HOLDLOCK)
        WHERE f.emp_codigo = @empresa
          AND f.suc_codigo = @sucursal
          AND f.tfa_codigo = @tipo
          AND f.fac_forma  = @forma
          AND f.fac_numero = @numero;

        IF @eNCFActual IS NOT NULL
        BEGIN
            IF (@SecuenciaUsada = 0)
            BEGIN
                SET @eNCF = @eNCFActual;
                COMMIT TRAN;
                RETURN;
            END;

            IF (@TieneError = 0 AND @Aceptada = 1)
            BEGIN
                SET @eNCF = @eNCFActual;
                COMMIT TRAN;
                RETURN;
            END;
        END;

        DECLARE @nuevoENCF VARCHAR(50);
        SET @nuevoENCF = dbo.fn_obtenerSecuenciaDGI(@empresa, @TipoeNCF);

        PRINT @nuevoENCF;

        UPDATE dbo.Facturas
        SET 
            eNCF = @nuevoENCF,
            secuenciaUtilizadaDGI = 0
        WHERE emp_codigo = @empresa
          AND suc_codigo = @sucursal
          AND tfa_codigo = @tipo
          AND fac_forma  = @forma
          AND fac_numero = @numero;

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
            SELECT @eNCF = eNCF
            FROM dbo.Facturas
            WHERE emp_codigo = @empresa
              AND suc_codigo = @sucursal
              AND tfa_codigo = @tipo
              AND fac_forma  = @forma
              AND fac_numero = @numero;
        END

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        DECLARE @ErrMsg NVARCHAR(4000), @ErrSev INT, @ErrState INT;
        SELECT @ErrMsg = ERROR_MESSAGE(), @ErrSev = ERROR_SEVERITY(), @ErrState = ERROR_STATE();
        RAISERROR(@ErrMsg, @ErrSev, @ErrState);
    END CATCH
END;
GO
