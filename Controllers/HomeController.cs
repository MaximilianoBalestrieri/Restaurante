using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.AspNetCore.Http;
using System;

namespace RestauranteApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDbConnection _db;

        public HomeController(IDbConnection db)
        {
            _db = db;
        }

       public IActionResult Index()
{
    var idUsuarioStr = HttpContext.Session.GetString("IdUsuario");
    var rol = HttpContext.Session.GetString("Rol");

    // Si no hay ID de usuario, al login
    if (string.IsNullOrEmpty(idUsuarioStr))
    {
        return RedirectToAction("Index", "Login");
    }

    
            // 2. Valores por defecto (Para que la vista no explote si la DB falla)
            ViewBag.TotalVentasPropio = "0.00";
            ViewBag.MesasOcupadas = "0";
            ViewBag.ProductoEstrella = "Sin ventas aún";
            ViewBag.NombreUsuario = HttpContext.Session.GetString("NombreCompleto") ?? "Usuario";

            try
            {
                if (_db.State == ConnectionState.Closed) _db.Open();

                using (var cmd = _db.CreateCommand())
                {
                    // --- CONSULTA 1: VENTAS DEL CAJERO ACTUAL ---
                    // IMPORTANTE: Verifica si en tu base de datos la columna es IdUsuario o id_usuario
                    cmd.CommandText = @"SELECT IFNULL(SUM(Total), 0) 
                                        FROM comanda 
                                        WHERE Estado = 'CERRADA' 
                                        AND DATE(Fecha) = CURDATE() 
                                        AND IdUsuario = @idU";
                    
                    var pId = cmd.CreateParameter();
                    pId.ParameterName = "@idU";
                    pId.Value = idUsuarioStr; 
                    cmd.Parameters.Add(pId);
                    
                    var total = cmd.ExecuteScalar();
                    ViewBag.TotalVentasPropio = Convert.ToDecimal(total).ToString("N2");

                    // --- CONSULTA 2: MESAS OCUPADAS ---
                    cmd.Parameters.Clear(); 
                    cmd.CommandText = "SELECT COUNT(*) FROM Mesa WHERE Estado = 'OCUPADA'";
                    ViewBag.MesasOcupadas = cmd.ExecuteScalar()?.ToString() ?? "0";

                    // --- CONSULTA 3: PRODUCTO ESTRELLA ---
                    cmd.CommandText = @"SELECT p.Nombre 
                                        FROM detalle_comanda d 
                                        JOIN producto p ON d.IdProducto = p.IdProducto 
                                        JOIN comanda c ON d.IdComanda = c.IdComanda 
                                        WHERE DATE(c.Fecha) = CURDATE() 
                                        GROUP BY p.Nombre 
                                        ORDER BY SUM(d.Cantidad) DESC LIMIT 1";
                    
                    var estrella = cmd.ExecuteScalar();
                    if (estrella != null) ViewBag.ProductoEstrella = estrella.ToString();
                }
            }
            catch (Exception ex)
            {
                // Si llegas aquí, hay un error en el SQL (probablemente nombre de columna)
                ViewBag.ErrorSQL = "Error en base de datos: " + ex.Message;
            }
            finally
            {
                _db.Close();
            }

            return View();
        }
    }
}