using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Debe ingresar su usuario o correo electrónico.")]
    [Display(Name = "Usuario o correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe ingresar su contraseña.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Recordarme")]
    public bool Recordarme { get; set; }

    public string? UrlRetorno { get; set; }
}

/// <summary>
/// Formulario de creación del primer administrador. Solo se muestra cuando
/// todavía no existe ningún usuario en el sistema (uso único).
/// </summary>
public class ConfiguracionInicialViewModel
{
    [Required(ErrorMessage = "Debe ingresar el correo electrónico del administrador.")]
    [EmailAddress(ErrorMessage = "Debe ingresar un correo electrónico válido.")]
    [Display(Name = "Correo electrónico del administrador")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe ingresar una contraseña.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe confirmar la contraseña.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [Display(Name = "Confirmar contraseña")]
    public string ConfirmarPassword { get; set; } = string.Empty;
}

public class ErrorViewModel
{
    public string? Mensaje { get; set; }
    public string? Detalle { get; set; }
}
