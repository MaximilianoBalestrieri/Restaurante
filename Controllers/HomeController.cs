using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

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
            if (string.IsNullOrEmpty(idUsuarioStr)) return RedirectToAction("Index", "Login");

            // 1. Valores por defecto
            ViewBag.TotalEfectivo = 0;
            ViewBag.TotalTransf = 0;
            ViewBag.TotalTarjeta = 0;
            ViewBag.CantidadVentas = 0;
            ViewBag.MesasOcupadas = 0;
            ViewBag.ProductoEstrella = "Sin ventas aún";
            ViewBag.NombreUsuario = HttpContext.Session.GetString("Nombre") ?? "Usuario";
            ViewBag.CajaAbierta = false;
            ViewBag.MontoInicial = 0;

            try
            {
                if (_db.State == ConnectionState.Closed) _db.Open();

                DateTime fechaAperturaActual = DateTime.MinValue;

                // --- VERIFICACIÓN DE APERTURA DE CAJA ACTIVA ---
                using (var cmdC = _db.CreateCommand())
                {
                    // Buscamos la última apertura que esté ABIERTA
                    cmdC.CommandText = "SELECT TOP 1 MontoInicial, FechaApertura FROM AperturasCaja WHERE Estado = 'ABIERTA' ORDER BY IdApertura DESC";

                    using (var reader = cmdC.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ViewBag.CajaAbierta = true;
                            ViewBag.MontoInicial = Convert.ToDecimal(reader["MontoInicial"]);
                            fechaAperturaActual = Convert.ToDateTime(reader["FechaApertura"]);
                        }
                    }
                }

                // 2. Si la caja está abierta, cargamos estadísticas filtrando DESDE LA APERTURA
                if (ViewBag.CajaAbierta)
                {
                    using (var cmd = _db.CreateCommand())
                    {
                        // FILTRO CLAVE: Fecha >= @fechaApe (Limpia los montos de turnos anteriores)
                        cmd.CommandText = @"
                            SELECT 
                                ISNULL(SUM(CASE WHEN MetodoPago = 'EFECTIVO' THEN Total ELSE 0 END), 0) as Efectivo,
                                ISNULL(SUM(CASE WHEN MetodoPago = 'TRANSFERENCIA' THEN Total ELSE 0 END), 0) as Transf,
                                ISNULL(SUM(CASE WHEN MetodoPago = 'TARJETA' THEN Total ELSE 0 END), 0) as Tarjeta,
                                COUNT(IdVenta) as Cantidad
                            FROM Ventas 
                            WHERE Fecha >= @fechaApe";

                        var pFA = cmd.CreateParameter(); pFA.ParameterName = "@fechaApe"; pFA.Value = fechaAperturaActual;
                        cmd.Parameters.Add(pFA);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ViewBag.TotalEfectivo = Convert.ToDecimal(reader["Efectivo"]);
                                ViewBag.TotalTransf = Convert.ToDecimal(reader["Transf"]);
                                ViewBag.TotalTarjeta = Convert.ToDecimal(reader["Tarjeta"]);
                                ViewBag.CantidadVentas = reader["Cantidad"];
                            }
                        }

                        // --- MESAS OCUPADAS (Estado actual del salón) ---
                        cmd.Parameters.Clear();
                        cmd.CommandText = "SELECT COUNT(*) FROM Mesa WHERE Estado = 'OCUPADA'";
                        ViewBag.MesasOcupadas = cmd.ExecuteScalar()?.ToString() ?? "0";

                        // --- PRODUCTO ESTRELLA DEL TURNO ---
                        cmd.CommandText = @"
                            SELECT TOP 1 p.Nombre 
                            FROM detalle_comanda d 
                            JOIN producto p ON d.IdProducto = p.IdProducto 
                            JOIN comanda c ON d.IdComanda = c.IdComanda 
                            WHERE c.Fecha >= @fechaApe 
                            GROUP BY p.Nombre 
                            ORDER BY SUM(d.Cantidad) DESC";

                        var pFA2 = cmd.CreateParameter(); pFA2.ParameterName = "@fechaApe"; pFA2.Value = fechaAperturaActual;
                        cmd.Parameters.Add(pFA2);

                        var estrella = cmd.ExecuteScalar();
                        if (estrella != null) ViewBag.ProductoEstrella = estrella.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorSQL = "Error: " + ex.Message;
            }
            finally { _db.Close(); }

            return View();
        }

        [HttpPost]
        public IActionResult ProcesarApertura(decimal monto)
        {
            var idUsuarioStr = HttpContext.Session.GetString("IdUsuario");
            if (string.IsNullOrEmpty(idUsuarioStr)) return Json(new { success = false, message = "Sesión expirada." });

            try
            {
                if (_db.State == ConnectionState.Closed) _db.Open();

                var zonaAr = TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");
                var ahoraAr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaAr);

                using (var cmd = _db.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO AperturasCaja (FechaApertura, MontoInicial, IdUsuario, Estado) VALUES (@fecha, @monto, @idU, 'ABIERTA')";

                    var pF = cmd.CreateParameter(); pF.ParameterName = "@fecha"; pF.Value = ahoraAr;
                    var pM = cmd.CreateParameter(); pM.ParameterName = "@monto"; pM.Value = monto;
                    var pU = cmd.CreateParameter(); pU.ParameterName = "@idU"; pU.Value = Convert.ToInt32(idUsuarioStr);

                    cmd.Parameters.Add(pF); cmd.Parameters.Add(pM); cmd.Parameters.Add(pU);
                    cmd.ExecuteNonQuery();
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
            finally { _db.Close(); }
        }

        [HttpPost]
        public IActionResult ProcesarCierre()
        {
            var idUsuarioStr = HttpContext.Session.GetString("IdUsuario");
            if (string.IsNullOrEmpty(idUsuarioStr)) return Json(new { success = false, message = "Sesión expirada." });

            try
            {
                if (_db.State == ConnectionState.Closed) _db.Open();

                // 1. Primero definimos la zona horaria
                var zonaAr = TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");

                // 2. Luego creamos la variable 'ahoraAr' usando 'zonaAr' como parámetro
                var ahoraAr = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaAr);
                using (var transaction = _db.BeginTransaction())
                {
                    try
                    {
                        decimal montoApe = 0;
                        DateTime fechaApe = DateTime.MinValue;

                        // 1. Obtener la apertura abierta
                        using (var cmdA = _db.CreateCommand())
                        {
                            cmdA.Transaction = transaction;
                            cmdA.CommandText = "SELECT TOP 1 MontoInicial, FechaApertura FROM AperturasCaja WHERE Estado = 'ABIERTA' ORDER BY IdApertura DESC";
                            using (var reader = cmdA.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    montoApe = Convert.ToDecimal(reader["MontoInicial"]);
                                    fechaApe = Convert.ToDateTime(reader["FechaApertura"]);
                                }
                            }
                        }

                        // 2. Insertar Cierre filtrando por el inicio de esta apertura
                        int idCierre = 0;
                        using (var cmd = _db.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                INSERT INTO CierresCaja (FechaCierre, TotalEfectivo, TotalTransferencia, TotalTarjeta, TotalGeneral, CantidadVentas, IdUsuario, MontoApertura)
                                OUTPUT INSERTED.IdCierre
                                SELECT @ahora, 
                                       ISNULL(SUM(CASE WHEN MetodoPago = 'EFECTIVO' THEN Total ELSE 0 END), 0),
                                       ISNULL(SUM(CASE WHEN MetodoPago = 'TRANSFERENCIA' THEN Total ELSE 0 END), 0),
                                       ISNULL(SUM(CASE WHEN MetodoPago = 'TARJETA' THEN Total ELSE 0 END), 0),
                                       ISNULL(SUM(Total), 0),
                                       COUNT(IdVenta),
                                       @idU,
                                       @mApe
                                FROM Ventas 
                                WHERE Fecha >= @fApe";

                            var p1 = cmd.CreateParameter(); p1.ParameterName = "@ahora"; p1.Value = ahoraAr;
                            var p2 = cmd.CreateParameter(); p2.ParameterName = "@idU"; p2.Value = Convert.ToInt32(idUsuarioStr);
                            var p3 = cmd.CreateParameter(); p3.ParameterName = "@mApe"; p3.Value = montoApe;
                            var p4 = cmd.CreateParameter(); p4.ParameterName = "@fApe"; p4.Value = fechaApe;

                            cmd.Parameters.Add(p1); cmd.Parameters.Add(p2); cmd.Parameters.Add(p3); cmd.Parameters.Add(p4);
                            idCierre = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // 3. Marcar apertura como CERRADA
                        using (var cmdUpd = _db.CreateCommand())
                        {
                            cmdUpd.Transaction = transaction;
                            cmdUpd.CommandText = "UPDATE AperturasCaja SET Estado = 'CERRADA' WHERE Estado = 'ABIERTA'";
                            cmdUpd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return Json(new { success = true, idCierre = idCierre });
                    }
                    catch (Exception ex) { transaction.Rollback(); return Json(new { success = false, message = ex.Message }); }
                }
            }
            finally { _db.Close(); }
        }

        public IActionResult ImprimirCierre(int idCierre)
{
    if (_db.State == ConnectionState.Closed) _db.Open();
    dynamic? cierre = null;

    try
    {
        using (var cmd = _db.CreateCommand())
        {
            // Corregimos 'Usuario' y 'NombreUsuario'
            cmd.CommandText = @"SELECT c.*, u.NombreUsuario 
                                FROM CierresCaja c 
                                JOIN Usuario u ON c.IdUsuario = u.IdUsuario 
                                WHERE c.IdCierre = @id";
            
            var p = cmd.CreateParameter(); 
            p.ParameterName = "@id"; 
            p.Value = idCierre;
            cmd.Parameters.Add(p);

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    cierre = new
                    {
                        Fecha = Convert.ToDateTime(reader["FechaCierre"]),
                        Efectivo = Convert.ToDecimal(reader["TotalEfectivo"]),
                        Transf = Convert.ToDecimal(reader["TotalTransferencia"]),
                        Tarjeta = Convert.ToDecimal(reader["TotalTarjeta"]),
                        Total = Convert.ToDecimal(reader["TotalGeneral"]),
                        Cant = reader["CantidadVentas"].ToString(),
                        // Aquí usamos el nombre exacto de tu base de datos
                        Cajero = reader["NombreUsuario"].ToString(), 
                        Apertura = Convert.ToDecimal(reader["MontoApertura"])
                    };
                }
            }
        }
    }
    finally { _db.Close(); }

    if (cierre == null) return Content("Cierre no encontrado");
    
    return View(cierre);
}


    }
}