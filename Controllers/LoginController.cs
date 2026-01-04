using Microsoft.AspNetCore.Mvc;
using System.Data;
using RestauranteApp.Models;
using Microsoft.AspNetCore.Http;

namespace RestauranteApp.Controllers
{
    public class LoginController : Controller
    {
        private readonly IDbConnection _db;

        public LoginController(IDbConnection db)
        {
            _db = db;
        }

        // GET: /Login
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Login
        [HttpPost]
        public IActionResult Index(string usuario, string password)
        {
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Debe completar usuario y contraseña";
                return View();
            }

            Usuario? user = null;

            string sql = @"
                SELECT *
                FROM Usuario
                WHERE NombreUsuario = @usuario
                  AND Activo = 1
                LIMIT 1;
            ";

            using (var cmd = _db.CreateCommand())
            {
                cmd.CommandText = sql;

                var pUsuario = cmd.CreateParameter();
                pUsuario.ParameterName = "@usuario";
                pUsuario.Value = usuario;
                cmd.Parameters.Add(pUsuario);

                _db.Open();
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
                _db.Close();
            }

            if (user == null || user.PasswordHash != password)
            {
                ViewBag.Error = "Usuario o contraseña incorrectos";
                return View();
            }

            // 🔐 Sesión
            HttpContext.Session.SetString("IdUsuario", user.IdUsuario.ToString()); // <-- AGREGA ESTA LÍNEA
            HttpContext.Session.SetString("Usuario", user.NombreUsuario);
            HttpContext.Session.SetString("NombreCompleto", user.NombreCompleto);
            HttpContext.Session.SetString("Rol", user.Rol);

            // 🚦 Redirección por rol - va a la pagina segun su rol --------------
            return user.Rol switch
            {
                "MOZO" => RedirectToAction("Index", "Mesas"),
                "COCINA" => RedirectToAction("Index", "Cocina"),
                "CAJA" => RedirectToAction("Index", "Caja"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}
