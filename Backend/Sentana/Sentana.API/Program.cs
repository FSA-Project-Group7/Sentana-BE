using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using OfficeOpenXml;
using Sentana.API.Models;
using Sentana.API.Repositories;
using Sentana.API.Services;
using Sentana.API.Services.SApartment;
using Sentana.API.Services.SBuilding;
using Sentana.API.Services.SEmail;
using Sentana.API.Services.SInfo;
using Sentana.API.Services.SInvoice;
using Sentana.API.Services.SNotification;
using Sentana.API.Services.SPayment;
using Sentana.API.Services.SRabbitMQ;
using Sentana.API.Services.SService;
using Sentana.API.Services.SStorage;
using Sentana.API.Services.STechnician;
using System.Text;
using Sentana.API.Services.SMaintenance;


namespace Sentana.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Env.Load();
            var builder = WebApplication.CreateBuilder(args);

            // cấu hình database
            builder.Services.AddDbContext<SentanaContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // cấu hình jwt
            var secretKey = builder.Configuration["JwtSettings:SecretKey"];
            var secretKeyBytes = Encoding.UTF8.GetBytes(secretKey!);

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                        ValidAudience = builder.Configuration["JwtSettings:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(secretKeyBytes)
                    };
                });
            builder.Services.AddSingleton<IMinioClient>(sp =>
            {
                return new MinioClient()
                    .WithEndpoint("localhost:9000")
                    .WithCredentials("minioadmin", "minioadmin")
                    .Build();
            });

            builder.Services.AddScoped<IMinioService, MinioService>();

            // Add services to the container.
            builder.Services.AddScoped<IRabbitMQProducer, RabbitMQProducer>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IServiceService, ServiceService>();
            builder.Services.AddScoped<IContractService, ContractService>();
            builder.Services.AddScoped<IContractRepository, ContractRepository>();
            builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
            builder.Services.AddScoped<IPaymentService, PaymentService>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ResidentService>();
            builder.Services.AddScoped<IBuildingService, BuildingService>();
            builder.Services.AddScoped<ITechnicianService, TechnicianService>();
            builder.Services.AddScoped<IInfoService, InfoService>();
            builder.Services.AddScoped<IApartmentService, Services.SApartment.ApartmentService>();
			builder.Services.AddCors(options =>
			{
				options.AddPolicy("AllowReactApp", policy =>
				{
					policy.WithOrigins("http://localhost:5173")
						  .AllowAnyHeader()
						  .AllowAnyMethod()
						  .AllowCredentials();
				});
			});
            builder.Services.AddScoped<IInvoiceService, InvoiceService>();
            builder.Services.AddScoped<IUtilityService, UtilityService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddHostedService<Sentana.API.BackgroundServices.NotificationCleanupService>();
            builder.Services.AddHostedService<Sentana.API.BackgroundServices.EmailConsumerService>();
            builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();

            builder.Services.AddControllers()
				.AddJsonOptions(options =>
				{
					options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
				});
			builder.Services.AddMemoryCache();
            // đăng ý gửi thư
            builder.Services.AddScoped<IEmailService, EmailService>();
            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Sentana.API", Version = "v1" });

                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "JWT Authentication",
                    Description = "cho token jwt vào đây",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Reference = new OpenApiReference
                    {
                        Id = "Bearer",
                        Type = ReferenceType.SecurityScheme
                    }
                };

                c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    { securityScheme, new string[] { } }
                });

                c.CustomSchemaIds(type => type.FullName);
            });

            var app = builder.Build();

            ExcelPackage.License.SetNonCommercialPersonal("Sentana");

			if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
			app.UseCors("AllowReactApp");

			// cho đăng nhập
			app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}