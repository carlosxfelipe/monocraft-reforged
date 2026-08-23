using System.IO;
using Android.Content.Res;

namespace MonoCraft;

/// <summary>
/// Extrai os arquivos do APK (AssetManager) para o armazenamento interno do app.
/// Necessário porque PackLoader usa File/Directory APIs que não funcionam direto no APK.
/// </summary>
public static class AndroidAssetExtractor
{
    public static void Extract(AssetManager assets, string destRoot)
    {
        ExtractFolder(assets, "Content/packs", Path.Combine(destRoot, "Content", "packs"));
        ExtractFolder(assets, "Content/sounds", Path.Combine(destRoot, "Content", "sounds"));
    }

    private static void ExtractFolder(AssetManager assets, string assetPath, string destPath)
    {
        Directory.CreateDirectory(destPath);

        string[] entries = assets.List(assetPath);
        if (entries == null)
            return;

        foreach (var entry in entries)
        {
            string srcEntry = assetPath + "/" + entry;
            string destEntry = Path.Combine(destPath, entry);

            string[] children = assets.List(srcEntry);
            if (children != null && children.Length > 0)
            {
                ExtractFolder(assets, srcEntry, destEntry);
            }
            else
            {
                if (!File.Exists(destEntry))
                {
                    using var src = assets.Open(srcEntry);
                    using var dest = File.Create(destEntry);
                    src.CopyTo(dest);
                }
            }
        }
    }
}
