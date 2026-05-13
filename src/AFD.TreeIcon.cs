// Auto Forestry Designations
// Copyright (c) 2026 Kayser
// Licensed under the MIT License.
//
// Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
// related trademarks, code, and assets belong to MaFi Games. This repository is
// intended to contain only original mod code/configuration; if MaFi Games material
// is included by mistake, I intend to correct it promptly upon discovery or notice.
using Mafi;
using Mafi.Unity.UiToolkit;
using Mafi.Unity.UiToolkit.Component;
using Mafi.Unity.UiToolkit.Library;
using UnityEngine;
using UiImage = UnityEngine.UIElements.Image;

namespace AutoForestryDesignations
{
    internal static class TreeIcon
    {
        private static Texture2D? s_matureTreeTexture;

        internal static UiComponent BuildMature(int sizePx)
        {
            return new RuntimeTextureIcon(GetMatureTreeTexture(), sizePx);
        }

        private static Texture2D GetMatureTreeTexture()
        {
            if (s_matureTreeTexture != null)
                return s_matureTreeTexture;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            texture.name = "AFD_MatureTreeIcon";
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            var trunk = HexColor(0x8f5a2c);
            var trunkDark = HexColor(0x6e4122);
            var leaf = HexColor(0x4fa65d);
            var leafDark = HexColor(0x2f7b44);
            var leafOutline = HexColor(0x1f5a31);
            var leafHighlight = HexColor(0x86c77c);

            FillEllipse(pixels, size, 20, 14, 24, 26, leafOutline);
            FillEllipse(pixels, size, 6, 25, 24, 24, leafOutline);
            FillEllipse(pixels, size, 30, 24, 26, 25, leafOutline);
            FillEllipse(pixels, size, 17, 6, 30, 28, leafOutline);

            FillEllipse(pixels, size, 22, 16, 21, 23, leaf);
            FillEllipse(pixels, size, 9, 27, 21, 21, leaf);
            FillEllipse(pixels, size, 31, 26, 22, 21, leafDark);
            FillEllipse(pixels, size, 19, 9, 26, 25, leaf);

            FillRect(pixels, size, 27, 36, 10, 21, trunk);
            FillRect(pixels, size, 32, 36, 6, 21, trunkDark);
            FillCircle(pixels, size, 21, 24, 3, leafHighlight);
            FillCircle(pixels, size, 16, 35, 3, leafHighlight);

            texture.SetPixels(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            s_matureTreeTexture = texture;
            return texture;
        }

        private static Color HexColor(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xff) / 255f,
                ((rgb >> 8) & 0xff) / 255f,
                (rgb & 0xff) / 255f,
                1f);
        }

        private static void FillRect(Color[] pixels, int size, int x, int y, int width, int height, Color color)
        {
            for (int yy = y; yy < y + height; yy++)
                for (int xx = x; xx < x + width; xx++)
                    SetPixel(pixels, size, xx, yy, color);
        }

        private static void FillCircle(Color[] pixels, int size, int cx, int cy, int radius, Color color)
        {
            FillEllipse(pixels, size, cx - radius, cy - radius, radius * 2 + 1, radius * 2 + 1, color);
        }

        private static void FillEllipse(Color[] pixels, int size, int x, int y, int width, int height, Color color)
        {
            float rx = width / 2f;
            float ry = height / 2f;
            float cx = x + rx;
            float cy = y + ry;
            for (int yy = y; yy < y + height; yy++)
            {
                for (int xx = x; xx < x + width; xx++)
                {
                    float nx = (xx + 0.5f - cx) / rx;
                    float ny = (yy + 0.5f - cy) / ry;
                    if (nx * nx + ny * ny <= 1f)
                        SetPixel(pixels, size, xx, yy, color);
                }
            }
        }

        private static void SetPixel(Color[] pixels, int size, int x, int y, Color color)
        {
            if (x < 0 || x >= size || y < 0 || y >= size)
                return;
            pixels[(size - 1 - y) * size + x] = color;
        }

        private sealed class RuntimeTextureIcon : UiComponent<UiImage>
        {
            public RuntimeTextureIcon(Texture2D texture, int sizePx)
                : base(new UiImage())
            {
                Element.image = texture;
                this.Size(sizePx.px());
            }
        }
    }
}
