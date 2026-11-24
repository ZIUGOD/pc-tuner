namespace PcTuner.Core;

/// <summary>Pasta grande que só o usuário pode decidir apagar. O app só mede e orienta.</summary>
public sealed record DiskSpot(string Title, string Path, string Advice);

public static class DiskUsage
{
    public static IReadOnlyList<DiskSpot> Spots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        DiskSpot[] all =
        [
            new("Docker", System.IO.Path.Combine(local, "Docker"),
                "Imagens, containers e volumes. Com o Docker aberto, rode \"docker system prune -a\" para apagar o que não está em uso."),
            new("Emuladores Android", System.IO.Path.Combine(home, ".android", "avd"),
                "Android Studio > Device Manager: apague os emuladores que você não usa."),
            new("Modelos do Ollama", System.IO.Path.Combine(home, ".ollama"),
                "Rode \"ollama list\" e apague os que não usa com \"ollama rm nome\". Dá para guardar os modelos em outro disco com a variável OLLAMA_MODELS."),
            new("Spotify", System.IO.Path.Combine(local, "Spotify"),
                "Músicas baixadas e cache. No Spotify: Configurações > Armazenamento."),
            new("Downloads", System.IO.Path.Combine(home, "Downloads"),
                "Apague instaladores antigos ou mova a pasta para outro disco."),
            new("Android SDK", System.IO.Path.Combine(local, "Android", "Sdk"),
                "Android Studio > SDK Manager: remova versões e system images antigas."),
            new("Linux (WSL)", System.IO.Path.Combine(local, "wsl"),
                "Disco virtual das distribuições WSL. Limpe por dentro do Linux."),
            new("Cache do Gradle", System.IO.Path.Combine(home, ".gradle", "caches"),
                "Pode apagar com o Android Studio fechado. O próximo build demora mais porque baixa tudo de novo."),
            new("Cache do npm", System.IO.Path.Combine(local, "npm-cache"),
                "Rode \"npm cache clean --force\"."),
        ];

        return all.Where(s => Directory.Exists(s.Path)).ToList();
    }
}
