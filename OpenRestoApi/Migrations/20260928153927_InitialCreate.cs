using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenRestoApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PasswordSalt = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Owner"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    PvqQuestion = table.Column<string>(type: "text", nullable: true),
                    PvqAnswerHash = table.Column<string>(type: "text", nullable: true),
                    PvqAnswerSalt = table.Column<string>(type: "text", nullable: true),
                    ResetToken = table.Column<string>(type: "text", nullable: true),
                    ResetTokenExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminPushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Endpoint = table.Column<string>(type: "text", nullable: false),
                    P256dh = table.Column<string>(type: "text", nullable: false),
                    Auth = table.Column<string>(type: "text", nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrandSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AppName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PrimaryColor = table.Column<string>(type: "text", nullable: false),
                    AccentColor = table.Column<string>(type: "text", nullable: true),
                    HeaderImageUrl = table.Column<string>(type: "text", nullable: true),
                    FaviconIcon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    EmailAddress = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CopyrightText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Subtitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    HighlightsHeading = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    HighlightsSubheading = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PrivacyPolicyUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    MinimumAppVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    HeaderImageFit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailFailures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingRef = table.Column<string>(type: "text", nullable: true),
                    RecipientEmail = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailFailures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Host = table.Column<string>(type: "text", nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "text", nullable: false),
                    EnableSsl = table.Column<bool>(type: "boolean", nullable: false),
                    FromName = table.Column<string>(type: "text", nullable: true),
                    FromEmail = table.Column<string>(type: "text", nullable: true),
                    SendBookingConfirmations = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Highlights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IconKey = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Highlights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NativeClientStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AppVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestCount = table.Column<int>(type: "integer", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NativeClientStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    OpenTime = table.Column<string>(type: "text", nullable: false),
                    CloseTime = table.Column<string>(type: "text", nullable: false),
                    OpenDays = table.Column<string>(type: "text", nullable: false),
                    OpenHoursJson = table.Column<string>(type: "text", nullable: true),
                    Timezone = table.Column<string>(type: "text", nullable: false),
                    BookingsPausedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MenuUrl = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    EmailAddress = table.Column<string>(type: "text", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    WalkInOnly = table.Column<bool>(type: "boolean", nullable: false),
                    WalkInDays = table.Column<string>(type: "text", nullable: true),
                    DefaultBookingDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    TurnTimesJson = table.Column<string>(type: "text", nullable: true),
                    BookingSlotIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxTableOversizeSeats = table.Column<int>(type: "integer", nullable: true),
                    MaxCoversPerSlot = table.Column<int>(type: "integer", nullable: true),
                    BookingRefFormat = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    IconKey = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminApiKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ScopesJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminApiKeys_AdminCredentials_UserId",
                        column: x => x.UserId,
                        principalTable: "AdminCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminAuditEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ActorDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TargetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TargetLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RestaurantId = table.Column<int>(type: "integer", nullable: true),
                    Summary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ChangesJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAuditEntries_AdminCredentials_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AdminCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TableGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    CombinedSeats = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TableGroups_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    WalkInOnly = table.Column<bool>(type: "boolean", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tables_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    SectionId = table.Column<int>(type: "integer", nullable: true),
                    TableGroupId = table.Column<int>(type: "integer", nullable: true),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerEmail = table.Column<string>(type: "text", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    SpecialRequests = table.Column<string>(type: "text", nullable: true),
                    BookingRef = table.Column<string>(type: "text", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    StatusChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginalEndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookings_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bookings_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Bookings_TableGroups_TableGroupId",
                        column: x => x.TableGroupId,
                        principalTable: "TableGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Bookings_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TableGroupMemberships",
                columns: table => new
                {
                    TableGroupId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableGroupMemberships", x => new { x.TableGroupId, x.TableId });
                    table.ForeignKey(
                        name: "FK_TableGroupMemberships_TableGroups_TableGroupId",
                        column: x => x.TableGroupId,
                        principalTable: "TableGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TableGroupMemberships_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    BookingRef = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    BookingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    RestaurantName = table.Column<string>(type: "text", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PushSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PushError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminNotifications_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AdminNotifications_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuestPushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Auth = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReminderLeadHours = table.Column<int>(type: "integer", nullable: true),
                    LastReminderSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestPushSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestPushSubscriptions_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    Ref = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PushChannel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    PushEndpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PushP256dh = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PushAuth = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BookingId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WaitlistEntries_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminApiKeys_KeyHash",
                table: "AdminApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminApiKeys_UserId",
                table: "AdminApiKeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_Action",
                table: "AdminAuditEntries",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_ActorUserId_OccurredAt",
                table: "AdminAuditEntries",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_OccurredAt",
                table: "AdminAuditEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_RestaurantId_OccurredAt",
                table: "AdminAuditEntries",
                columns: new[] { "RestaurantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminCredentials_Email",
                table: "AdminCredentials",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotifications_BookingId",
                table: "AdminNotifications",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotifications_RestaurantId_CreatedAt",
                table: "AdminNotifications",
                columns: new[] { "RestaurantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotifications_RestaurantId_IsRead",
                table: "AdminNotifications",
                columns: new[] { "RestaurantId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminPushSubscriptions_Endpoint",
                table: "AdminPushSubscriptions",
                column: "Endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RestaurantId",
                table: "Bookings",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_SectionId",
                table: "Bookings",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TableGroupId",
                table: "Bookings",
                column: "TableGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_TableId",
                table: "Bookings",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestPushSubscriptions_BookingId_Endpoint",
                table: "GuestPushSubscriptions",
                columns: new[] { "BookingId", "Endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NativeClientStats_Platform_AppVersion_Day",
                table: "NativeClientStats",
                columns: new[] { "Platform", "AppVersion", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sections_RestaurantId",
                table: "Sections",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_TableGroupMemberships_TableId",
                table: "TableGroupMemberships",
                column: "TableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TableGroups_RestaurantId",
                table: "TableGroups",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_SectionId",
                table: "Tables",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_BookingId",
                table: "WaitlistEntries",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_Ref",
                table: "WaitlistEntries",
                column: "Ref",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_RestaurantId_Status",
                table: "WaitlistEntries",
                columns: new[] { "RestaurantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminApiKeys");

            migrationBuilder.DropTable(
                name: "AdminAuditEntries");

            migrationBuilder.DropTable(
                name: "AdminNotifications");

            migrationBuilder.DropTable(
                name: "AdminPushSubscriptions");

            migrationBuilder.DropTable(
                name: "BrandSettings");

            migrationBuilder.DropTable(
                name: "EmailFailures");

            migrationBuilder.DropTable(
                name: "EmailSettings");

            migrationBuilder.DropTable(
                name: "GuestPushSubscriptions");

            migrationBuilder.DropTable(
                name: "Highlights");

            migrationBuilder.DropTable(
                name: "NativeClientStats");

            migrationBuilder.DropTable(
                name: "SocialLinks");

            migrationBuilder.DropTable(
                name: "TableGroupMemberships");

            migrationBuilder.DropTable(
                name: "WaitlistEntries");

            migrationBuilder.DropTable(
                name: "AdminCredentials");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "TableGroups");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "Restaurants");
        }
    }
}
