using Microsoft.AspNetCore.Mvc;
using System.Data;
using RestauranteApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient; // Asegúrate de tener este usando

namespace RestauranteApp.Controllers
{
    public class LoginController : Controller
    {
        private readonly IDbConnection _db;

        public LoginController(IDbConnection db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        public IActionResult Index(string usuario, string password)
        {
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Debe completar usuario y contraseña";
                return View();
            }

            Usuario? user = null;

            // CAMBIO: En SQL Server se usa "SELECT TOP 1" en lugar de "LIMIT 1"
            string sql = @"
                SELECT TOP 1 *
                FROM Usuario
                WHERE NombreUsuario = @usuario
                  AND Activo = 1;
            ";

            try 
            {
                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = sql;

                    var pUsuario = cmd.CreateParameter();
                    pUsuario.ParameterName = "@usuario";
                    pUsuario.Value = usuario;
                    cmd.Parameters.Add(pUsuario);

                    if (_db.State == ConnectionState.Closed) _db.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            user = new Usuario
                            {
                                IdUsuario = Convert.ToInt32(reader["IdUsuario"]),
                                NombreUsuario = reader["NombreUsuario"].ToString()!,
                                NombreCompleto = reader["NombreCompleto"].ToString()!,
                                PasswordHash = reader["PasswordHash"].ToString()!,
                                Rol = reader["Rol"].ToString()!,
                                Activo = Convert.ToBoolean(reader["Activo"])
                            };
                        }
                    }
                }
            }
            finally 
            {
                _db.Close(); // Nos aseguramos de cerrar la conexión siempre
            }

            if (user == null || user.PasswordHash != password)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos";
                return View();
            }

            // 🔐 Sesión
            HttpContext.Session.SetString("IdUsuario", user.IdUsuario.ToString());
            HttpContext.Session.SetString("Usuario", user.NombreUsuario);
            HttpContext.Session.SetString("NombreCompleto", user.NombreCompleto);
            HttpContext.Session.SetString("Rol", user.Rol);

            // 🚦 Redirección por rol
            return user.Rol switch
            {
                "MOZO" => RedirectToAction("Index", "Mesas"),
                "COCINA" => RedirectToAction("Index", "Cocina"),
                "CAJA" => RedirectToAction("Index", "Home"),
                _ => RedirectToAction("Index", "Home") // Agregado por seguridad
            };
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}