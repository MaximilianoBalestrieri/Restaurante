using Microsoft.AspNetCore.Mvc;
using System.Data;
using RestauranteApp.Models;
using System.Collections.Generic;

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
ViewBag.Rol=rol;
            var pedidosPendientes = new List<dynamic>(); // Usamos dynamic para simplificar el JOIN rápido

            if (_db.State == ConnectionState.Closed) _db.Open();

            using (var cmd = _db.CreateCommand())
            {
                // Traemos solo Comidas (1) y Postres (3) que estén "PARA_PREPARAR"
                cmd.CommandText = @"
                    SELECT m.Numero as Mesa, p.Nombre as Producto, d.Cantidad, d.IdDetalle
                    FROM detalle_comanda d
                    JOIN producto p ON d.IdProducto = p.IdProducto
                    JOIN comanda c ON d.IdComanda = c.IdComanda
                    JOIN mesa m ON c.IdMesa = m.IdMesa
                    WHERE d.EstadoItem = 'PARA_PREPARAR' 
                    AND p.IdCategoria IN (1, 3)
                    ORDER BY c.Fecha ASC";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        pedidosPendientes.Add(new {
                            IdDetalle = reader["IdDetalle"],
                            Mesa = reader["Mesa"],
                            Producto = reader["Producto"],
                            Cantidad = reader["Cantidad"]
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