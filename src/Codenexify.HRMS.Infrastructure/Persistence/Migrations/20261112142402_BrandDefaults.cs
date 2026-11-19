using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Codenexify.HRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BrandDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Organizations still on the previous default colour (teal) move to the Codenexify brand blue.
            // Any colour an organization picked itself is left alone.
            migrationBuilder.Sql("UPDATE [Organizations] SET [BrandColor] = '#1F5EFF' WHERE LOWER([BrandColor]) = '#0f766e';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Organizations] SET [BrandColor] = '#0f766e' WHERE [BrandColor] = '#1F5EFF';");
        }
    }
}
