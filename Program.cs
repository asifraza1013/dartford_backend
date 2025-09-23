using inflan_api.MyDBContext;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using inflan_api.Interfaces;
using inflan_api.Repositories;
using inflan_api.Services;
using inflan_api.Models;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.Mvc;

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

            builder.Services.AddControllers()
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
            // builder.Services.AddTransient<ITransactionRepository, TransactionRepository>();
            builder.Services.AddTransient<ICampaignRepository, CampaignRepository>();
            builder.Services.AddTransient<ICampaignService, CampaignService>();
            
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
            });


            var app = builder.Build();

            // Run database migrations on startup
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<InflanDBContext>();
                context.Database.Migrate();
            }

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

            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            app.Urls.Add($"http://*:{port}");
            app.Run();
        }
    }
}
