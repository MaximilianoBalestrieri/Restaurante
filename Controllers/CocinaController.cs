using Microsoft.AspNetCore.Mvc;
using System.Data;
using RestauranteApp.Models;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace RestauranteApp.Controllers
{
    public class CocinaController : Controller
    {
        private readonly IDbConnection _db;

        public CocinaController(IDbConnection db)
        {
            _db = db;
        }

       public IActionResult Index()
{
    var rol = HttpContext.Session.GetString("Rol");
    var nombre = HttpContext.Session.GetString("NombreCompleto");
    ViewBag.NombreUsuario = nombre;
    ViewBag.Rol = rol;

    var pedidosPendientes = new List<dynamic>();

    if (_db.State == ConnectionState.Closed) _db.Open();

    using (var cmd = _db.CreateCommand())
    {
        // CAMBIO 1: Seleccionamos d.HoraPedido en lugar de c.Fecha
        // CAMBIO 2: Ordenamos por d.HoraPedido para respetar el orden de llegada real
        cmd.CommandText = @"
            SELECT m.Numero as Mesa, p.Nombre as Producto, d.Cantidad, d.IdDetalle, d.HoraPedido
            FROM detalle_comanda d
            JOIN producto p ON d.IdProducto = p.IdProducto
            JOIN comanda c ON d.IdComanda = c.IdComanda
            JOIN mesa m ON c.IdMesa = m.IdMesa
            WHERE d.EstadoItem = 'PARA_PREPARAR' 
            AND p.IdCategoria IN (1, 3)
            ORDER BY d.HoraPedido ASC";

        using (var reader = cmd.ExecuteReader())
        {
            TimeZoneInfo zonaArgentina = TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");

            while (reader.Read())
            {
                // Si la columna HoraPedido es NULL por registros viejos, usamos DateTime.Now como fallback
                DateTime fechaBD = reader["HoraPedido"] != DBNull.Value 
                                   ? Convert.ToDateTime(reader["HoraPedido"]) 
                                   : DateTime.Now;

                // Si tu servidor ya guarda la hora en Argentina (como configuramos antes), 
                // ya no hace falta la conversión compleja, pero la mantenemos por seguridad:
                DateTime fechaFinal = fechaBD;
                
                // Si el servidor es Somee y guarda en UTC, habilitá la conversión:
                // fechaFinal = TimeZoneInfo.ConvertTimeFromUtc(fechaBD.ToUniversalTime(), zonaArgentina);

                pedidosPendientes.Add(new
                {
                    IdDetalle = reader["IdDetalle"],
                    Mesa = reader["Mesa"],
                    Producto = reader["Producto"],
                    Cantidad = reader["Cantidad"],
                    // IMPORTANTE: El nombre de la propiedad aquí debe coincidir con el de la Vista
                    HoraPedido = fechaFinal 
                });
            }
        }
    }
    _db.Close();

    return View(pedidosPendientes);
}

        [HttpPost]
        public IActionResult MarcarListo(int idDetalle)
        {
            if (_db.State == ConnectionState.Closed) _db.Open();
            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = "UPDATE detalle_comanda SET EstadoItem = 'LISTO' WHERE IdDetalle = @id";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = idDetalle;
                cmd.Parameters.Add(p);
                cmd.ExecuteNonQuery();
            }
            _db.Close();
            return Json(new { success = true });
        }
    }
}