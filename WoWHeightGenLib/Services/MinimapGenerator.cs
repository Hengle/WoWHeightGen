using SereniaBLPLib;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WoWHeightGenLib.Models;

namespace WoWHeightGenLib.Services
{
    /// <summary>
    /// Generates minimap images from WoW minimap texture data.
    /// </summary>
    public class MinimapGenerator
    {
        private readonly MapGenerationContext _context;

        /// <summary>
        /// Initializes a new instance of the MinimapGenerator class.
        /// </summary>
        /// <param name="context">The map generation context.</param>
        public MinimapGenerator(MapGenerationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Generates a minimap image from the specified WDT file.
        /// </summary>
        /// <param name="wdtFileID">The WDT file ID.</param>
        public void Generate(int wdtFileID)
        {
            if (!Directory.Exists(_context.Config.OutputPath))
                Directory.CreateDirectory(_context.Config.OutputPath);

            if (_context.CascHandler == null) return;
            if (!_context.CascHandler.FileExists(wdtFileID)) return;

            using var wdtStream = _context.CascHandler.OpenFile(wdtFileID);
            var wdt = new Wdt(wdtStream);

            if (wdt.fileInfo == null) return;

            var resolution = GetMinimapResolution(wdt);
            using var outputImage = new Image<Rgba32>(
                resolution * _context.Config.MapSize,
                resolution * _context.Config.MapSize);

            RenderMinimap(outputImage, wdt, resolution);

            var fileName = $"{wdtFileID}_minimap_{_context.Product}_{_context.VersionName}.png";
            var filePath = Path.Combine(_context.Config.OutputPath, fileName);
            outputImage.SaveAsPng(filePath);
        }

        private int GetMinimapResolution(Wdt wdt)
        {
            if (_context.CascHandler == null) return 0;
            if (wdt.fileInfo == null) return 0;

            for (var y = 0; y < _context.Config.MapSize; y++)
            {
                for (var x = 0; x < _context.Config.MapSize; x++)
                {
                    var info = wdt.fileInfo[x, y];
                    int minimapFileID = (int)info.minimapTexture;

                    if (_context.CascHandler.FileExists(minimapFileID))
                    {
                        using var blpStream = _context.CascHandler.OpenFile(minimapFileID);
                        var blp = new BlpFile(blpStream);
                        var img = blp.GetImage(0);
                        return img?.Width ?? 256;
                    }
                }
            }

            return 256; // Default fallback
        }

        private void RenderMinimap(Image<Rgba32> outputImage, Wdt wdt, int resolution)
        {
            if (wdt.fileInfo == null) return;

            for (var y = 0; y < _context.Config.MapSize; y++)
            {
                for (var x = 0; x < _context.Config.MapSize; x++)
                {
                    var info = wdt.fileInfo[x, y];
                    int minimapFileID = (int)info.minimapTexture;

                    if (_context.CascHandler!.FileExists(minimapFileID))
                    {
                        using var blpStream = _context.CascHandler.OpenFile(minimapFileID);
                        var blp = new BlpFile(blpStream);
                        var img = blp.GetImage(0);

                        if (img != null)
                        {
                            outputImage.Mutate(o => o.DrawImage(
                                img,
                                new Point(resolution * x, resolution * y),
                                1f));
                        }
                    }
                }
            }
        }
    }
}
