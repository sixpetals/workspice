using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Workspice.Application.Contracts;
using Workspice.Domain.Models;
using Workspice.Infrastructure.Persistence;

namespace Workspice.Infrastructure.Wallpaper;

public sealed class WallpaperService(WorkspicePathOptions pathOptions) : IWallpaperService
{
    public Task<string?> ApplyAsync(ProfileDefinition profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(pathOptions.WallpapersDirectory);

        var imagePath = ResolveWallpaperPath(profile);
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            SystemParametersInfo(SpiSetDesktopWallpaper, 0, imagePath, SpifUpdateIniFile | SpifSendWinIniChange);
        }

        return Task.FromResult(imagePath);
    }

    private string? ResolveWallpaperPath(ProfileDefinition profile)
    {
        if (profile.WallpaperMode == WallpaperMode.CustomImage && !string.IsNullOrWhiteSpace(profile.WallpaperPath) && File.Exists(profile.WallpaperPath))
        {
            return profile.WallpaperPath;
        }

        var generatedPath = Path.Combine(pathOptions.WallpapersDirectory, $"{profile.Id}.png");
        if (!File.Exists(generatedPath))
        {
            using var bitmap = new Bitmap(1920, 1080);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.FromArgb(24, 64, 96));
            using var font = new Font("Yu Gothic UI", 52, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(Color.WhiteSmoke);
            using var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            var rect = new RectangleF(0, 0, bitmap.Width, bitmap.Height);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            graphics.DrawString(profile.Name, font, shadow, new RectangleF(4, 4, rect.Width, rect.Height), format);
            graphics.DrawString(profile.Name, font, brush, rect, format);
            bitmap.Save(generatedPath, ImageFormat.Png);
        }

        return generatedPath;
    }

    private const int SpiSetDesktopWallpaper = 20;
    private const int SpifUpdateIniFile = 0x01;
    private const int SpifSendWinIniChange = 0x02;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, string pvParam, int fWinIni);
}
