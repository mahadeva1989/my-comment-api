using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using my_comment_api.Extensions;
using my_comment_api.Middleware;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();


var builder = WebApplication.CreateBuilder(args);

builder.AddServices();

var app = builder.Build();

app.Configure();
app.Run();

