using ADWebApp.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();

// Windows Authentication
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization();
builder.Services.AddScoped<ActiveDirectoryService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();