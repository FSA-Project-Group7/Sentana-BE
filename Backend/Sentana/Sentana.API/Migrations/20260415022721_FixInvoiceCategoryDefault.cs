using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentana.API.Migrations
{
    /// <inheritdoc />
    public partial class FixInvoiceCategoryDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop default constraint cũ nếu có
            migrationBuilder.Sql(@"
                DECLARE @ConstraintName NVARCHAR(200);
                SELECT @ConstraintName = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON dc.parent_column_id = c.column_id
                INNER JOIN sys.tables t ON dc.parent_object_id = t.object_id
                WHERE t.name = 'Invoice' AND c.name = 'Category';

                IF @ConstraintName IS NOT NULL
                BEGIN
                    DECLARE @SQL NVARCHAR(500);
                    SET @SQL = 'ALTER TABLE Invoice DROP CONSTRAINT ' + @ConstraintName;
                    EXEC sp_executesql @SQL;
                END
            ");

            // Thêm default constraint mới với value = 2
            migrationBuilder.Sql(@"
                ALTER TABLE Invoice
                ADD CONSTRAINT DF_Invoice_Category DEFAULT 2 FOR Category;
            ");

            // Update tất cả invoice cũ có Category = 0 hoặc NULL
            migrationBuilder.Sql(@"
                UPDATE Invoice
                SET Category = 2
                WHERE (Category = 0 OR Category IS NULL) AND IsDeleted = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop constraint
            migrationBuilder.Sql(@"
                DECLARE @ConstraintName NVARCHAR(200);
                SELECT @ConstraintName = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON dc.parent_column_id = c.column_id
                INNER JOIN sys.tables t ON dc.parent_object_id = t.object_id
                WHERE t.name = 'Invoice' AND c.name = 'Category';

                IF @ConstraintName IS NOT NULL
                BEGIN
                    DECLARE @SQL NVARCHAR(500);
                    SET @SQL = 'ALTER TABLE Invoice DROP CONSTRAINT ' + @ConstraintName;
                    EXEC sp_executesql @SQL;
                END
            ");

            // Add back old default = 0
            migrationBuilder.Sql(@"
                ALTER TABLE Invoice
                ADD CONSTRAINT DF_Invoice_Category DEFAULT 0 FOR Category;
            ");
        }
    }
}
