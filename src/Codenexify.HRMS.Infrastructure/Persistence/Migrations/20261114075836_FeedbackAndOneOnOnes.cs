using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Codenexify.HRMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FeedbackAndOneOnOnes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestedByEmployeeId = table.Column<int>(type: "int", nullable: false),
                    AboutEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackRequests_Employees_AboutEmployeeId",
                        column: x => x.AboutEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackRequests_Employees_RequestedByEmployeeId",
                        column: x => x.RequestedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackRequests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OneOnOnes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ManagerEmployeeId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    OnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartsAt = table.Column<TimeOnly>(type: "time", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Repeat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SharedNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ManagerPrivateNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EmployeePrivateNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OneOnOnes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OneOnOnes_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OneOnOnes_Employees_ManagerEmployeeId",
                        column: x => x.ManagerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OneOnOnes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorEmployeeId = table.Column<int>(type: "int", nullable: false),
                    AboutEmployeeId = table.Column<int>(type: "int", nullable: false),
                    Tone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FeedbackRequestId = table.Column<int>(type: "int", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackNotes_Employees_AboutEmployeeId",
                        column: x => x.AboutEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackNotes_Employees_AuthorEmployeeId",
                        column: x => x.AuthorEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackNotes_FeedbackRequests_FeedbackRequestId",
                        column: x => x.FeedbackRequestId,
                        principalTable: "FeedbackRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackNotes_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackRequestRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FeedbackRequestId = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackRequestRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedbackRequestRecipients_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackRequestRecipients_FeedbackRequests_FeedbackRequestId",
                        column: x => x.FeedbackRequestId,
                        principalTable: "FeedbackRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeedbackRequestRecipients_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OneOnOneId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AddedByEmployeeId = table.Column<int>(type: "int", nullable: false),
                    OwnerEmployeeId = table.Column<int>(type: "int", nullable: true),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    DoneAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingItems_Employees_OwnerEmployeeId",
                        column: x => x.OwnerEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingItems_OneOnOnes_OneOnOneId",
                        column: x => x.OneOnOneId,
                        principalTable: "OneOnOnes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingItems_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackNotes_AboutEmployeeId",
                table: "FeedbackNotes",
                column: "AboutEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackNotes_AuthorEmployeeId",
                table: "FeedbackNotes",
                column: "AuthorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackNotes_FeedbackRequestId",
                table: "FeedbackNotes",
                column: "FeedbackRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackNotes_OrganizationId_AboutEmployeeId_AtUtc",
                table: "FeedbackNotes",
                columns: new[] { "OrganizationId", "AboutEmployeeId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackNotes_OrganizationId_AuthorEmployeeId_AtUtc",
                table: "FeedbackNotes",
                columns: new[] { "OrganizationId", "AuthorEmployeeId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequestRecipients_EmployeeId",
                table: "FeedbackRequestRecipients",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequestRecipients_FeedbackRequestId_EmployeeId",
                table: "FeedbackRequestRecipients",
                columns: new[] { "FeedbackRequestId", "EmployeeId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequestRecipients_OrganizationId_EmployeeId_Status",
                table: "FeedbackRequestRecipients",
                columns: new[] { "OrganizationId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequests_AboutEmployeeId",
                table: "FeedbackRequests",
                column: "AboutEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequests_OrganizationId",
                table: "FeedbackRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackRequests_RequestedByEmployeeId",
                table: "FeedbackRequests",
                column: "RequestedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingItems_OneOnOneId",
                table: "MeetingItems",
                column: "OneOnOneId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingItems_OrganizationId",
                table: "MeetingItems",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingItems_OwnerEmployeeId",
                table: "MeetingItems",
                column: "OwnerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OneOnOnes_EmployeeId",
                table: "OneOnOnes",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OneOnOnes_ManagerEmployeeId",
                table: "OneOnOnes",
                column: "ManagerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OneOnOnes_OrganizationId_EmployeeId_OnDate",
                table: "OneOnOnes",
                columns: new[] { "OrganizationId", "EmployeeId", "OnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OneOnOnes_OrganizationId_ManagerEmployeeId_OnDate",
                table: "OneOnOnes",
                columns: new[] { "OrganizationId", "ManagerEmployeeId", "OnDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackNotes");

            migrationBuilder.DropTable(
                name: "FeedbackRequestRecipients");

            migrationBuilder.DropTable(
                name: "MeetingItems");

            migrationBuilder.DropTable(
                name: "FeedbackRequests");

            migrationBuilder.DropTable(
                name: "OneOnOnes");
        }
    }
}
