using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WoWHeightGenLib.Models;

namespace WoWHeightGenLib.Services
{
    /// <summary>
    /// Generates height maps from WoW terrain data.
    /// </summary>
    public class HeightMapGenerator
    {
        private readonly MapGenerationContext _context;

        /// <summary>
        /// Initializes a new instance of the HeightMapGenerator class.
        /// </summary>
        /// <param name="context">The map generation context.</param>
        public HeightMapGenerator(MapGenerationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Generates a height map image from the specified WDT file.
        /// </summary>
        /// <param name="wdtFileID">The WDT file ID.</param>
        /// <param name="clampToAboveSea">Whether to clamp heights to above sea level.</param>
        /// <param name="clampToBelowSea">Whether to clamp heights to below sea level.</param>
        public void Generate(int wdtFileID, bool clampToAboveSea = false, bool clampToBelowSea = false)
        {
            if (!Directory.Exists(_context.Config.OutputPath))
                Directory.CreateDirectory(_context.Config.OutputPath);

            if (_context.CascHandler == null) return;
            if (!_context.CascHandler.FileExists(wdtFileID)) return;

            using var outputImage = new Image<Rgba32>(_context.Config.HeightMapResolution, _context.Config.HeightMapResolution);
            using var wdtStream = _context.CascHandler.OpenFile(wdtFileID);
            var wdt = new Wdt(wdtStream);
            var adts = LoadAdtGrid(wdt, out float minAdt, out float maxAdt);

            if (clampToAboveSea)
                minAdt = 0;

            if (clampToBelowSea)
                maxAdt = 0;

            Console.WriteLine($"{wdtFileID} : Min Height {minAdt} Max Height {maxAdt}");

            RenderHeightMap(outputImage, adts, minAdt, maxAdt, clampToAboveSea, clampToBelowSea);

            var fileName = $"{wdtFileID}_height_{_context.Product}_{_context.VersionName}.png";
            var filePath = Path.Combine(_context.Config.OutputPath, fileName);
            outputImage.SaveAsPng(filePath);
        }

        private Adt?[,] LoadAdtGrid(Wdt wdt, out float minHeight, out float maxHeight)
        {
            var adts = new Adt?[_context.Config.MapSize, _context.Config.MapSize];
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;

            if (wdt.fileInfo == null)
                return adts;

            for (var y = 0; y < _context.Config.MapSize; y++)
            {
                for (var x = 0; x < _context.Config.MapSize; x++)
                {
                    var info = wdt.fileInfo[x, y];
                    int adtFileID = (int)info.rootADT;

                    if (_context.CascHandler!.FileExists(adtFileID))
                    {
                        using var adtStream = _context.CascHandler.OpenFile(adtFileID);
                        adts[x, y] = new Adt(adtStream);

                        if (minHeight > adts[x, y]!.minHeight)
                            minHeight = adts[x, y]!.minHeight;

                        if (maxHeight < adts[x, y]!.maxHeight)
                            maxHeight = adts[x, y]!.maxHeight;
                    }
                }
            }

            return adts;
        }

        private void RenderHeightMap(
            Image<Rgba32> outputImage,
            Adt?[,] adts,
            float minHeight,
            float maxHeight,
            bool clampToAboveSea,
            bool clampToBelowSea)
        {
            int chunkRes = _context.Config.HeightChunkResolution;

            for (int y = 0; y < _context.Config.MapSize; y++)
            {
                for (int x = 0; x < _context.Config.MapSize; x++)
                {
                    if (adts[x, y] == null)
                        continue;

                    byte[] pixelData = new byte[chunkRes * chunkRes * 3];
                    int idx = 0;

                    for (int x1 = 0; x1 < chunkRes; x1++)
                    {
                        for (int y1 = 0; y1 < chunkRes; y1++)
                        {
                            float value = adts[x, y]!.heightmap[x1, y1];

                            if (clampToAboveSea && value < 0)
                                value = 0;
                            if (clampToBelowSea && value > 0)
                                value = 0;

                            float normalized = (value - minHeight) / (maxHeight - minHeight);
                            byte byteValue = (byte)(normalized * 255f);

                            pixelData[idx] = byteValue;
                            pixelData[idx + 1] = byteValue;
                            pixelData[idx + 2] = byteValue;
                            idx += 3;
                        }
                    }

                    var img = Image.LoadPixelData<Rgb24>(pixelData, chunkRes, chunkRes);
                    outputImage.Mutate(o => o.DrawImage(img, new Point(chunkRes * x, chunkRes * y), 1f));
                }
            }
        }
    }
}
