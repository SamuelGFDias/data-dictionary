using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sample.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tb_dicionario_dados",
                columns: table => new
                {
                    enum_key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    field_name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    numeric_value = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    group_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    is_active = table.Column<int>(type: "int", nullable: false),
                    is_deprecated = table.Column<bool>(type: "bit", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    content_hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_dicionario_dados", x => new { x.enum_key, x.field_name });
                });

            migrationBuilder.CreateTable(
                name: "tb_dicionario_enum",
                columns: table => new
                {
                    enum_key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    clr_full_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    assembly_name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    group_name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    manifest_hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    last_sync_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_dicionario_enum", x => x.enum_key);
                });

            migrationBuilder.InsertData(
                table: "tb_dicionario_dados",
                columns: new[] { "enum_key", "field_name", "code", "content_hash", "created_at", "description", "group_name", "is_active", "is_deprecated", "numeric_value", "sort_order", "updated_at" },
                values: new object[,]
                {
                    { "RacaCor", "Amarela", "AM", "c1381bb9954f3aa46311c5ce3932a41897cddf18c4abf1d70c1b6b8616690d98", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Amarela", "Cadastro", 1, false, 4L, 3, null },
                    { "RacaCor", "Branca", "B", "0a2ed89f3b69376770586f8bce24ad8557176dc5274b7f602f1e31a568cc962f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Branca", "Cadastro", 1, false, 1L, 0, null },
                    { "RacaCor", "Indigena", "I", "54f7d9099698465c86ebbd8ba0f2d7522f07791fff4d7e02445a50ba947e02e1", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Indígena", "Cadastro", 1, false, 5L, 4, null },
                    { "RacaCor", "Parda", "PA", "b38a0a5f55e0268c459aa3e8504104213e6a4b9475ff94ecd25cca790d58239f", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Parda", "Cadastro", 1, false, 3L, 2, null },
                    { "RacaCor", "Preta", "P", "20ef683e4825f423341ef4f3ff89fcf727b4b2ae37afd03dd2f332703fc8cd28", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Preta", "Cadastro", 1, false, 2L, 1, null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_tb_dicionario_dados_enum_key_code_active",
                table: "tb_dicionario_dados",
                columns: new[] { "enum_key", "code" },
                unique: true,
                filter: "is_active = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_dicionario_dados");

            migrationBuilder.DropTable(
                name: "tb_dicionario_enum");
        }
    }
}
