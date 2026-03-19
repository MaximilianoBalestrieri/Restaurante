using System.Data;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// CONFIGURACIÓN PARA SQL SERVER (SOMEE)
builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection( // <--- Cambiamos MySqlConnection por SqlConnection
        builder.Configuration.GetConnectionString("MySqlConnection") 
    )
);

builder.Services.AddSession();

var app = builder.Build();

// COMENTA ESTO TEMPORALMENTE PARA VER EL ERROR REAL EN EL NAVEGADOR
// if (!app.Environment.IsDevelopment())
// {
    app.UseDeveloperExceptionPage(); // <--- Esto te dirá exactamente qué falla si sigue el Error 500
// }

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();