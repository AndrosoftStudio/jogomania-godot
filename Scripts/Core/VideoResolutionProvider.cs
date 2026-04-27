using Godot;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Jogomania.Core
{
    public static class VideoResolutionProvider
    {
        private static readonly Vector2I[] FallbackResolutions =
        {
            new Vector2I(3840, 2160),
            new Vector2I(2560, 1440),
            new Vector2I(1920, 1080),
            new Vector2I(1600, 900),
            new Vector2I(1366, 768),
            new Vector2I(1280, 720),
            new Vector2I(1024, 576),
            new Vector2I(854, 480),
            new Vector2I(640, 360)
        };

        public static List<Vector2I> GetAvailable16By9Resolutions()
        {
            var resolutions = OS.GetName() == "Windows"
                ? GetWindowsDisplayModes()
                : new List<Vector2I>();

            Vector2I screenSize = DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen());
            if (resolutions.Count > 0)
            {
                if (Is16By9(screenSize))
                    AddUniqueResolution(resolutions, screenSize);
            }
            else
            {
                AddFallbackResolutions(resolutions, screenSize);
            }
            resolutions.Sort((a, b) =>
            {
                int areaCompare = (b.X * b.Y).CompareTo(a.X * a.Y);
                return areaCompare != 0 ? areaCompare : b.X.CompareTo(a.X);
            });
            return resolutions;
        }

        private static void AddFallbackResolutions(List<Vector2I> resolutions, Vector2I screenSize)
        {
            foreach (Vector2I resolution in FallbackResolutions)
            {
                if (resolution.X <= screenSize.X && resolution.Y <= screenSize.Y)
                    AddUniqueResolution(resolutions, resolution);
            }

            if (Is16By9(screenSize))
                AddUniqueResolution(resolutions, screenSize);

            if (resolutions.Count == 0)
                AddUniqueResolution(resolutions, new Vector2I(1280, 720));
        }

        private static List<Vector2I> GetWindowsDisplayModes()
        {
            var resolutions = new List<Vector2I>();
            int modeIndex = 0;
            while (true)
            {
                var mode = new DevMode();
                mode.dmSize = (short)Marshal.SizeOf<DevMode>();
                if (!EnumDisplaySettings(null, modeIndex, ref mode))
                    break;

                var resolution = new Vector2I(mode.dmPelsWidth, mode.dmPelsHeight);
                if (resolution.X >= 640 && resolution.Y >= 360 && Is16By9(resolution))
                    AddUniqueResolution(resolutions, resolution);

                modeIndex++;
            }
            return resolutions;
        }

        private static bool Is16By9(Vector2I resolution)
        {
            if (resolution.X <= 0 || resolution.Y <= 0) return false;
            return Mathf.Abs((resolution.X / (float)resolution.Y) - (16f / 9f)) < 0.02f;
        }

        private static void AddUniqueResolution(List<Vector2I> resolutions, Vector2I resolution)
        {
            foreach (Vector2I existing in resolutions)
            {
                if (existing == resolution)
                    return;
            }
            resolutions.Add(resolution);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }
    }
}
