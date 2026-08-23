namespace MonoCraft;

public static class GameSettings
{
    // A semente do mundo (temporariamente fixa para facilitar o desenvolvimento)
    // No futuro, isso pode ser alterado para gerar mundos aleatórios.
    public static int Seed { get; set; } = 1337;

    // Configurações do ciclo de Dia e Noite
    public static bool EnableDayNightCycle { get; set; } = true;
    public static float DayNightDurationMinutes { get; set; } = 20.0f; // Padrão do Minecraft
}
