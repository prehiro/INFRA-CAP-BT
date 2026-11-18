using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedUpsRegister : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// The register's starting contents, transcribed from the sheet HIRO pointed at
        /// (reff/Screenshot 2026-11-09 155526_035615.png), minus its UPS Condition block.
        ///
        /// WHY AS DATA AND NOT A TYPED SEEDER: the office server has its own database, so the rows have to
        /// arrive through a migration for both machines to end up with the same register. The guard makes it
        /// a one-time fill: a database where the register has already been edited is left as it is.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [UpsUnits])
                BEGIN
                    INSERT INTO [UpsUnits] ([Brand], [Model], [SerialNumber], [Location], [CreatedAt]) VALUES
                    (N'APC', N'Smart-UPS SRT 5000VA 230V', N'AS2143171426', N'CAP Server Room B2L2', SYSUTCDATETIME()),
                    (N'APC', N'Smart-UPS SRT 5000VA 230V', N'AS2234272494', N'CAP Server Room B2L2', SYSUTCDATETIME()),
                    (N'APC', N'Smart-UPS SMT3000RMI2UC',    N'AS2420144494', N'CAP Prod B2 - Rack B', SYSUTCDATETIME()),
                    (N'APC', N'Smart-UPS SMT3000RMI2UC',    N'AS2420144435', N'CAP Prod B2 - Rack C', SYSUTCDATETIME()),
                    (N'APC', N'Smart-UPS SMT3000RMI2UC',    N'AS2420144437', N'CAP Prod B1 - Rack D', SYSUTCDATETIME()),
                    (N'APC', N'Smart-UPS SMT3000RMI2UC',    N'AS2202257878', N'CAP MAIC Room', SYSUTCDATETIME()),
                    (N'APC', N'Smart-UPS SMT3000RMI2UC',    N'AS2202257879', N'CAP Bending Room - Rack E', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'4B1737P11418', N'CAP IQC Room', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'4B1737P27159', N'CAP Kyoto Room', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'4B1801P03330', N'CAP Prod B2 - Anode Trimming', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'4B1703P31312', N'CAP Prod B2 - Stacking 026', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'4B1714P27431', N'CAP Prod B2 - Stacking 001', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'3B1128X27311', N'CAP Prod B2 - Molding', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'5B2446TA4382', N'CAP Prod B1 - Packing', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'5B2631T01026', N'CAP Prod B1 - Blasting eGov trolley', SYSUTCDATETIME()),
                    (N'APC', N'Back UPS Pro 1500',          N'5B2631T40317', N'CAP Prod B2 - Stacking eGov trolley', SYSUTCDATETIME()),
                    (N'PowerPlus', N'2200AP',               N'10015721901', N'CAP IQC Room', SYSUTCDATETIME()),
                    (N'PowerPlus', N'2200AP',               N'10033681804', N'CAP IQC Room', SYSUTCDATETIME()),
                    (N'PowerPlus', N'2200AP',               N'10018172304', N'CAP Prod B1 - Outgoing QC', SYSUTCDATETIME()),
                    (N'PowerPlus', N'2200AP',               N'10033851804', N'CAP Prod B1 - Outgoing QC', SYSUTCDATETIME());
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
