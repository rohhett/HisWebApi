/* =====================================================================
   User-wise favourite status cards  —  2026-11-26
   Target DB : GWT_HIS_API (NEW HIS)
   Re-runnable: yes (IF OBJECT_ID / CREATE OR ALTER)

   Owner: save each user's favourite cards (Sample Management status cards) in the database,
   so the same favourites show in every browser. Until now they were kept in browser
   localStorage only.

   Objects
     1. TABLE      dbo.UserWiseFavoriteCardMapping   (new; one row per favourite card)
                   key = HospId + UserId + PageKey ('sample-management'); SequenceNo keeps the order
     2. PROCEDURE  dbo.S_GetUserWiseFavoriteCards    (@hospId, @userId, @pageKey -> CardKey in order)
     3. PROCEDURE  dbo.IU_UserWiseFavoriteCards      (replaces the user's list for the page)
                   @cardKeys = JSON array of card keys, e.g. ["holdClicked","approvedClicked"]
                   @Result: >0 number of cards saved, 0 list cleared,
                            -2 more than 5 cards, -3 invalid JSON / card key

   Callers: Home/getUserFavoriteCards, Home/saveUserFavoriteCards (HomeRepository).
   After running: restart the API with the new build. Nothing to assign; no cache.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.UserWiseFavoriteCardMapping', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserWiseFavoriteCardMapping
    (
        UserWiseFavoriteCardMappingId INT IDENTITY(1,1) NOT NULL,
        HospId     INT            NOT NULL,
        UserId     INT            NOT NULL,
        PageKey    NVARCHAR(50)   NOT NULL,
        CardKey    NVARCHAR(100)  NOT NULL,
        SequenceNo INT            NOT NULL,
        CreatedBy  INT            NULL,
        CreatedOn  DATETIME       NULL CONSTRAINT DF_UserWiseFavoriteCardMapping_CreatedOn DEFAULT GETDATE(),
        IpAddress  NVARCHAR(20)   NULL,
        CONSTRAINT PK_UserWiseFavoriteCardMapping PRIMARY KEY CLUSTERED (UserWiseFavoriteCardMappingId)
    );

    CREATE UNIQUE INDEX UX_UserWiseFavoriteCardMapping_User_Page_Card
        ON dbo.UserWiseFavoriteCardMapping (HospId, UserId, PageKey, CardKey);
END
GO

CREATE OR ALTER PROCEDURE dbo.S_GetUserWiseFavoriteCards
    @hospId  INT,
    @userId  INT,
    @pageKey NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CardKey, SequenceNo
    FROM dbo.UserWiseFavoriteCardMapping
    WHERE HospId = @hospId
      AND UserId = @userId
      AND PageKey = LTRIM(RTRIM(@pageKey))
    ORDER BY SequenceNo;
END
GO

CREATE OR ALTER PROCEDURE dbo.IU_UserWiseFavoriteCards
    @hospId    INT,
    @userId    INT,
    @pageKey   NVARCHAR(50),
    @cardKeys  NVARCHAR(MAX),          -- JSON array, in display order
    @IpAddress NVARCHAR(20) = NULL,
    @Result    INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @pageKey = LTRIM(RTRIM(@pageKey));
    SET @cardKeys = NULLIF(LTRIM(RTRIM(@cardKeys)), '');

    IF @cardKeys IS NOT NULL AND ISJSON(@cardKeys) = 0
    BEGIN
        SET @Result = -3;
        RETURN;
    END

    DECLARE @cards TABLE (CardKey NVARCHAR(100) NOT NULL, SequenceNo INT NOT NULL);

    -- keep the first position of each key, drop blanks
    INSERT INTO @cards (CardKey, SequenceNo)
    SELECT k.CardKey, ROW_NUMBER() OVER (ORDER BY MIN(k.Ord))
    FROM (
        SELECT LTRIM(RTRIM(CAST(j.[value] AS NVARCHAR(200)))) AS CardKey,
               CAST(j.[key] AS INT) AS Ord
        FROM OPENJSON(ISNULL(@cardKeys, '[]')) j
    ) k
    WHERE k.CardKey <> ''
    GROUP BY k.CardKey;

    IF EXISTS (SELECT 1 FROM @cards WHERE LEN(CardKey) > 100)
    BEGIN
        SET @Result = -3;
        RETURN;
    END

    IF (SELECT COUNT(*) FROM @cards) > 5   -- same limit as the screen (max 5 favourites)
    BEGIN
        SET @Result = -2;
        RETURN;
    END

    DELETE FROM dbo.UserWiseFavoriteCardMapping
    WHERE HospId = @hospId AND UserId = @userId AND PageKey = @pageKey;

    INSERT INTO dbo.UserWiseFavoriteCardMapping
        (HospId, UserId, PageKey, CardKey, SequenceNo, CreatedBy, IpAddress)
    SELECT @hospId, @userId, @pageKey, CardKey, SequenceNo, @userId, @IpAddress
    FROM @cards;

    SET @Result = (SELECT COUNT(*) FROM @cards);
END
GO
