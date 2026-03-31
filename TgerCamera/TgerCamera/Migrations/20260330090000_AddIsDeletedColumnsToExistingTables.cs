using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TgerCamera.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedColumnsToExistingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IsDeleted column to Brands table
            migrationBuilder.Sql(
                @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Brands' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [Brands] ADD [IsDeleted] bit NULL DEFAULT CAST(0 AS bit);");

            // Add IsDeleted column to Categories table
            migrationBuilder.Sql(
                @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Categories' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [Categories] ADD [IsDeleted] bit NULL DEFAULT CAST(0 AS bit);");

            // Add IsDeleted column to Products table
            migrationBuilder.Sql(
                @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [Products] ADD [IsDeleted] bit NULL DEFAULT CAST(0 AS bit);");

            // Add IsDeleted column to RentalProducts table
            migrationBuilder.Sql(
                @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'RentalProducts' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [RentalProducts] ADD [IsDeleted] bit NULL DEFAULT CAST(0 AS bit);");

            // Add IsDeleted column to Orders table
            migrationBuilder.Sql(
                @"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [Orders] ADD [IsDeleted] bit NULL DEFAULT CAST(0 AS bit);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop IsDeleted columns
            migrationBuilder.Sql(
                @"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Brands' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [Brands] DROP COLUMN [IsDeleted];");

            migrationBuilder.Sql(
                @"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Categories' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [Categories] DROP COLUMN [IsDeleted];");

            migrationBuilder.Sql(
                @"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [Products] DROP COLUMN [IsDeleted];");

            migrationBuilder.Sql(
                @"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'RentalProducts' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [RentalProducts] DROP COLUMN [IsDeleted];");

            migrationBuilder.Sql(
                @"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders' AND COLUMN_NAME = 'IsDeleted')
                  ALTER TABLE [Orders] DROP COLUMN [IsDeleted];");
        }
    }
}
