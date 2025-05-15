using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Makeup.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Location",
                columns: table => new
                {
                    location_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    latitude = table.Column<decimal>(type: "decimal(10,8)", nullable: false),
                    longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: false),
                    address = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "artist")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Location__771831EA93E49A09", x => x.location_id);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    avatar = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    is_active = table.Column<byte>(type: "tinyint", nullable: true, defaultValue: (byte)1),
                    location_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK__Users__location___70DDC3D8",
                        column: x => x.location_id,
                        principalTable: "Location",
                        principalColumn: "location_id");
                });

            migrationBuilder.CreateTable(
                name: "MakeupArtists",
                columns: table => new
                {
                    artist_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    full_name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    bio = table.Column<string>(type: "text", nullable: true),
                    experience = table.Column<int>(type: "int", nullable: true),
                    specialty = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    is_available_at_home = table.Column<byte>(type: "tinyint", nullable: true, defaultValue: (byte)0),
                    rating = table.Column<decimal>(type: "decimal(3,2)", nullable: true, defaultValue: 0.0m),
                    reviews_count = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    is_active = table.Column<byte>(type: "tinyint", nullable: true, defaultValue: (byte)1),
                    location_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__MakeupAr__6CD04001B1BDC0D9", x => x.artist_id);
                    table.ForeignKey(
                        name: "FK_MakeupArtists_Location",
                        column: x => x.location_id,
                        principalTable: "Location",
                        principalColumn: "location_id");
                    table.ForeignKey(
                        name: "FK__MakeupArt__user___6477ECF3",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    message_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sender_id = table.Column<int>(type: "int", nullable: false),
                    receiver_id = table.Column<int>(type: "int", nullable: false),
                    message_content = table.Column<string>(type: "text", nullable: false),
                    message_timestamp = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    is_read = table.Column<byte>(type: "tinyint", nullable: true, defaultValue: (byte)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Messages__0BBF6EE68E5E6D76", x => x.message_id);
                    table.ForeignKey(
                        name: "FK__Messages__receiv__6754599E",
                        column: x => x.receiver_id,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK__Messages__sender__68487DD7",
                        column: x => x.sender_id,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    service_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    service_name = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<byte>(type: "tinyint", nullable: true, defaultValue: (byte)1),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    MakeupArtistArtistId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Services__3E0DB8AF0199314B", x => x.service_id);
                    table.ForeignKey(
                        name: "FK_Services_MakeupArtists_MakeupArtistArtistId",
                        column: x => x.MakeupArtistArtistId,
                        principalTable: "MakeupArtists",
                        principalColumn: "artist_id");
                });

            migrationBuilder.CreateTable(
                name: "ServiceDetails",
                columns: table => new
                {
                    service_detail_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    service_id = table.Column<int>(type: "int", nullable: false),
                    artist_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    price = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    duration = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ServiceD__8327FB05EADDA462", x => x.service_detail_id);
                    table.ForeignKey(
                        name: "FK__ServiceDe__artis__6FEA9F9F",
                        column: x => x.artist_id,
                        principalTable: "MakeupArtists",
                        principalColumn: "artist_id");
                    table.ForeignKey(
                        name: "FK__ServiceDe__servi__6E01572D",
                        column: x => x.service_id,
                        principalTable: "Services",
                        principalColumn: "service_id");
                    table.ForeignKey(
                        name: "FK__ServiceDe__user___6EF57B66",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    appointment_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    artist_id = table.Column<int>(type: "int", nullable: true),
                    service_detail_id = table.Column<int>(type: "int", nullable: false),
                    appointment_date = table.Column<DateTime>(type: "datetime", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "pending"),
                    location_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Appointm__A50828FCB06E2574", x => x.appointment_id);
                    table.ForeignKey(
                        name: "FK__Appointme__artis__60A75C0F",
                        column: x => x.artist_id,
                        principalTable: "MakeupArtists",
                        principalColumn: "artist_id");
                    table.ForeignKey(
                        name: "FK__Appointme__locat__619B8048",
                        column: x => x.location_id,
                        principalTable: "Location",
                        principalColumn: "location_id");
                    table.ForeignKey(
                        name: "FK__Appointme__servi__628FA481",
                        column: x => x.service_detail_id,
                        principalTable: "ServiceDetails",
                        principalColumn: "service_detail_id");
                    table.ForeignKey(
                        name: "FK__Appointme__user___6383C8BA",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    payment_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    appointment_id = table.Column<int>(type: "int", nullable: false),
                    payment_method = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    payment_status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "pending"),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Payments__ED1FC9EA2641BB32", x => x.payment_id);
                    table.ForeignKey(
                        name: "FK__Payments__appoin__693CA210",
                        column: x => x.appointment_id,
                        principalTable: "Appointments",
                        principalColumn: "appointment_id");
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    review_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    appointment_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    artist_id = table.Column<int>(type: "int", nullable: false),
                    rating = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__Reviews__60883D909C72057A", x => x.review_id);
                    table.ForeignKey(
                        name: "FK__Reviews__appoint__6B24EA82",
                        column: x => x.appointment_id,
                        principalTable: "Appointments",
                        principalColumn: "appointment_id");
                    table.ForeignKey(
                        name: "FK__Reviews__artist___6C190EBB",
                        column: x => x.artist_id,
                        principalTable: "MakeupArtists",
                        principalColumn: "artist_id");
                    table.ForeignKey(
                        name: "FK__Reviews__user_id__6D0D32F4",
                        column: x => x.user_id,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_artist_id",
                table: "Appointments",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_location_id",
                table: "Appointments",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_service_detail_id",
                table: "Appointments",
                column: "service_detail_id");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_user_id",
                table: "Appointments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_MakeupArtists_location_id",
                table: "MakeupArtists",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "UQ__MakeupAr__B9BE370E2F4FDF56",
                table: "MakeupArtists",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_receiver_id",
                table: "Messages",
                column: "receiver_id");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_sender_id",
                table: "Messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_appointment_id",
                table: "Payments",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_artist_id",
                table: "Reviews",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_user_id",
                table: "Reviews",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "UQ__Reviews__A50828FD37E48493",
                table: "Reviews",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDetails_artist_id",
                table: "ServiceDetails",
                column: "artist_id");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDetails_service_id",
                table: "ServiceDetails",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDetails_user_id",
                table: "ServiceDetails",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_Services_MakeupArtistArtistId",
                table: "Services",
                column: "MakeupArtistArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_User_location_id",
                table: "User",
                column: "location_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "RoleClaims");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "UserClaims");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTokens");

            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "ServiceDetails");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "MakeupArtists");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "Location");
        }
    }
}
