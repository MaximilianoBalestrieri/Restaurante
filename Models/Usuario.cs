using System;

namespace RestauranteApp.Models
{
    public class Usuario
    {
        public int IdUsuario { get; set; }

        public string NombreUsuario { get; set; } = string.Empty;

        public string NombreCompleto { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Rol { get; set; } = string.Empty;

        public string? Telefono { get; set; }

        public string? Domicilio { get; set; }

        public string? Localidad { get; set; }

        public string? Email { get; set; }

        public bool Activo { get; set; }

        public DateTime FechaAlta { get; set; }
    }
}
