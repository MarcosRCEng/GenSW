using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GenSW.Infrastructure.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class CatalogIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogRoleClaims_CatalogRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "CatalogRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AccessGeneration = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogAccounts", x => x.Id);
                    table.UniqueConstraint("AK_CatalogAccounts_Id_SubjectId", x => new { x.Id, x.SubjectId });
                    table.CheckConstraint("CK_CatalogAccounts_Generation", "\"AccessGeneration\" > 0");
                    table.CheckConstraint("CK_CatalogAccounts_State", "\"State\" BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_CatalogAccounts_CatalogUsers_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "CatalogUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogInitialOperator",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogInitialOperator", x => x.Id);
                    table.CheckConstraint("CK_CatalogInitialOperator_Singleton", "\"Id\" = 1");
                    table.ForeignKey(
                        name: "FK_CatalogInitialOperator_CatalogUsers_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "CatalogUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogUserClaims_CatalogUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "CatalogUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_CatalogUserLogins_CatalogUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "CatalogUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_CatalogUserRoles_CatalogRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "CatalogRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogUserRoles_CatalogUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "CatalogUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_CatalogUserTokens_CatalogUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "CatalogUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ErpActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseName = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    VolumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseSecretRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaintenanceSecretRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VolumeRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpectedMigration = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    VerifiedMigration = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LastMaintenanceFailure = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    Ready = table.Column<bool>(type: "boolean", nullable: false),
                    InMaintenance = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogBindings", x => x.Id);
                    table.UniqueConstraint("AK_CatalogBindings_Id_AccountId_Revision", x => new { x.Id, x.AccountId, x.Revision });
                    table.CheckConstraint("CK_CatalogBindings_Revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_CatalogBindings_CatalogAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "CatalogAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Audience = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    BindingId = table.Column<Guid>(type: "uuid", nullable: true),
                    BindingRevision = table.Column<long>(type: "bigint", nullable: true),
                    AccessGeneration = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSessions", x => x.Id);
                    table.CheckConstraint("CK_CatalogSessions_Audience", "(\"Audience\" = 1 AND \"AccountId\" IS NOT NULL AND \"BindingId\" IS NOT NULL AND\n    \"BindingRevision\" IS NOT NULL AND \"AccessGeneration\" IS NOT NULL AND \"AccessGeneration\" > 0)\nOR (\"Audience\" = 2 AND \"AccountId\" IS NULL AND \"BindingId\" IS NULL AND\n    \"BindingRevision\" IS NULL AND \"AccessGeneration\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_CatalogSessions_CatalogAccounts_AccountId_SubjectId",
                        columns: x => new { x.AccountId, x.SubjectId },
                        principalTable: "CatalogAccounts",
                        principalColumns: new[] { "Id", "SubjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogSessions_CatalogBindings_BindingId_AccountId_Binding~",
                        columns: x => new { x.BindingId, x.AccountId, x.BindingRevision },
                        principalTable: "CatalogBindings",
                        principalColumns: new[] { "Id", "AccountId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CatalogSessions_CatalogUsers_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "CatalogUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Digest = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_CatalogRefreshTokens_Digest", "length(\"Digest\") = 32");
                    table.ForeignKey(
                        name: "FK_CatalogRefreshTokens_CatalogSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "CatalogSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogAccounts_SubjectId",
                table: "CatalogAccounts",
                column: "SubjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBindings_AccountId",
                table: "CatalogBindings",
                column: "AccountId",
                unique: true,
                filter: "\"Published\"");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBindings_AccountId_Revision",
                table: "CatalogBindings",
                columns: new[] { "AccountId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBindings_DatabaseId",
                table: "CatalogBindings",
                column: "DatabaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBindings_VolumeId",
                table: "CatalogBindings",
                column: "VolumeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogBindings_VolumeRef",
                table: "CatalogBindings",
                column: "VolumeRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogInitialOperator_SubjectId",
                table: "CatalogInitialOperator",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRefreshTokens_Digest",
                table: "CatalogRefreshTokens",
                column: "Digest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRefreshTokens_SessionId",
                table: "CatalogRefreshTokens",
                column: "SessionId",
                unique: true,
                filter: "\"ConsumedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRoleClaims_RoleId",
                table: "CatalogRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "CatalogRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSessions_AccountId_SubjectId",
                table: "CatalogSessions",
                columns: new[] { "AccountId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSessions_BindingId_AccountId_BindingRevision",
                table: "CatalogSessions",
                columns: new[] { "BindingId", "AccountId", "BindingRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSessions_SubjectId",
                table: "CatalogSessions",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogUserClaims_UserId",
                table: "CatalogUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogUserLogins_UserId",
                table: "CatalogUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogUserRoles_RoleId",
                table: "CatalogUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "CatalogUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "CatalogUsers",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogInitialOperator");

            migrationBuilder.DropTable(
                name: "CatalogRefreshTokens");

            migrationBuilder.DropTable(
                name: "CatalogRoleClaims");

            migrationBuilder.DropTable(
                name: "CatalogUserClaims");

            migrationBuilder.DropTable(
                name: "CatalogUserLogins");

            migrationBuilder.DropTable(
                name: "CatalogUserRoles");

            migrationBuilder.DropTable(
                name: "CatalogUserTokens");

            migrationBuilder.DropTable(
                name: "CatalogSessions");

            migrationBuilder.DropTable(
                name: "CatalogRoles");

            migrationBuilder.DropTable(
                name: "CatalogBindings");

            migrationBuilder.DropTable(
                name: "CatalogAccounts");

            migrationBuilder.DropTable(
                name: "CatalogUsers");
        }
    }
}
