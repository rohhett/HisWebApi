/* =====================================================================
   2026-11-17  SMS / Email OTP: wrong-OTP lockout + resend cooldown
   ---------------------------------------------------------------------
   Rule: after @MaxAttempts (5) wrong OTPs - SMS and email together, counted per user
   within @LockMinutes (15) - the user is LOCKED for @LockMinutes:
   no OTP send, no OTP verify (forgot password + post-login verification) and no password reset.
   All pending OTPs of the user are burnt when the lock starts.

   * PasswordResetOtp.FailedAttempts   - wrong tries on that OTP (audit)
   * UserOtpLock (new table)            - per-user wrong-OTP counter + LockedUntil
   * S_VerifyOtpCore (new, internal)    - shared check for SMS and EMAIL OTPs:
                                          - looks only at the latest OTP OF THAT TYPE
                                            (before, SMS and email verify both took the latest
                                            OTP of any type, so sending both at once - as the
                                            post-login verification popup does - always made
                                            one of them "Invalid OTP")
                                          - result -8 while locked, -7 when this wrong OTP starts the lock
                                          - burnt OTPs get isPasswordReset = 1 so U_ResetPasswordByUserId
                                            can never accept them
   * S_VerifySmsOtp / S_VerifyEmailOtp  - same signature as before + optional
                                          @MaxAttempts, @LockMinutes, @AttemptsLeft OUT, @LockedSeconds OUT
   * S_GetOtpLockStatus (new)           - seconds left on a user's lock (0 = not locked)
   * S_GetOtpSendInfo (new)             - last send / sends in window / lock seconds (resend cooldown)
   * ResponseMessageMaster              - OTP_ATTEMPTS_EXCEEDED, OTP_RESEND_WAIT, OTP_SEND_LIMIT_REACHED,
                                          OTP_ALREADY_USED, OTP_LOCKED
   Callers of PasswordResetOtp: IU_StoreOtpForPasswordReset, IU_StoreEmailOtpForPasswordReset
   (unchanged), S_VerifySmsOtp, S_VerifyEmailOtp (replaced), U_ResetPasswordByUserId (unchanged;
   the API checks S_GetOtpLockStatus before calling it).
   Re-runnable. After running, clear Redis key _GetAllResponseMessagesCache
   (or wait 6h) so the new messages are picked up (the API has fallback texts meanwhile).
   ===================================================================== */

IF COL_LENGTH('dbo.PasswordResetOtp', 'FailedAttempts') IS NULL
    ALTER TABLE dbo.PasswordResetOtp
        ADD FailedAttempts INT NOT NULL
            CONSTRAINT DF_PasswordResetOtp_FailedAttempts DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_PasswordResetOtp_UserId_Type_CreatedAt'
                 AND object_id = OBJECT_ID('dbo.PasswordResetOtp'))
    CREATE NONCLUSTERED INDEX IX_PasswordResetOtp_UserId_Type_CreatedAt
        ON dbo.PasswordResetOtp (UserId, OtpType, CreatedAt DESC);
GO

IF OBJECT_ID('dbo.UserOtpLock', 'U') IS NULL
    CREATE TABLE dbo.UserOtpLock
    (
        UserId        BIGINT   NOT NULL CONSTRAINT PK_UserOtpLock PRIMARY KEY,
        FailedCount   INT      NOT NULL CONSTRAINT DF_UserOtpLock_FailedCount DEFAULT (0),
        FirstFailedOn DATETIME NULL,      -- start of the current counting window
        LockedUntil   DATETIME NULL,      -- OTP verify / send / password reset blocked until
        LastLockedOn  DATETIME NULL,
        LockCount     INT      NOT NULL CONSTRAINT DF_UserOtpLock_LockCount DEFAULT (0)  -- audit
    );
GO

/* drop the earlier draft name if it was ever created */
IF OBJECT_ID('dbo.S_GetSmsOtpSendInfo', 'P') IS NOT NULL
    DROP PROCEDURE dbo.S_GetSmsOtpSendInfo;
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetOtpLockStatus]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LockedSeconds = ISNULL((SELECT CASE WHEN LockedUntil > GETDATE()
                                               THEN DATEDIFF(SECOND, GETDATE(), LockedUntil) ELSE 0 END
                                   FROM UserOtpLock WHERE UserId = @UserId), 0);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[S_VerifyOtpCore]
    @UserId        BIGINT,
    @Otp           NVARCHAR(10),
    @OtpType       NVARCHAR(10),         -- 'SMS' / 'EMAIL'
    @MaxAttempts   INT,
    @LockMinutes   INT,
    @Result        INT OUTPUT,           -- 1 ok, -1 no user, -2 inactive, -3 none, -4 used, -5 expired,
                                         -- -6 wrong, -7 wrong + lock started, -8 locked
    @Message       NVARCHAR(255) OUTPUT,
    @AttemptsLeft  INT OUTPUT,
    @LockedSeconds INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OtpId BIGINT, @StoredOtp NVARCHAR(10), @ExpiryTime DATETIME,
            @IsActive BIT, @IsUsed BIT, @Now DATETIME = GETDATE(),
            @Count INT, @FirstFailed DATETIME, @LockedUntil DATETIME;

    SET @AttemptsLeft = NULL;
    SET @LockedSeconds = 0;
    IF @MaxAttempts IS NULL OR @MaxAttempts < 1 SET @MaxAttempts = 5;
    IF @LockMinutes IS NULL OR @LockMinutes < 1 SET @LockMinutes = 15;

    SELECT @IsActive = IsActive FROM UserMaster WHERE Id = @UserId;
    IF @@ROWCOUNT = 0
    BEGIN
        SET @Result = -1; SET @Message = 'User not found.'; RETURN;
    END
    IF @IsActive = 0
    BEGIN
        SET @Result = -2; SET @Message = 'User account is inactive.'; RETURN;
    END

    -- user lock (serialised per user)
    SELECT @Count = FailedCount, @FirstFailed = FirstFailedOn, @LockedUntil = LockedUntil
    FROM UserOtpLock WITH (UPDLOCK, HOLDLOCK)
    WHERE UserId = @UserId;

    IF @LockedUntil > @Now
    BEGIN
        SET @Result = -8; SET @AttemptsLeft = 0;
        SET @LockedSeconds = DATEDIFF(SECOND, @Now, @LockedUntil);
        SET @Message = 'Too many wrong OTPs. Try again later.';
        RETURN;
    END

    -- counting window expired (or never started): fresh count
    IF @FirstFailed IS NULL OR @FirstFailed < DATEADD(MINUTE, -@LockMinutes, @Now) OR @LockedUntil IS NOT NULL
        SET @Count = 0;

    SELECT TOP 1
        @OtpId      = Id,
        @StoredOtp  = Otp,
        @ExpiryTime = ExpiryTime,
        @IsUsed     = IsUsed
    FROM PasswordResetOtp WITH (UPDLOCK, ROWLOCK)
    WHERE UserId = @UserId AND OtpType = @OtpType
    ORDER BY CreatedAt DESC, Id DESC;

    IF @OtpId IS NULL
    BEGIN
        SET @Result = -3; SET @Message = 'No OTP found for this user.'; RETURN;
    END
    IF @IsUsed = 1
    BEGIN
        SET @Result = -4; SET @Message = 'OTP has already been used.'; RETURN;
    END
    IF @ExpiryTime < @Now
    BEGIN
        SET @Result = -5; SET @Message = 'OTP has expired. Please request a new one.'; RETURN;
    END

    IF @StoredOtp = @Otp
    BEGIN
        UPDATE PasswordResetOtp SET IsUsed = 1 WHERE Id = @OtpId;
        -- success clears the wrong-OTP counter
        UPDATE UserOtpLock SET FailedCount = 0, FirstFailedOn = NULL, LockedUntil = NULL WHERE UserId = @UserId;
        SET @Result = 1; SET @Message = 'OTP verified successfully.';
        RETURN;
    END

    /* ---------- wrong OTP ---------- */
    UPDATE PasswordResetOtp SET FailedAttempts = FailedAttempts + 1 WHERE Id = @OtpId;

    SET @Count = @Count + 1;
    SET @AttemptsLeft = CASE WHEN @MaxAttempts - @Count < 0 THEN 0 ELSE @MaxAttempts - @Count END;

    IF @AttemptsLeft > 0
    BEGIN
        MERGE UserOtpLock AS t
        USING (SELECT @UserId AS UserId) AS s ON t.UserId = s.UserId
        WHEN MATCHED THEN
            UPDATE SET FailedCount = @Count,
                       FirstFailedOn = CASE WHEN @Count = 1 THEN @Now ELSE t.FirstFailedOn END,
                       LockedUntil = NULL
        WHEN NOT MATCHED THEN
            INSERT (UserId, FailedCount, FirstFailedOn) VALUES (@UserId, 1, @Now);

        SET @Result = -6; SET @Message = 'Invalid OTP. Please try again.';
        RETURN;
    END

    -- limit reached: lock the user and burn every pending OTP (SMS + email)
    SET @LockedUntil = DATEADD(MINUTE, @LockMinutes, @Now);

    MERGE UserOtpLock AS t
    USING (SELECT @UserId AS UserId) AS s ON t.UserId = s.UserId
    WHEN MATCHED THEN
        UPDATE SET FailedCount = 0, FirstFailedOn = NULL, LockedUntil = @LockedUntil,
                   LastLockedOn = @Now, LockCount = t.LockCount + 1
    WHEN NOT MATCHED THEN
        INSERT (UserId, FailedCount, FirstFailedOn, LockedUntil, LastLockedOn, LockCount)
        VALUES (@UserId, 0, NULL, @LockedUntil, @Now, 1);

    UPDATE PasswordResetOtp
    SET IsUsed = 1, isPasswordReset = 1
    WHERE UserId = @UserId AND (IsUsed = 0 OR isPasswordReset = 0);

    SET @Result = -7; SET @AttemptsLeft = 0;
    SET @LockedSeconds = DATEDIFF(SECOND, @Now, @LockedUntil);
    SET @Message = 'Too many wrong OTPs. Try again later.';
END
GO

CREATE OR ALTER PROCEDURE [dbo].[S_VerifySmsOtp]
    @UserId        BIGINT,
    @Otp           NVARCHAR(6),
    @Result        INT OUTPUT,
    @Message       NVARCHAR(255) OUTPUT,
    @MaxAttempts   INT = 5,
    @AttemptsLeft  INT = NULL OUTPUT,
    @LockMinutes   INT = 15,
    @LockedSeconds INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
        EXEC dbo.S_VerifyOtpCore @UserId, @Otp, 'SMS', @MaxAttempts, @LockMinutes,
             @Result OUTPUT, @Message OUTPUT, @AttemptsLeft OUTPUT, @LockedSeconds OUTPUT;

        IF @Result = 1
            UPDATE UserMaster
            SET IsContactVerified = 1, LastModifiedOn = GETDATE(), LastModifiedBy = @UserId
            WHERE Id = @UserId;
    COMMIT TRAN;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[S_VerifyEmailOtp]
    @UserId        BIGINT,
    @Otp           NVARCHAR(6),
    @Result        INT OUTPUT,
    @Message       NVARCHAR(255) OUTPUT,
    @MaxAttempts   INT = 5,
    @AttemptsLeft  INT = NULL OUTPUT,
    @LockMinutes   INT = 15,
    @LockedSeconds INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
        EXEC dbo.S_VerifyOtpCore @UserId, @Otp, 'EMAIL', @MaxAttempts, @LockMinutes,
             @Result OUTPUT, @Message OUTPUT, @AttemptsLeft OUTPUT, @LockedSeconds OUTPUT;

        IF @Result = 1
        BEGIN
            UPDATE UserMaster
            SET IsEmailVerified = 1, LastModifiedOn = GETDATE(), LastModifiedBy = @UserId
            WHERE Id = @UserId;
            SET @Message = 'Email OTP verified successfully.';
        END
    COMMIT TRAN;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[S_GetOtpSendInfo]
    @UserId        BIGINT,
    @OtpType       NVARCHAR(10),         -- 'SMS' / 'EMAIL'
    @WindowMinutes INT = 60
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @From DATETIME = DATEADD(MINUTE, -@WindowMinutes, GETDATE());

    SELECT
        SecondsSinceLastSend = (SELECT TOP 1 DATEDIFF(SECOND, CreatedAt, GETDATE())
                                FROM PasswordResetOtp
                                WHERE UserId = @UserId AND OtpType = @OtpType
                                ORDER BY CreatedAt DESC, Id DESC),
        SendsInWindow        = (SELECT COUNT(1)
                                FROM PasswordResetOtp
                                WHERE UserId = @UserId AND OtpType = @OtpType AND CreatedAt >= @From),
        OldestSendSecondsAgo = (SELECT DATEDIFF(SECOND, MIN(CreatedAt), GETDATE())
                                FROM PasswordResetOtp
                                WHERE UserId = @UserId AND OtpType = @OtpType AND CreatedAt >= @From),
        LockedSeconds        = ISNULL((SELECT CASE WHEN LockedUntil > GETDATE()
                                                   THEN DATEDIFF(SECOND, GETDATE(), LockedUntil) ELSE 0 END
                                       FROM UserOtpLock WHERE UserId = @UserId), 0);
END
GO

/* ---------- response messages ---------- */
IF NOT EXISTS (SELECT 1 FROM ResponseMessageMaster WHERE AlertCode = 'OTP_ATTEMPTS_EXCEEDED')
    INSERT INTO ResponseMessageMaster (TypeId, Type, AlertCode, Message, IsActive)
    VALUES (3, 'Error', 'OTP_ATTEMPTS_EXCEEDED', 'Too many wrong OTPs. OTP verification and password reset are blocked for this user.', 1);

IF NOT EXISTS (SELECT 1 FROM ResponseMessageMaster WHERE AlertCode = 'OTP_LOCKED')
    INSERT INTO ResponseMessageMaster (TypeId, Type, AlertCode, Message, IsActive)
    VALUES (3, 'Error', 'OTP_LOCKED', 'OTP verification and password reset are temporarily blocked because of too many wrong OTPs.', 1);

IF NOT EXISTS (SELECT 1 FROM ResponseMessageMaster WHERE AlertCode = 'OTP_RESEND_WAIT')
    INSERT INTO ResponseMessageMaster (TypeId, Type, AlertCode, Message, IsActive)
    VALUES (2, 'Warn', 'OTP_RESEND_WAIT', 'Please wait before requesting a new OTP.', 1);

IF NOT EXISTS (SELECT 1 FROM ResponseMessageMaster WHERE AlertCode = 'OTP_SEND_LIMIT_REACHED')
    INSERT INTO ResponseMessageMaster (TypeId, Type, AlertCode, Message, IsActive)
    VALUES (3, 'Error', 'OTP_SEND_LIMIT_REACHED', 'OTP request limit reached. Please try again later.', 1);

IF NOT EXISTS (SELECT 1 FROM ResponseMessageMaster WHERE AlertCode = 'OTP_ALREADY_USED')
    INSERT INTO ResponseMessageMaster (TypeId, Type, AlertCode, Message, IsActive)
    VALUES (3, 'Error', 'OTP_ALREADY_USED', 'This OTP has already been used. Please request a new OTP.', 1);
GO
