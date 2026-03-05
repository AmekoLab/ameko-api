using Autofac;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using FPTU.Capstone.AMKCollective.Infrastructure.DI;
using FPTU.Capstone.AMKCollective.Application.DI;
using FPTU.Capstone.AMKCollective.Application.Mappings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using FPTU.Capstone.AMKCollective.API.Workers;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Clear default claim mapping to use standard JWT claim names
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Register AutoMapper
        builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

        // Add DbContext to the container (for EF Core migrations)
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseMySql(connectionString, serverVersion, mySqlOptions =>
            {
                mySqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null
                );
            });
        });

        #region JWT Authentication
        // Configure JWT Authentication
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Required for .NET 8: use legacy JwtSecurityTokenHandler
                options.UseSecurityTokenValidators = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!)),
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["JwtSettings:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = "role",
                    NameClaimType = "nameid"
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddAuthorization();
        #endregion

        #region Configure Settings  
        // Configure EmailSettings
        builder.Services.Configure<FPTU.Capstone.AMKCollective.Application.DTOs.Settings.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
        builder.Services.Configure<FPTU.Capstone.AMKCollective.Application.DTOs.Settings.SecuritySettings>(builder.Configuration.GetSection("SecuritySettings"));
        #endregion

        #region CORS
        // Configure CORS
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000" };

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });
        #endregion

        #region Dependency Injection (Autofac DI Configuration)
        // Use Autofac as the service provider
        builder.Host.UseServiceProviderFactory(new Autofac.Extensions.DependencyInjection.AutofacServiceProviderFactory());
        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            // register infrastructure Autofac module
            containerBuilder.RegisterModule(new InfrastructureModule(builder.Configuration));
            // register application Autofac module
            containerBuilder.RegisterModule(new ApplicationModule());
        });

        // DI - wire Application interfaces to Infrastructure implementations
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();
        #endregion

        // DI registrations moved to Autofac module in Infrastructure (see InfrastructureModule)

        #region Swagger
        //Add Swagger document with Bearer to Authentication and Authorization
        builder.Services.AddSwaggerGen(opt =>
        {
            opt.EnableAnnotations(); // Enable Swagger annotations
            opt.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "ameko-api",
                Version = "v1",
                Description = "Api document for Ameko System"
            });

            opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Please enter token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "bearer"
            });

            opt.AddSecurityRequirement(new OpenApiSecurityRequirement
                        {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type=ReferenceType.SecurityScheme,
                                Id="Bearer"
                            }
                        },
                        new string []{}
                    }
                        });
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            opt.IncludeXmlComments(xmlPath);
        });
        #endregion

        // Register Background Workers
        builder.Services.AddHostedService<OrderCancellationTimeoutWorker>();
        builder.Services.AddHostedService<FundsReleaseWorker>();
        builder.Services.AddHostedService<AbandonedOrderCleanupWorker>();

        var app = builder.Build();

        // if (app.Environment.IsDevelopment())
        // {
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseRewriter(new RewriteOptions().AddRedirect("^$", "swagger"));
        // }

        //app.UseHttpsRedirection();
        app.UseCors("AllowFrontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        //app.MapHub<RealTimeHub>("/hub");

        app.Run();
    }
}
