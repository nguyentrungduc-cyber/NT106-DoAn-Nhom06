using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Services;
using SecureChat.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 256 * 1024);

var connStr = builder.Configuration.GetConnectionString("Default")
	?? throw new InvalidOperationException("Connection string 'Default' not found.");

if (connStr.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
	|| connStr.Contains(".db", StringComparison.OrdinalIgnoreCase)
	|| connStr.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
{
	throw new InvalidOperationException("SQLite connection string detected. SecureChat.Server only supports MariaDB/MySQL.");
}

builder.Services.AddDbContext<AppDbContext>(o => o.UseMySql(
    connStr,
	ServerVersion.AutoDetect(connStr),
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
builder.Services.AddSingleton<PresenceTracker>();
builder.Services.AddScoped<JwtTokenService>();
// Email service used by forgot-password flow. Registered as singleton so it can be reused.
builder.Services.AddSingleton<EmailService>();
// OtpService holds in-memory OTP state and must be a singleton so state is preserved across requests
builder.Services.AddSingleton<OtpService>();
builder.Services.AddSingleton<ForgotPasswordService>();

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

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

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
app.MapControllers();
app.MapHub<SecureChat.Server.Hubs.ChatHub>("/hubs/chat");

app.Run();
