using Blazor_Server.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Syncfusion.Blazor;
using static Blazor_Server.Pages.Index;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
var config= builder.Configuration.AddUserSecrets<Program>().Build();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

string openAiAPIKEY = config["openaiapikey"];
string synfusionkey = config["synckey"];

var app = builder.Build();
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(synfusionkey);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
