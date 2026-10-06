using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgriAssist.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalCultivationGuideStepUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AgentSteps_FinalGuideRevision",
                table: "AgentSteps",
                columns: new[] { "AgentWorkflowId", "CandidateRevision" },
                unique: true,
                filter: "\"AgentName\" = 'FinalCultivationGuideAgent' AND \"IsDeleted\" = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentSteps_FinalGuideRevision",
                table: "AgentSteps");
        }
    }
}
