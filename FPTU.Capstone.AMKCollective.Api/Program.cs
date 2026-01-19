using Autofac;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Infrastructure.Services;
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

var builder = WebApplication.CreateBuilder(args);

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
    options.UseMySql(connectionString, serverVersion));

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Configure EmailSettings
builder.Services.Configure<FPTU.Capstone.AMKCollective.Application.DTOs.Settings.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Use Autofac as the service provider
builder.Host.UseServiceProviderFactory(new Autofac.Extensions.DependencyInjection.AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<Autofac.ContainerBuilder>(containerBuilder =>
{
    // register infrastructure Autofac module
        containerBuilder.RegisterModule(new InfrastructureModule(builder.Configuration));
    // register application Autofac module
        containerBuilder.RegisterModule(new ApplicationModule());
});

// DI - wire Application interfaces to Infrastructure implementations
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, TokenService>();


// DI registrations moved to Autofac module in Infrastructure (see InfrastructureModule)

#region Swagger
//Add Swagger document with Bearer to Authentication and Authorization
builder.Services.AddSwaggerGen(opt =>
{
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
});
#endregion

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseRewriter(new RewriteOptions().AddRedirect("^$", "swagger"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
//app.MapHub<RealTimeHub>("/hub");

app.Run();
