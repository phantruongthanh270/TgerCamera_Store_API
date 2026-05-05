using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TgerCamera.Models;

#nullable disable

namespace TgerCamera.Migrations
{
    [DbContext(typeof(TgerCameraContext))]
    [Migration("20260430040120_RefactorOrdersAndCart")]
    public partial class RefactorOrdersAndCart : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Orders', 'ShippingFullName') IS NULL
                    ALTER TABLE [dbo].[Orders] ADD [ShippingFullName] NVARCHAR(150) NULL;

                IF COL_LENGTH('dbo.Orders', 'ShippingPhone') IS NULL
                    ALTER TABLE [dbo].[Orders] ADD [ShippingPhone] NVARCHAR(20) NULL;

                IF COL_LENGTH('dbo.Orders', 'ShippingAddressLine') IS NULL
                    ALTER TABLE [dbo].[Orders] ADD [ShippingAddressLine] NVARCHAR(255) NULL;

                IF COL_LENGTH('dbo.Orders', 'ShippingDistrict') IS NULL
                    ALTER TABLE [dbo].[Orders] ADD [ShippingDistrict] NVARCHAR(100) NULL;

                IF COL_LENGTH('dbo.Orders', 'ShippingCity') IS NULL
                    ALTER TABLE [dbo].[Orders] ADD [ShippingCity] NVARCHAR(100) NULL;

                IF COL_LENGTH('dbo.OrderItems', 'ProductName') IS NULL
                    ALTER TABLE [dbo].[OrderItems] ADD [ProductName] NVARCHAR(200) NULL;

                IF COL_LENGTH('dbo.ShippingAddresses', 'UpdatedAt') IS NULL
                    ALTER TABLE [dbo].[ShippingAddresses] ADD [UpdatedAt] DATETIME2 NULL;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Orders', 'ShippingAddressId') IS NOT NULL
                BEGIN
                    UPDATE o
                    SET
                        [ShippingFullName] = COALESCE(NULLIF(o.[ShippingFullName], N''), sa.[FullName], N''),
                        [ShippingPhone] = COALESCE(NULLIF(o.[ShippingPhone], N''), sa.[Phone], N''),
                        [ShippingAddressLine] = COALESCE(NULLIF(o.[ShippingAddressLine], N''), sa.[AddressLine], N''),
                        [ShippingDistrict] = COALESCE(NULLIF(o.[ShippingDistrict], N''), sa.[District], N''),
                        [ShippingCity] = COALESCE(NULLIF(o.[ShippingCity], N''), sa.[City], N'')
                    FROM [dbo].[Orders] o
                    LEFT JOIN [dbo].[ShippingAddresses] sa ON sa.[Id] = o.[ShippingAddressId];
                END
                ELSE
                BEGIN
                    UPDATE [dbo].[Orders]
                    SET
                        [ShippingFullName] = ISNULL([ShippingFullName], N''),
                        [ShippingPhone] = ISNULL([ShippingPhone], N''),
                        [ShippingAddressLine] = ISNULL([ShippingAddressLine], N''),
                        [ShippingDistrict] = ISNULL([ShippingDistrict], N''),
                        [ShippingCity] = ISNULL([ShippingCity], N'');
                END

                UPDATE oi
                SET [ProductName] = COALESCE(NULLIF(oi.[ProductName], N''), p.[Name], N'')
                FROM [dbo].[OrderItems] oi
                LEFT JOIN [dbo].[Products] p ON p.[Id] = oi.[ProductId];

                UPDATE [dbo].[Orders]
                SET [SessionId] = NULL
                WHERE [UserId] IS NOT NULL
                  AND NULLIF(LTRIM(RTRIM([SessionId])), N'') IS NOT NULL;

                UPDATE [dbo].[Orders]
                SET [SessionId] = CONCAT(N'legacy-order-', [Id])
                WHERE [UserId] IS NULL
                  AND NULLIF(LTRIM(RTRIM([SessionId])), N'') IS NULL;

                UPDATE [dbo].[Carts]
                SET [SessionId] = NULL
                WHERE [UserId] IS NOT NULL
                  AND NULLIF(LTRIM(RTRIM([SessionId])), N'') IS NOT NULL;

                UPDATE [dbo].[Carts]
                SET [SessionId] = CONCAT(N'legacy-cart-', [Id])
                WHERE [UserId] IS NULL
                  AND NULLIF(LTRIM(RTRIM([SessionId])), N'') IS NULL;

                UPDATE [dbo].[ShippingAddresses]
                SET
                    [FullName] = ISNULL([FullName], N''),
                    [Phone] = ISNULL([Phone], N''),
                    [AddressLine] = ISNULL([AddressLine], N''),
                    [District] = ISNULL([District], N''),
                    [City] = ISNULL([City], N''),
                    [IsDefault] = ISNULL([IsDefault], 0),
                    [UpdatedAt] = ISNULL([UpdatedAt], [CreatedAt]);
                """);

            migrationBuilder.Sql(
                """
                ;WITH DuplicateCartItems AS
                (
                    SELECT
                        [CartId],
                        [ProductId],
                        MIN([Id]) AS [KeepId],
                        SUM([Quantity]) AS [TotalQuantity]
                    FROM [dbo].[CartItems]
                    GROUP BY [CartId], [ProductId]
                    HAVING COUNT(*) > 1
                )
                UPDATE ci
                SET [Quantity] = dci.[TotalQuantity]
                FROM [dbo].[CartItems] ci
                INNER JOIN DuplicateCartItems dci ON dci.[KeepId] = ci.[Id];

                ;WITH DuplicateCartItems AS
                (
                    SELECT
                        [CartId],
                        [ProductId],
                        MIN([Id]) AS [KeepId]
                    FROM [dbo].[CartItems]
                    GROUP BY [CartId], [ProductId]
                    HAVING COUNT(*) > 1
                )
                DELETE ci
                FROM [dbo].[CartItems] ci
                INNER JOIN DuplicateCartItems dci
                    ON dci.[CartId] = ci.[CartId]
                   AND dci.[ProductId] = ci.[ProductId]
                   AND dci.[KeepId] <> ci.[Id];

                DECLARE @CartMerge TABLE
                (
                    [DuplicateCartId] INT PRIMARY KEY,
                    [SurvivorCartId] INT NOT NULL
                );

                ;WITH RankedByUser AS
                (
                    SELECT
                        [Id],
                        MIN([Id]) OVER (PARTITION BY [UserId]) AS [SurvivorCartId],
                        ROW_NUMBER() OVER (
                            PARTITION BY [UserId]
                            ORDER BY ISNULL([CreatedAt], '19000101'), [Id]
                        ) AS [RowNum]
                    FROM [dbo].[Carts]
                    WHERE [UserId] IS NOT NULL
                )
                INSERT INTO @CartMerge ([DuplicateCartId], [SurvivorCartId])
                SELECT [Id], [SurvivorCartId]
                FROM RankedByUser
                WHERE [RowNum] > 1;

                ;WITH RankedBySession AS
                (
                    SELECT
                        [Id],
                        MIN([Id]) OVER (PARTITION BY [SessionId]) AS [SurvivorCartId],
                        ROW_NUMBER() OVER (
                            PARTITION BY [SessionId]
                            ORDER BY ISNULL([CreatedAt], '19000101'), [Id]
                        ) AS [RowNum]
                    FROM [dbo].[Carts]
                    WHERE [UserId] IS NULL
                      AND [SessionId] IS NOT NULL
                )
                INSERT INTO @CartMerge ([DuplicateCartId], [SurvivorCartId])
                SELECT rs.[Id], rs.[SurvivorCartId]
                FROM RankedBySession rs
                WHERE rs.[RowNum] > 1
                  AND NOT EXISTS (
                      SELECT 1
                      FROM @CartMerge cm
                      WHERE cm.[DuplicateCartId] = rs.[Id]
                  );

                MERGE [dbo].[CartItems] AS target
                USING
                (
                    SELECT
                        cm.[SurvivorCartId] AS [CartId],
                        ci.[ProductId],
                        SUM(ci.[Quantity]) AS [Quantity]
                    FROM @CartMerge cm
                    INNER JOIN [dbo].[CartItems] ci ON ci.[CartId] = cm.[DuplicateCartId]
                    GROUP BY cm.[SurvivorCartId], ci.[ProductId]
                ) AS source
                    ON target.[CartId] = source.[CartId]
                   AND target.[ProductId] = source.[ProductId]
                WHEN MATCHED THEN
                    UPDATE SET target.[Quantity] = target.[Quantity] + source.[Quantity]
                WHEN NOT MATCHED THEN
                    INSERT ([CartId], [ProductId], [Quantity])
                    VALUES (source.[CartId], source.[ProductId], source.[Quantity]);

                DELETE ci
                FROM [dbo].[CartItems] ci
                INNER JOIN @CartMerge cm ON cm.[DuplicateCartId] = ci.[CartId];

                DELETE c
                FROM [dbo].[Carts] c
                INNER JOIN @CartMerge cm ON cm.[DuplicateCartId] = c.[Id];

                ;WITH RankedDefaults AS
                (
                    SELECT
                        [Id],
                        ROW_NUMBER() OVER (
                            PARTITION BY [UserId]
                            ORDER BY ISNULL([CreatedAt], '19000101') DESC, [Id] DESC
                        ) AS [RowNum]
                    FROM [dbo].[ShippingAddresses]
                    WHERE [IsDefault] = 1
                )
                UPDATE sa
                SET [IsDefault] = 0
                FROM [dbo].[ShippingAddresses] sa
                INNER JOIN RankedDefaults rd ON rd.[Id] = sa.[Id]
                WHERE rd.[RowNum] > 1;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Orders', 'ShippingFullName') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] ALTER COLUMN [ShippingFullName] NVARCHAR(150) NOT NULL;

                IF COL_LENGTH('dbo.Orders', 'ShippingPhone') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] ALTER COLUMN [ShippingPhone] NVARCHAR(20) NOT NULL;

                IF COL_LENGTH('dbo.Orders', 'ShippingAddressLine') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] ALTER COLUMN [ShippingAddressLine] NVARCHAR(255) NOT NULL;

                IF COL_LENGTH('dbo.Orders', 'ShippingDistrict') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] ALTER COLUMN [ShippingDistrict] NVARCHAR(100) NOT NULL;

                IF COL_LENGTH('dbo.Orders', 'ShippingCity') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] ALTER COLUMN [ShippingCity] NVARCHAR(100) NOT NULL;

                IF COL_LENGTH('dbo.OrderItems', 'ProductName') IS NOT NULL
                    ALTER TABLE [dbo].[OrderItems] ALTER COLUMN [ProductName] NVARCHAR(200) NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Orders', 'ShippingAddressId') IS NOT NULL
                BEGIN
                    DECLARE @sql NVARCHAR(MAX) = N'';

                    SELECT @sql = @sql + N'ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [' + fk.[name] + N'];'
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.foreign_key_columns fkc ON fkc.[constraint_object_id] = fk.[object_id]
                    INNER JOIN sys.columns c
                        ON c.[object_id] = fkc.[parent_object_id]
                       AND c.[column_id] = fkc.[parent_column_id]
                    WHERE fk.[parent_object_id] = OBJECT_ID(N'[dbo].[Orders]')
                      AND c.[name] = N'ShippingAddressId';

                    IF @sql <> N''
                        EXEC sp_executesql @sql;

                    SET @sql = N'';

                    SELECT @sql = @sql + N'DROP INDEX [' + i.[name] + N'] ON [dbo].[Orders];'
                    FROM sys.indexes i
                    INNER JOIN sys.index_columns ic
                        ON ic.[object_id] = i.[object_id]
                       AND ic.[index_id] = i.[index_id]
                    INNER JOIN sys.columns c
                        ON c.[object_id] = ic.[object_id]
                       AND c.[column_id] = ic.[column_id]
                    WHERE i.[object_id] = OBJECT_ID(N'[dbo].[Orders]')
                      AND c.[name] = N'ShippingAddressId'
                      AND i.[is_primary_key] = 0
                      AND i.[is_unique_constraint] = 0;

                    IF @sql <> N''
                        EXEC sp_executesql @sql;

                    DECLARE @defaultConstraintName SYSNAME;

                    SELECT @defaultConstraintName = dc.[name]
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c
                        ON c.[default_object_id] = dc.[object_id]
                       AND c.[object_id] = dc.[parent_object_id]
                    WHERE dc.[parent_object_id] = OBJECT_ID(N'[dbo].[Orders]')
                      AND c.[name] = N'ShippingAddressId';

                    IF @defaultConstraintName IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [' + @defaultConstraintName + N']');

                    ALTER TABLE [dbo].[Orders] DROP COLUMN [ShippingAddressId];
                END
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM [dbo].[ShippingAddresses]
                WHERE [UserId] IS NULL;

                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [UserId] INT NOT NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [FullName] NVARCHAR(150) NOT NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [Phone] NVARCHAR(20) NOT NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [AddressLine] NVARCHAR(255) NOT NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [District] NVARCHAR(100) NOT NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [City] NVARCHAR(100) NOT NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [IsDefault] BIT NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS [UX_Carts_UserId] ON [dbo].[Carts];
                DROP INDEX IF EXISTS [UX_Carts_SessionId] ON [dbo].[Carts];
                DROP INDEX IF EXISTS [UX_CartItems_CartId_ProductId] ON [dbo].[CartItems];
                DROP INDEX IF EXISTS [UX_ShippingAddresses_UserId_Default] ON [dbo].[ShippingAddresses];
                DROP INDEX IF EXISTS [IX_Orders_UserId_CreatedAt] ON [dbo].[Orders];
                DROP INDEX IF EXISTS [IX_Orders_SessionId_CreatedAt] ON [dbo].[Orders];

                IF OBJECT_ID(N'[dbo].[CK_Carts_UserId_XOR_SessionId]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[Carts] DROP CONSTRAINT [CK_Carts_UserId_XOR_SessionId];

                IF OBJECT_ID(N'[dbo].[CK_CartItems_Quantity_Positive]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[CartItems] DROP CONSTRAINT [CK_CartItems_Quantity_Positive];

                IF OBJECT_ID(N'[dbo].[CK_Orders_UserId_XOR_SessionId]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [CK_Orders_UserId_XOR_SessionId];

                IF OBJECT_ID(N'[dbo].[CK_Orders_TotalPrice_NonNegative]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [CK_Orders_TotalPrice_NonNegative];

                IF OBJECT_ID(N'[dbo].[CK_OrderItems_Price_NonNegative]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[OrderItems] DROP CONSTRAINT [CK_OrderItems_Price_NonNegative];

                IF OBJECT_ID(N'[dbo].[CK_OrderItems_Quantity_Positive]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[OrderItems] DROP CONSTRAINT [CK_OrderItems_Quantity_Positive];

                ALTER TABLE [dbo].[Carts]
                    ADD CONSTRAINT [CK_Carts_UserId_XOR_SessionId]
                    CHECK (([UserId] IS NOT NULL AND [SessionId] IS NULL) OR ([UserId] IS NULL AND [SessionId] IS NOT NULL));

                ALTER TABLE [dbo].[CartItems]
                    ADD CONSTRAINT [CK_CartItems_Quantity_Positive]
                    CHECK ([Quantity] > 0);

                ALTER TABLE [dbo].[Orders]
                    ADD CONSTRAINT [CK_Orders_UserId_XOR_SessionId]
                    CHECK (([UserId] IS NOT NULL AND [SessionId] IS NULL) OR ([UserId] IS NULL AND [SessionId] IS NOT NULL));

                ALTER TABLE [dbo].[Orders]
                    ADD CONSTRAINT [CK_Orders_TotalPrice_NonNegative]
                    CHECK ([TotalPrice] >= 0);

                ALTER TABLE [dbo].[OrderItems]
                    ADD CONSTRAINT [CK_OrderItems_Price_NonNegative]
                    CHECK ([Price] >= 0);

                ALTER TABLE [dbo].[OrderItems]
                    ADD CONSTRAINT [CK_OrderItems_Quantity_Positive]
                    CHECK ([Quantity] > 0);

                CREATE UNIQUE INDEX [UX_Carts_UserId]
                    ON [dbo].[Carts] ([UserId])
                    WHERE [UserId] IS NOT NULL;

                CREATE UNIQUE INDEX [UX_Carts_SessionId]
                    ON [dbo].[Carts] ([SessionId])
                    WHERE [SessionId] IS NOT NULL;

                CREATE UNIQUE INDEX [UX_CartItems_CartId_ProductId]
                    ON [dbo].[CartItems] ([CartId], [ProductId]);

                CREATE UNIQUE INDEX [UX_ShippingAddresses_UserId_Default]
                    ON [dbo].[ShippingAddresses] ([UserId], [IsDefault])
                    WHERE [IsDefault] = 1;

                CREATE INDEX [IX_Orders_UserId_CreatedAt]
                    ON [dbo].[Orders] ([UserId], [CreatedAt]);

                CREATE INDEX [IX_Orders_SessionId_CreatedAt]
                    ON [dbo].[Orders] ([SessionId], [CreatedAt]);
                """);

            migrationBuilder.Sql(
                """
                IF TYPE_ID(N'dbo.OrderItemType') IS NULL
                    EXEC(N'CREATE TYPE [dbo].[OrderItemType] AS TABLE ([ProductId] INT NOT NULL, [Quantity] INT NOT NULL);');
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE [dbo].[sp_CreateOrder]
                    @UserId INT = NULL,
                    @SessionId NVARCHAR(100) = NULL,
                    @FullName NVARCHAR(150),
                    @Phone NVARCHAR(20),
                    @AddressLine NVARCHAR(255),
                    @District NVARCHAR(100),
                    @City NVARCHAR(100),
                    @PaymentMethod NVARCHAR(50),
                    @CartId INT = NULL,
                    @Items [dbo].[OrderItemType] READONLY
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    SET @SessionId = NULLIF(LTRIM(RTRIM(@SessionId)), N'');

                    IF (
                        (CASE WHEN @UserId IS NULL THEN 0 ELSE 1 END) +
                        (CASE WHEN @SessionId IS NULL THEN 0 ELSE 1 END)
                    ) <> 1
                    BEGIN
                        THROW 50003, N'Exactly one of UserId or SessionId is required.', 1;
                    END;

                    IF NULLIF(LTRIM(RTRIM(@FullName)), N'') IS NULL
                        OR NULLIF(LTRIM(RTRIM(@Phone)), N'') IS NULL
                        OR NULLIF(LTRIM(RTRIM(@AddressLine)), N'') IS NULL
                        OR NULLIF(LTRIM(RTRIM(@District)), N'') IS NULL
                        OR NULLIF(LTRIM(RTRIM(@City)), N'') IS NULL
                    BEGIN
                        THROW 50003, N'Shipping address is incomplete.', 1;
                    END;

                    IF NOT EXISTS (SELECT 1 FROM @Items)
                    BEGIN
                        THROW 50002, N'Order must contain at least one item.', 1;
                    END;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DECLARE @NormalizedItems TABLE
                        (
                            [ProductId] INT PRIMARY KEY,
                            [Quantity] INT NOT NULL
                        );

                        INSERT INTO @NormalizedItems ([ProductId], [Quantity])
                        SELECT [ProductId], SUM([Quantity])
                        FROM @Items
                        GROUP BY [ProductId]
                        HAVING SUM([Quantity]) > 0;

                        IF NOT EXISTS (SELECT 1 FROM @NormalizedItems)
                        BEGIN
                            THROW 50002, N'Order must contain at least one item.', 1;
                        END;

                        DECLARE @LockedProducts TABLE
                        (
                            [ProductId] INT PRIMARY KEY,
                            [ProductName] NVARCHAR(200) NOT NULL,
                            [UnitPrice] DECIMAL(18, 2) NOT NULL,
                            [Quantity] INT NOT NULL,
                            [StockQuantity] INT NOT NULL
                        );

                        INSERT INTO @LockedProducts ([ProductId], [ProductName], [UnitPrice], [Quantity], [StockQuantity])
                        SELECT
                            p.[Id],
                            p.[Name],
                            p.[Price],
                            ni.[Quantity],
                            p.[StockQuantity]
                        FROM [dbo].[Products] p WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                        INNER JOIN @NormalizedItems ni ON ni.[ProductId] = p.[Id]
                        WHERE ISNULL(p.[IsDeleted], 0) = 0;

                        IF EXISTS
                        (
                            SELECT 1
                            FROM @NormalizedItems ni
                            LEFT JOIN @LockedProducts lp ON lp.[ProductId] = ni.[ProductId]
                            WHERE lp.[ProductId] IS NULL
                               OR lp.[StockQuantity] < ni.[Quantity]
                        )
                        BEGIN
                            THROW 50001, N'One or more products are unavailable or out of stock.', 1;
                        END;

                        DECLARE @TotalPrice DECIMAL(18, 2);
                        DECLARE @OrderId INT;

                        SELECT @TotalPrice = SUM(lp.[UnitPrice] * lp.[Quantity])
                        FROM @LockedProducts lp;

                        INSERT INTO [dbo].[Orders]
                        (
                            [UserId],
                            [SessionId],
                            [TotalPrice],
                            [Status],
                            [ShippingFullName],
                            [ShippingPhone],
                            [ShippingAddressLine],
                            [ShippingDistrict],
                            [ShippingCity]
                        )
                        VALUES
                        (
                            @UserId,
                            @SessionId,
                            @TotalPrice,
                            N'Pending',
                            @FullName,
                            @Phone,
                            @AddressLine,
                            @District,
                            @City
                        );

                        SET @OrderId = CAST(SCOPE_IDENTITY() AS INT);

                        INSERT INTO [dbo].[OrderItems]
                        (
                            [OrderId],
                            [ProductId],
                            [ProductName],
                            [Price],
                            [Quantity]
                        )
                        SELECT
                            @OrderId,
                            lp.[ProductId],
                            lp.[ProductName],
                            lp.[UnitPrice],
                            lp.[Quantity]
                        FROM @LockedProducts lp;

                        UPDATE p
                        SET p.[StockQuantity] = p.[StockQuantity] - lp.[Quantity]
                        FROM [dbo].[Products] p
                        INNER JOIN @LockedProducts lp ON lp.[ProductId] = p.[Id];

                        INSERT INTO [dbo].[Payments]
                        (
                            [OrderId],
                            [PaymentMethod],
                            [Amount],
                            [Status]
                        )
                        VALUES
                        (
                            @OrderId,
                            @PaymentMethod,
                            @TotalPrice,
                            N'Pending'
                        );

                        IF @CartId IS NOT NULL
                        BEGIN
                            DELETE FROM [dbo].[CartItems]
                            WHERE [CartId] = @CartId;
                        END;

                        COMMIT TRANSACTION;

                        SELECT @OrderId AS [OrderId], @TotalPrice AS [TotalPrice];
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0
                            ROLLBACK TRANSACTION;

                        THROW;
                    END CATCH;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Orders', 'ShippingAddressId') IS NULL
                    ALTER TABLE [dbo].[Orders] ADD [ShippingAddressId] INT NULL;

                IF OBJECT_ID(N'[dbo].[CK_Carts_UserId_XOR_SessionId]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[Carts] DROP CONSTRAINT [CK_Carts_UserId_XOR_SessionId];

                IF OBJECT_ID(N'[dbo].[CK_CartItems_Quantity_Positive]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[CartItems] DROP CONSTRAINT [CK_CartItems_Quantity_Positive];

                IF OBJECT_ID(N'[dbo].[CK_Orders_UserId_XOR_SessionId]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [CK_Orders_UserId_XOR_SessionId];

                IF OBJECT_ID(N'[dbo].[CK_Orders_TotalPrice_NonNegative]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [CK_Orders_TotalPrice_NonNegative];

                IF OBJECT_ID(N'[dbo].[CK_OrderItems_Price_NonNegative]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[OrderItems] DROP CONSTRAINT [CK_OrderItems_Price_NonNegative];

                IF OBJECT_ID(N'[dbo].[CK_OrderItems_Quantity_Positive]', N'C') IS NOT NULL
                    ALTER TABLE [dbo].[OrderItems] DROP CONSTRAINT [CK_OrderItems_Quantity_Positive];

                DROP INDEX IF EXISTS [UX_Carts_UserId] ON [dbo].[Carts];
                DROP INDEX IF EXISTS [UX_Carts_SessionId] ON [dbo].[Carts];
                DROP INDEX IF EXISTS [UX_CartItems_CartId_ProductId] ON [dbo].[CartItems];
                DROP INDEX IF EXISTS [UX_ShippingAddresses_UserId_Default] ON [dbo].[ShippingAddresses];
                DROP INDEX IF EXISTS [IX_Orders_UserId_CreatedAt] ON [dbo].[Orders];
                DROP INDEX IF EXISTS [IX_Orders_SessionId_CreatedAt] ON [dbo].[Orders];

                IF OBJECT_ID(N'[dbo].[FK_Orders_Address]', N'F') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Orders]
                        ADD CONSTRAINT [FK_Orders_Address]
                        FOREIGN KEY ([ShippingAddressId]) REFERENCES [dbo].[ShippingAddresses]([Id]);
                END;

                IF COL_LENGTH('dbo.OrderItems', 'ProductName') IS NOT NULL
                    ALTER TABLE [dbo].[OrderItems] DROP COLUMN [ProductName];

                IF COL_LENGTH('dbo.Orders', 'ShippingFullName') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP COLUMN [ShippingFullName];

                IF COL_LENGTH('dbo.Orders', 'ShippingPhone') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP COLUMN [ShippingPhone];

                IF COL_LENGTH('dbo.Orders', 'ShippingAddressLine') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP COLUMN [ShippingAddressLine];

                IF COL_LENGTH('dbo.Orders', 'ShippingDistrict') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP COLUMN [ShippingDistrict];

                IF COL_LENGTH('dbo.Orders', 'ShippingCity') IS NOT NULL
                    ALTER TABLE [dbo].[Orders] DROP COLUMN [ShippingCity];

                IF COL_LENGTH('dbo.ShippingAddresses', 'UpdatedAt') IS NOT NULL
                    ALTER TABLE [dbo].[ShippingAddresses] DROP COLUMN [UpdatedAt];

                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [UserId] INT NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [FullName] NVARCHAR(150) NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [Phone] NVARCHAR(20) NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [AddressLine] NVARCHAR(255) NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [District] NVARCHAR(100) NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [City] NVARCHAR(100) NULL;
                ALTER TABLE [dbo].[ShippingAddresses] ALTER COLUMN [IsDefault] BIT NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE [dbo].[sp_CreateOrder]
                    @UserId INT = NULL,
                    @SessionId NVARCHAR(100) = NULL,
                    @ShippingAddressId INT,
                    @PaymentMethod NVARCHAR(50),
                    @CartId INT = NULL,
                    @Items [dbo].[OrderItemType] READONLY
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DECLARE @TotalPrice DECIMAL(18, 2);
                        DECLARE @OrderId INT;

                        SELECT @TotalPrice = SUM(p.[Price] * i.[Quantity])
                        FROM @Items i
                        INNER JOIN [dbo].[Products] p ON p.[Id] = i.[ProductId];

                        INSERT INTO [dbo].[Orders]
                        (
                            [UserId],
                            [SessionId],
                            [ShippingAddressId],
                            [TotalPrice],
                            [Status]
                        )
                        VALUES
                        (
                            @UserId,
                            @SessionId,
                            @ShippingAddressId,
                            ISNULL(@TotalPrice, 0),
                            N'Pending'
                        );

                        SET @OrderId = CAST(SCOPE_IDENTITY() AS INT);

                        INSERT INTO [dbo].[OrderItems] ([OrderId], [ProductId], [Price], [Quantity])
                        SELECT @OrderId, p.[Id], p.[Price], i.[Quantity]
                        FROM @Items i
                        INNER JOIN [dbo].[Products] p ON p.[Id] = i.[ProductId];

                        UPDATE p
                        SET p.[StockQuantity] = p.[StockQuantity] - i.[Quantity]
                        FROM [dbo].[Products] p
                        INNER JOIN @Items i ON i.[ProductId] = p.[Id];

                        INSERT INTO [dbo].[Payments] ([OrderId], [PaymentMethod], [Amount], [Status])
                        VALUES (@OrderId, @PaymentMethod, ISNULL(@TotalPrice, 0), N'Pending');

                        IF @CartId IS NOT NULL
                        BEGIN
                            DELETE FROM [dbo].[CartItems]
                            WHERE [CartId] = @CartId;
                        END;

                        COMMIT TRANSACTION;

                        SELECT @OrderId AS [OrderId], ISNULL(@TotalPrice, 0) AS [TotalPrice];
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0
                            ROLLBACK TRANSACTION;

                        THROW;
                    END CATCH;
                END
                """);
        }
    }
}
