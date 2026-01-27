using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WoWHeightGenLib.Models;

namespace WoWHeightGenLib.Services
{
    /// <summary>
    /// Generates area ID maps from WoW terrain data with color-coded areas.
    /// </summary>
    public class AreaMapGenerator
    {
        private readonly MapGenerationContext _context;

        /// <summary>
        /// Initializes a new instance of the AreaMapGenerator class.
        /// </summary>
        /// <param name="context">The map generation context.</param>
        public AreaMapGenerator(MapGenerationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Generates an area ID map image from the specified WDT file.
        /// </summary>
        /// <param name="wdtFileID">The WDT file ID.</param>
        public void Generate(int wdtFileID)
        {
            if (!Directory.Exists(_context.Config.OutputPath))
                Directory.CreateDirectory(_context.Config.OutputPath);

            if (!_context.FileExists((uint)wdtFileID)) return;

            using var outputImage = new Image<Rgba32>(
                _context.Config.HeightMapResolution,
                _context.Config.HeightMapResolution);

            using var wdtStream = _context.OpenFile((uint)wdtFileID);
            var wdt = new Wdt(wdtStream);
            var adts = LoadAdtGrid(wdt);

            RenderAreaMap(outputImage, adts);

            var fileName = $"{wdtFileID}_area_{_context.Product}_{_context.VersionName}.png";
            var filePath = Path.Combine(_context.Config.OutputPath, fileName);
            outputImage.SaveAsPng(filePath);
        }

        private Adt?[,] LoadAdtGrid(Wdt wdt)
        {
            var adts = new Adt?[_context.Config.MapSize, _context.Config.MapSize];

            if (wdt.fileInfo == null)
                return adts;

            for (var y = 0; y < _context.Config.MapSize; y++)
            {
                for (var x = 0; x < _context.Config.MapSize; x++)
                {
                    var info = wdt.fileInfo[x, y];
                    uint adtFileID = info.rootADT;

                    if (_context.FileExists(adtFileID))
                    {
                        using var adtStream = _context.OpenFile(adtFileID);
                        adts[x, y] = new Adt(adtStream);
                    }
                }
            }

            return adts;
        }

        private void RenderAreaMap(Image<Rgba32> outputImage, Adt?[,] adts)
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
                            uint areaId = adts[x, y]!.areaIDmap[x1, y1];
                            var color = _context.GetOrCreateAreaColor(areaId);

                            pixelData[idx] = color.Item1;
                            pixelData[idx + 1] = color.Item2;
                            pixelData[idx + 2] = color.Item3;
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
