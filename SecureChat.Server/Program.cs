using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Services;
using SecureChat.Services;

try
{
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 256 * 1024);

var connStr = builder.Configuration.GetConnectionString("Default");

// Parse mysql://user:pass@host:port/db (Railway MySQL URL format) into ADO.NET format
static string? ParseMySqlUrl(string url)
{
    if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("mysql://"))
        return null;
    var rest = url["mysql://".Length..];
    var atIdx = rest.IndexOf('@');
    if (atIdx <= 0) return null;
    var userPass = rest[..atIdx].Split(':');
    if (userPass.Length < 2) return null;
    var hostPortDb = rest[(atIdx + 1)..];
    var slashIdx = hostPortDb.IndexOf('/');
    var hostPort = slashIdx > 0 ? hostPortDb[..slashIdx] : hostPortDb;
    var db = slashIdx > 0 ? hostPortDb[(slashIdx + 1)..] : "railway";
    var colonIdx = hostPort.IndexOf(':');
    var srvHost = colonIdx > 0 ? hostPort[..colonIdx] : hostPort;
    var srvPort = colonIdx > 0 ? hostPort[(colonIdx + 1)..] : "3306";
    return $"server={srvHost};port={srvPort};database={db};user={userPass[0]};password={userPass[1]}";
}

// ConnectionStrings:Default from config might already be mysql:// (Railway sets it)
if (connStr != null && connStr.StartsWith("mysql://"))
    connStr = ParseMySqlUrl(connStr);

// MYSQL_URL env var overrides config (also mysql:// format)
var mySqlUrl = Environment.GetEnvironmentVariable("MYSQL_URL");
var parsedUrl = ParseMySqlUrl(mySqlUrl);
if (parsedUrl != null)
    connStr = parsedUrl;

// MYSQL_HOST + individual env vars (overrides)
var mySqlHost = Environment.GetEnvironmentVariable("MYSQL_HOST");
if (!string.IsNullOrWhiteSpace(mySqlHost))
{
    var mySqlPort  = (Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306").Trim();
    var mySqlDb    = (Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "railway").Trim();
    var mySqlUser  = (Environment.GetEnvironmentVariable("MYSQL_USER") ?? "railway").Trim();
    var mySqlPass  = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "";
    connStr = $"server={mySqlHost.Trim()};port={mySqlPort};database={mySqlDb};user={mySqlUser};password={mySqlPass}";
}
if (connStr != null)
    connStr = connStr.Trim();
if (string.IsNullOrEmpty(connStr))
{
    Console.Error.WriteLine("MYSQL_URL=" + (Environment.GetEnvironmentVariable("MYSQL_URL") ?? "(null)"));
    Console.Error.WriteLine("MYSQL_HOST=" + (Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "(null)"));
    Console.Error.WriteLine("ConnectionStrings:Default=" + (builder.Configuration.GetConnectionString("Default") ?? "(null)"));
    throw new InvalidOperationException("Connection string not found. Set ConnectionStrings:Default or MYSQL_HOST env vars.");
}
Console.Error.WriteLine($"DB conn str prefix: {connStr[..Math.Min(connStr.Length, 60)]}");

if (connStr.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
	|| connStr.Contains(".db", StringComparison.OrdinalIgnoreCase)
	|| connStr.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
{
	throw new InvalidOperationException("SQLite connection string detected. SecureChat.Server only supports MariaDB/MySQL.");
}

var mySqlVersion = Environment.GetEnvironmentVariable("MYSQL_VERSION") ?? "8.0.0";

builder.Services.AddDbContext<AppDbContext>(o => o.UseMySql(
    connStr,
	ServerVersion.Parse(mySqlVersion),
	my => {
		my.MigrationsAssembly("SecureChat.Server");
		my.EnableRetryOnFailure(maxRetryCount: 3);
	})
);

builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<FriendRepository>();
builder.Services.AddScoped<ConversationRepository>();
builder.Services.AddScoped<MessageRepository>();
builder.Services.AddScoped<CallRepository>();
builder.Services.AddScoped<PrivacyRepository>();
builder.Services.AddSingleton<UserPresenceService>();
builder.Services.AddSingleton<GroupLockService>();
builder.Services.AddScoped<JwtTokenService>();
// Email service used by forgot-password flow. Registered as singleton so it can be reused.
builder.Services.AddSingleton<EmailService>();
// OtpService holds in-memory OTP state and must be a singleton so state is preserved across requests
builder.Services.AddSingleton<OtpService>();
builder.Services.AddSingleton<ForgotPasswordService>();
builder.Services.AddHostedService<CallTimeoutService>();
builder.Services.AddHostedService<AutoDeleteMessageService>();

var jwtKey = builder.Configuration["Jwt:Key"]
	?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
	o.TokenValidationParameters = new TokenValidationParameters {
		ValidateIssuerSigningKey = true,
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
		ValidateIssuer = true,
		ValidateAudience = true,
		ValidIssuer = builder.Configuration["Jwt:Issuer"],
		ValidAudience = builder.Configuration["Jwt:Audience"],
		ClockSkew = TimeSpan.FromMinutes(5)
	};
 // Allow JWT access token via query string for SignalR clients
	o.Events = new JwtBearerEvents
	{
		OnMessageReceived = context =>
		{
			var accessToken = context.Request.Query["access_token"];
			var path = context.HttpContext.Request.Path;
			if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/chat"))
			{
				context.Token = accessToken;
			}
			return Task.CompletedTask;
		}
	};
});

builder.Services.AddAuthorization();


builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524288000; // 500MB
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524288000; // 500MB
});

builder.Services.AddControllers().AddJsonOptions(o => {
		o.JsonSerializerOptions.Converters.Add(
			new System.Text.Json.Serialization.JsonStringEnumConverter());
	}
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "SecureChat API", Version = "v1" } );

var scheme = new OpenApiSecurityScheme {
	Name         = "Authorization",
	Type         = SecuritySchemeType.Http,
	Scheme       = "bearer",
	BearerFormat = "JWT",
	In           = ParameterLocation.Header,
	Description  = "Nhập JWT access token (không cần tiền tố 'Bearer ')"
};

c.AddSecurityDefinition("Bearer", scheme);
c.AddSecurityRequirement(new OpenApiSecurityRequirement {
		{
			new OpenApiSecurityScheme {
				Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id   = "Bearer" }
			},
			Array.Empty<string>()
		}
	});
});

// Railway: bind to dynamic $PORT, fallback to 5000
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Production CORS: allow SignalR with credentials from configured origins
var corsOrigins = (builder.Configuration["CorsOrigins"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(corsOrigins).AllowCredentials().AllowAnyMethod().AllowAnyHeader()));
}
else
{
    // Development fallback (no credentials needed without SignalR cookies)
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
}

var app = builder.Build();

// Auto-apply EF Core migrations on startup (idempotent, safe for Railway)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // MySQL 9.x strict sql_mode rejects DEFAULT current_timestamp
    // Open connection first so SET SESSION applies to the same connection Migrate() uses
    var dbConn = db.Database.GetDbConnection();
    dbConn.Open();
    using (var cmd = dbConn.CreateCommand())
    {
        cmd.CommandText = "SET SESSION sql_mode = ''";
        cmd.ExecuteNonQuery();
    }
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
	app.UseSwaggerUI(c => {
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "SecureChat API v1");
		// Mount Swagger UI at root to restore previous behavior
		c.RoutePrefix = string.Empty;
	});
}

// Serve static files from wwwroot (uploads will be available under /uploads)

// With this:
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint for Railway
app.MapGet("/", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapControllers();
app.MapHub<SecureChat.Server.Hubs.ChatHub>("/hubs/chat");

// Create Saved Messages conversations for existing users who lack one
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var usersNeedingSaved = await db.Users
        .Where(u => !db.Conversations
            .Where(c => c.Type == SecureChat.Models.ConversationType.SavedMessages)
            .Any(c => c.Members.Any(m => m.UserID == u.UserID)))
        .ToListAsync();

    foreach (var user in usersNeedingSaved)
    {
        var convID = Guid.NewGuid().ToString("N")[..8];
        db.Conversations.Add(new SecureChat.Models.Conversation
        {
            ConversationID = convID,
            Type = SecureChat.Models.ConversationType.SavedMessages,
            Name = "Saved Messages",
            CreatedBy = user.UserID,
            CreatedAt = DateTime.UtcNow
        });
        db.ConversationMembers.Add(new SecureChat.Models.ConversationMember
        {
            MemberID       = Guid.NewGuid().ToString("N")[..8],
            ConversationID = convID,
            UserID         = user.UserID,
            EncryptedKey   = "",
            Role           = SecureChat.Models.MemberRole.Owner,
            JoinedAt       = DateTime.UtcNow
        });
    }
    await db.SaveChangesAsync();
}

app.Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine("=== STARTUP EXCEPTION (FATAL) ===");
    var current = ex;
    int depth = 0;
    while (current != null)
    {
        Console.Error.WriteLine($"--- Depth {depth}: {current.GetType().FullName} ---");
        Console.Error.WriteLine($"Message: {current.Message}");
        Console.Error.WriteLine($"StackTrace: {current.StackTrace}");
        current = current.InnerException;
        depth++;
    }
    Console.Error.WriteLine("===================================");
    throw;
}
