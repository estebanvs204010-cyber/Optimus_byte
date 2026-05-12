using System.Diagnostics;
using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using VistaPrincipal.Models;
using VistaPrincipal.Data;

namespace VistaPrincipal.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly optimusDBContext _db;

    public HomeController(ILogger<HomeController> logger, optimusDBContext db)
    {
        _logger = logger;
        _db = db;
    }

    // GET: /Home
    public IActionResult Index()
    {
        ViewBag.UsuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
        ViewBag.UsuarioRol = HttpContext.Session.GetString("UsuarioRol");
        ViewBag.UsuarioId = HttpContext.Session.GetString("UsuarioId");
        return View();
    }

    // GET: /Home/Perfil
    public IActionResult Perfil()
    {
        var nombre = HttpContext.Session.GetString("UsuarioNombre");
        if (nombre == null) return RedirectToAction("Index", "Login");

        var idStr = HttpContext.Session.GetString("UsuarioId");
        var usuario = _db.Usuarios.Find(int.Parse(idStr!));

        ViewBag.UsuarioNombre = nombre;
        ViewBag.UsuarioRol = HttpContext.Session.GetString("UsuarioRol");
        ViewBag.UsuarioId = idStr;
        ViewBag.UsuarioCorreo = usuario?.Correo ?? "";
        ViewBag.UsuarioTelefono = usuario?.Telefono ?? "";

        return View();
    }

    // POST: /Home/ActualizarPerfil
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ActualizarPerfil(string NombreCompleto, string Correo,
                                          string Telefono,
                                          string? NuevaContrasena, string? ConfirmarContrasena)
    {
        var idStr = HttpContext.Session.GetString("UsuarioId");
        if (idStr == null) return RedirectToAction("Index", "Login");

        var usuario = _db.Usuarios.Find(int.Parse(idStr));
        if (usuario == null) return RedirectToAction("Index", "Login");

        // Cambiar contraseña solo si se escribió algo
        if (!string.IsNullOrWhiteSpace(NuevaContrasena))
        {
            if (NuevaContrasena.Length < 6)
            {
                TempData["Error"] = "La contraseña debe tener al menos 6 caracteres.";
                return RedirectToAction("Perfil");
            }
            if (NuevaContrasena != ConfirmarContrasena)
            {
                TempData["Error"] = "Las contraseñas no coinciden.";
                return RedirectToAction("Perfil");
            }
            usuario.ContrasenaHash = BC.HashPassword(NuevaContrasena);
        }

        // Actualizar los demás datos
        if (!string.IsNullOrWhiteSpace(NombreCompleto))
            usuario.NombreCompleto = NombreCompleto.Trim();

        if (!string.IsNullOrWhiteSpace(Correo))
            usuario.Correo = Correo.Trim();

        if (!string.IsNullOrWhiteSpace(Telefono))
            usuario.Telefono = Telefono.Trim();

        _db.SaveChanges();

        // Refrescar el nombre en sesión por si cambió
        HttpContext.Session.SetString("UsuarioNombre", usuario.NombreCompleto);

        TempData["Exito"] = "Perfil actualizado correctamente.";
        return RedirectToAction("Perfil");
    }

    // POST: /Home/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
   
        
}
