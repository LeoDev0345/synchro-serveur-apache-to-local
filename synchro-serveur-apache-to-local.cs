using System.Net.Http;
using System.Text.RegularExpressions;

/// <summary>
/// Programme de synchronisation d'un dossier distant (via HTTP) vers un dossier local.
/// Utilise un parsing HTML simple pour récupérer les fichiers listés dans un répertoire Apache.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Synchronisation complète ===");

        // URL du serveur distant (répertoire de fichiers exposé via HTTP)
        string serverUrl = "https://ip-du-serveur/mp3/";
        // Chemin local où les fichiers doivent être enregistrés
        string localPath = "/home/mp3/";

        // Crée le répertoire local s’il n’existe pas
        if (!Directory.Exists(localPath))
        {
            Directory.CreateDirectory(localPath);
        }

        // Ignore les erreurs de certificat SSL (utile pour HTTPS en local ou sans certificat valide)
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };

        using var httpClient = new HttpClient(handler);

        // Contient la liste des fichiers présents sur le serveur pour nettoyage plus tard
        var remoteFiles = new HashSet<string>();

        try
        {
            // Synchronise récursivement le dossier distant avec le dossier local
            await SynchronizeFolder(httpClient, serverUrl, localPath, remoteFiles, serverUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erreur de synchronisation : {ex.Message}");
        }

        // Supprime les fichiers locaux qui ne sont plus présents sur le serveur
        CleanLocalFiles(localPath, remoteFiles, localPath);

        Console.WriteLine("=== Synchronisation terminée ===");
    }

    /// <summary>
    /// Synchronise récursivement les fichiers et dossiers depuis une URL HTTP Apache.
    /// </summary>
    static async Task SynchronizeFolder(HttpClient httpClient, string url, string localPath, HashSet<string> remoteFiles, string baseUrl)
    {
        Console.WriteLine($"[SYNC] {url}");

        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        // Recherche tous les liens dans la page HTML (listing Apache)
        var matches = Regex.Matches(html, "<a href=\"([^\"]+)\"", RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var item = match.Groups[1].Value;

            // Filtres : ignorent les liens de tri, absolus, ou le parent "../"
            if (item.StartsWith("?") || item.StartsWith("/") || item == "../")
                continue;

            var itemUrl = $"{url}{item}";
            var decodedItem = Uri.UnescapeDataString(item.TrimEnd('/'));
            var itemLocalPath = Path.Combine(localPath, decodedItem);

            if (item.EndsWith("/")) // Cas d'un dossier
            {
                if (!Directory.Exists(itemLocalPath))
                    Directory.CreateDirectory(itemLocalPath);

                // Appel récursif pour synchroniser les sous-dossiers
                await SynchronizeFolder(httpClient, itemUrl, itemLocalPath, remoteFiles, baseUrl);
            }
            else // Cas d’un fichier
            {
                // Ajoute le chemin relatif du fichier à la liste des fichiers distants
                remoteFiles.Add(Path.GetRelativePath(baseUrl.Replace("https://", ""), itemUrl.Replace("https://", "")));

                // Télécharge le fichier uniquement s’il n’existe pas localement
                if (!File.Exists(itemLocalPath))
                {
                    try
                    {
                        Console.WriteLine($"[NEW] Téléchargement {itemUrl}");
                        var data = await httpClient.GetByteArrayAsync(itemUrl);
                        await File.WriteAllBytesAsync(itemLocalPath, data);
                        Console.WriteLine($"[OK] Fichier téléchargé : {itemLocalPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Impossible de télécharger {itemUrl} : {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[SKIP] Déjà présent : {itemLocalPath}");
                }
            }
        }
    }

    /// <summary>
    /// Supprime tous les fichiers locaux qui ne sont pas listés sur le serveur distant.
    /// Supprime aussi les dossiers vides.
    /// </summary>
    static void CleanLocalFiles(string basePath, HashSet<string> remoteFiles, string rootPath)
    {
        // Supprime les fichiers absents du serveur
        foreach (var path in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootPath, path);

            if (!remoteFiles.Contains(relativePath))
            {
                Console.WriteLine($"[DELETE] Suppression : {path}");
                File.Delete(path);
            }
        }

        // Supprime les répertoires devenus vides
        foreach (var dir in Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories))
        {
            if (Directory.GetFileSystemEntries(dir).Length == 0)
            {
                Console.WriteLine($"[DELETE] Suppression dossier vide : {dir}");
                Directory.Delete(dir);
            }
        }
    }
}
