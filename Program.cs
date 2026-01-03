using inflan_api.MyDBContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using inflan_api.Interfaces;
using inflan_api.Repositories;
using inflan_api.Services;
using inflan_api.Services.Payment;
using inflan_api.Models;
using inflan_api.Hubs;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;

namespace inflan_api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            
            // Add database context
            builder.Services.AddDbContext<InflanDBContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
            
            // Configure Social Blade settings
            builder.Services.Configure<SocialBladeConfig>(
                builder.Configuration.GetSection(SocialBladeConfig.SectionName));
            
            // Configure Follower Sync settings
            builder.Services.Configure<FollowerSyncConfig>(
                builder.Configuration.GetSection(FollowerSyncConfig.SectionName));

            // Configure Milestone Payment settings
            builder.Services.Configure<MilestonePaymentConfig>(
                builder.Configuration.GetSection(MilestonePaymentConfig.SectionName));

            // Configure file upload limits (25MB to allow for 20MB videos with overhead)
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 25 * 1024 * 1024; // 25MB
            });

            // Configure Kestrel limits for larger uploads
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.MaxRequestBodySize = 25 * 1024 * 1024; // 25MB
            });

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // Ensure camelCase from frontend maps to PascalCase in C# models
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var errors = context.ModelState
                            .Where(e => e.Value?.Errors.Count > 0)
                            .Select(e => new
                            {
                                field = e.Key,
                                message = e.Value?.Errors.First().ErrorMessage
                            })
                            .ToList();

                        var result = new
                        {
                            message = "Validation failed",
                            code = "VALIDATION_ERROR",
                            errors = errors
                        };

                        return new BadRequestObjectResult(result);
                    };
                });
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddTransient<IUserRepository, UserRepository>();
            builder.Services.AddTransient<IUserService, UserService>();
            builder.Services.AddTransient<IAuthService, AuthService>();
            builder.Services.AddTransient<IInfluencerRepository, InfluencerRepository>();
            builder.Services.AddTransient<IInfluencerService, InfluencerService>();
            builder.Services.AddTransient<IPlanRepository, PlanRepository>();
            builder.Services.AddTransient<IPlanService, PlanService>();
            builder.Services.AddTransient<ITransactionRepository, TransactionRepository>();
            builder.Services.AddTransient<ICampaignRepository, CampaignRepository>();
            builder.Services.AddTransient<ICampaignService, CampaignService>();
            builder.Services.AddTransient<IPaymentService, PaymentService>();

            // Register PDF and Email services
            builder.Services.AddTransient<IPdfGenerationService, PdfGenerationService>();
            builder.Services.AddTransient<IEmailService, EmailService>();

            // Register Payment Module services
            builder.Services.AddMemoryCache();
            builder.Services.AddTransient<IPaymentMilestoneRepository, PaymentMilestoneRepository>();
            builder.Services.AddTransient<IPaymentMethodRepository, PaymentMethodRepository>();
            builder.Services.AddTransient<IInvoiceRepository, InvoiceRepository>();
            builder.Services.AddTransient<IInfluencerPayoutRepository, InfluencerPayoutRepository>();
            builder.Services.AddTransient<IWithdrawalRepository, WithdrawalRepository>();
            builder.Services.AddTransient<IInfluencerBankAccountRepository, InfluencerBankAccountRepository>();
            builder.Services.AddTransient<IPlatformSettingsService, PlatformSettingsService>();
            builder.Services.AddTransient<IMilestoneService, MilestoneService>();
            builder.Services.AddTransient<TrueLayerGateway>();
            builder.Services.AddTransient<PaystackGateway>();
            builder.Services.AddTransient<IPaymentGatewayFactory, PaymentGatewayFactory>();
            builder.Services.AddTransient<IPaymentOrchestrator, PaymentOrchestrator>();
            builder.Services.AddTransient<IInvoicePdfService, InvoicePdfService>();

            // Register Chat services
            builder.Services.AddScoped<IChatRepository, ChatRepository>();
            builder.Services.AddScoped<IChatService, ChatService>();

            // Register Notification services
            builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<INotificationService, NotificationService>();

            // Add SignalR
            builder.Services.AddSignalR();

            // Register follower count service with HTTP client
            builder.Services.AddHttpClient<IFollowerCountService, SocialBladeFollowerService>()
                .AddPolicyHandler(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(msg => !msg.IsSuccessStatusCode)
                    .WaitAndRetryAsync(
                        3,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            Console.WriteLine($"HTTP Retry {retryCount} after {timespan} seconds");
                        }));
            
            // Register background service for weekly sync
            builder.Services.AddHostedService<FollowerSyncBackgroundService>();

            // Register background service for milestone auto-payment
            builder.Services.AddHostedService<MilestonePaymentBackgroundService>();

            builder.Services.AddAuthentication(cfg => {
                cfg.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                cfg.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                cfg.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(x => {
                x.RequireHttpsMetadata = false;
                x.SaveToken = false;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8
                        .GetBytes(builder.Configuration["Jwt:Key"])
                    ),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };

                // Configure JWT for SignalR - read token from query string
                x.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        // If the request is for our hub, read the token from query string
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
            
            builder.Services.AddSwaggerGen(c => {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Inflan Web Api",
                    Version = "v1"
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\"",
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement {
                    {
                        new OpenApiSecurityScheme {
                            Reference = new OpenApiReference {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });

                // SignalR requires credentials, so we need a specific policy
                options.AddPolicy("SignalRPolicy", policy =>
                {
                    policy
                        .SetIsOriginAllowed(_ => true) // Allow any origin
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials(); // Required for SignalR
                });
            });


            var app = builder.Build();

            // Skip auto-migration - migrations are already applied and auto-migrate causes issues
            // Run migrations manually with: dotnet ef database update
            // using (var scope = app.Services.CreateScope())
            // {
            //     try
            //     {
            //         var context = scope.ServiceProvider.GetRequiredService<InflanDBContext>();
            //         context.Database.Migrate();
            //     }
            //     catch (Exception ex)
            //     {
            //         // Log migration error but don't fail startup
            //         Console.WriteLine($"Migration failed: {ex.Message}");
            //         Console.WriteLine("Continuing without migration...");
            //     }
            // }

            // Configure the HTTP request pipeline.
            // if (app.Environment.IsDevelopment())
            // {
            //     app.UseSwagger();
            //     app.UseSwaggerUI();
            // }
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inflan Web Api");
                c.RoutePrefix = "swagger";
            });

            // Use CORS
            app.UseCors("AllowAll");


            // Skip HTTPS redirection in Docker development

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseStaticFiles();
            app.MapControllers();

            // Map SignalR hub with CORS policy that supports credentials
            app.MapHub<ChatHub>("/hubs/chat").RequireCors("SignalRPolicy");

            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            app.Urls.Add($"http://*:{port}");
            app.Run();
        }
    }
}
