namespace TallerMecanicoSantoDomingo.Services;

internal static class Paginacion
{
    public static int Ajustar(int solicitada, int total, int tamPagina)
    {
        var ultima = Math.Max(1, (int)((total + (long)tamPagina - 1) / tamPagina));
        return Math.Clamp(solicitada, 1, ultima);
    }
}
