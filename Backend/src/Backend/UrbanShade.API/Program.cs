using UrbanShade.API.Filters;
using UrbanShade.API.Authentication;
using UrbanShade.Application;
using Microsoft.AspNetCore.Mvc;
using UrbanShade.Communication.Responses;
using UrbanShade.Communication.Enums;
using UrbanShade.Infraestructure;
using UrbanShade.Infraestructure.Migrations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

builder.Services.AddMvc(options => { options.Filters.Add<ExceptionFilter>(); });

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddBearerAuthentication(builder.Configuration);

builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
    new BadRequestObjectResult(new PayloadResponse
    {
        Status = nameof(ResponseStatus.Error),
        Messages = [.. context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)]
    }));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await ExecuteMigrations();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

async Task ExecuteMigrations()
{

    await using var scope = app.Services.CreateAsyncScope();

    await DatabaseMigration.ExecuteMigrations(scope.ServiceProvider);

}

public partial class Program { }
