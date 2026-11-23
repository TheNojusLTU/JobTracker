using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobTracker.Migrations
{
    /// <inheritdoc />
    public partial class TrackApplicationMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasReachedInterview",
                table: "JobApplications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasReachedOffer",
                table: "JobApplications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Existing rows only record their current status (2 = Interview, 3 = Offer).
            migrationBuilder.Sql("""
                UPDATE "JobApplications"
                SET "HasReachedInterview" = ("Status" = 2),
                    "HasReachedOffer" = ("Status" = 3)
                WHERE "Status" IN (2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasReachedInterview",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "HasReachedOffer",
                table: "JobApplications");
        }
    }
}
