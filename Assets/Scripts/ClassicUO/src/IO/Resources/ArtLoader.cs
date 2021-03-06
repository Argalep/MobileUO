﻿#region license
// Copyright (C) 2020 ClassicUO Development Community on Github
// 
// This project is an alternative client for the game Ultima Online.
// The goal of this is to develop a lightweight client considering
// new technologies.
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Renderer;

using Microsoft.Xna.Framework;

namespace ClassicUO.IO.Resources
{
    internal class ArtLoader : UOFileLoader<ArtTexture>
    {
        private UOFile _file;
        private ushort _graphicMask;
        private readonly UOTexture32[] _land_resources;
        private readonly LinkedList<uint> _used_land_textures_ids = new LinkedList<uint>();

        private ArtLoader(int static_count, int land_count) : base(static_count)
        {
            _graphicMask = Client.IsUOPInstallation ? (ushort) 0xFFFF : (ushort) 0x3FFF;
            _land_resources = new UOTexture32[land_count];
        }

        private static ArtLoader _instance;
        public static ArtLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ArtLoader(Constants.MAX_STATIC_DATA_INDEX_COUNT, Constants.MAX_LAND_DATA_INDEX_COUNT);
                }

                return _instance;
            }
        }


        public override Task Load()
        {
            return Task.Run(() =>
            {
                string filepath = UOFileManager.GetUOFilePath("artLegacyMUL.uop");

                if (Client.IsUOPInstallation && File.Exists(filepath))
                {
                    _file = new UOFileUop(filepath, "build/artlegacymul/{0:D8}.tga");
                    Entries = new UOFileIndex[Constants.MAX_STATIC_DATA_INDEX_COUNT];
                }
                else
                {
                    filepath = UOFileManager.GetUOFilePath("art.mul");
                    string idxpath = UOFileManager.GetUOFilePath("artidx.mul");

                    if (File.Exists(filepath) && File.Exists(idxpath))
                    {
                        _file = new UOFileMul(filepath, idxpath, Constants.MAX_STATIC_DATA_INDEX_COUNT);
                    }
                }

                _file.FillEntries(ref Entries);
            });
        }

        public override ArtTexture GetTexture(uint g, bool keepData = false)
        {
            if (g >= Resources.Length)
                return null;

            ref var texture = ref Resources[g];

            if (texture == null || texture.IsDisposed)
            {
                ReadStaticArt(ref texture, (ushort) g);
                if (texture != null)
                {
                    SaveID(g);
                }
            }
            else
            {
                texture.Ticks = Time.Ticks;
            }

            return texture;
        }

        public UOTexture32 GetLandTexture(uint g)
        {
            if (g >= _land_resources.Length)
                return null;

            ref var texture = ref _land_resources[g];

            if (texture == null || texture.IsDisposed)
            {
                ReadLandArt(ref texture, (ushort) g);

                if (texture != null)
                {
                    _used_land_textures_ids.AddLast(g);
                }
            }
            else
            {
                texture.Ticks = Time.Ticks;
            }

            return texture;
        }

        public override bool TryGetEntryInfo(int entry, out long address, out long size, out long compressedsize)
        {
            entry += 0x4000;

            if (entry < _file.Length && entry >= 0)
            {
                ref UOFileIndex e = ref GetValidRefEntry(entry);

                address = _file.StartAddress.ToInt64() + e.Offset;
                size = e.DecompressedLength == 0 ? e.Length : e.DecompressedLength;
                compressedsize = e.Length;

                return true;
            }

            return base.TryGetEntryInfo(entry, out address, out size, out compressedsize);
        }

        public override void ClearResources()
        {
            base.ClearResources();

            var first = _used_land_textures_ids.First;

            while (first != null)
            {
                var next = first.Next;

                uint idx = first.Value;

                if (idx < _land_resources.Length)
                {
                    ref var texture = ref _land_resources[idx];
                    texture?.Dispose();
                    texture = null;
                }

                _used_land_textures_ids.Remove(first);

                first = next;
            }

            _file?.Dispose();
            _file = null;
            _instance = null;
        }

        public override void CleaUnusedResources(int count)
        {
            base.CleaUnusedResources(count);
            ClearUnusedResources(_land_resources, count);
        }

        public unsafe uint[] ReadStaticArt(ushort graphic, out short width, out short height, out Rectangle imageRectangle)
        {
            imageRectangle.X = 0;
            imageRectangle.Y = 0;
            imageRectangle.Width = 0;
            imageRectangle.Height = 0;

            ref var entry = ref GetValidRefEntry(graphic + 0x4000);

            if (entry.Length == 0)
            {
                width = height = 0;

                return null;
            }

            _file.Seek(entry.Offset);
            _file.Skip(4);
            width = _file.ReadShort();
            height = _file.ReadShort();

            if (width == 0 || height == 0)
                return null;

            uint[] pixels = new uint[width * height];
            ushort* ptr = (ushort*) _file.PositionAddress;
            ushort* lineoffsets = ptr;
            byte* datastart = (byte*) ptr + height * 2;
            int x = 0;
            int y = 0;
            ptr = (ushort*) (datastart + lineoffsets[0] * 2);
            int minX = width, minY = height, maxX = 0, maxY = 0;

            while (y < height)
            {
                ushort xoffs = *ptr++;
                ushort run = *ptr++;

                if (xoffs + run >= 2048)
                {
                    return null;
                }

                if (xoffs + run != 0)
                {
                    x += xoffs;
                    int pos = y * width + x;

                    for (int j = 0; j < run; j++)
                    {
                        var val = *ptr++;

                        pixels[pos++] = val == 0 && run == 1 ? 0x01 : (Utility.HuesHelper.Color16To32(val) | 0xFF_00_00_00);
                    }

                    x += run;
                }
                else
                {
                    x = 0;
                    y++;
                    ptr = (ushort*) (datastart + lineoffsets[y] * 2);
                }
            }

            if (graphic >= 0x2053 && graphic <= 0x2062 || graphic >= 0x206A && graphic <= 0x2079)
            {
                for (int i = 0; i < width; i++)
                {
                    pixels[i] = 0;
                    pixels[(height - 1) * width + i] = 0;
                }

                for (int i = 0; i < height; i++)
                {
                    pixels[i * width] = 0;
                    pixels[i * width + width - 1] = 0;
                }
            }
            else if (StaticFilters.IsCave(graphic) && ProfileManager.Current != null && ProfileManager.Current.EnableCaveBorder)
            {
                for (int yy = 0; yy < height; yy++)
                {
                    int startY = yy != 0 ? -1 : 0;
                    int endY = yy + 1 < height ? 2 : 1;

                    for (int xx = 0; xx < width; xx++)
                    {
                        ref var pixel = ref pixels[yy * width + xx];

                        if (pixel == 0)
                            continue;

                        int startX = xx != 0 ? -1 : 0;
                        int endX = xx + 1 < width ? 2 : 1;

                        for (int i = startY; i < endY; i++)
                        {
                            int currentY = yy + i;

                            for (int j = startX; j < endX; j++)
                            {
                                int currentX = xx + j;

                                ref var currentPixel = ref pixels[currentY * width + currentX];

                                if (currentPixel == 0u) 
                                    pixel = 0xFF_00_00_00;
                            }
                        }
                    }
                }
            }

            int pos1 = 0;

            for (y = 0; y < height; y++)
            {
                for (x = 0; x < width; x++)
                {
                    if (pixels[pos1++] != 0)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            imageRectangle.X = minX;
            imageRectangle.Y = minY;
            imageRectangle.Width = maxX - minX;
            imageRectangle.Height = maxY - minY;

            return pixels;
        }


        private unsafe void ReadStaticArt(ref ArtTexture texture, ushort graphic)
        {
            Rectangle imageRectangle = new Rectangle();

            ref var entry = ref GetValidRefEntry(graphic + 0x4000);

            if (entry.Length == 0)
            {
                return;
            }

            _file.Seek(entry.Offset);
            _file.Skip(4);
            short width = _file.ReadShort();
            short height = _file.ReadShort();

            if (width == 0 || height == 0)
            {
                return;
            }

            uint[] pixels = new uint[width * height];
            ushort* ptr = (ushort*) _file.PositionAddress;
            ushort* lineoffsets = ptr;
            byte* datastart = (byte*) ptr + height * 2;
            int x = 0;
            int y = 0;
            ptr = (ushort*) (datastart + lineoffsets[0] * 2);
            int minX = width, minY = height, maxX = 0, maxY = 0;

            while (y < height)
            {
                ushort xoffs = *ptr++;
                ushort run = *ptr++;

                if (xoffs + run >= 2048)
                {
                    texture = new ArtTexture(imageRectangle, 0, 0);
                    return;
                }

                if (xoffs + run != 0)
                {
                    x += xoffs;
                    int pos = y * width + x;

                    for (int j = 0; j < run; j++, pos++)
                    {
                        ushort val = *ptr++;

                        if (val != 0)
                        {
                            pixels[pos] = Utility.HuesHelper.Color16To32(val) | 0xFF_00_00_00;
                        }          
                    }

                    x += run;
                }
                else
                {
                    x = 0;
                    y++;
                    ptr = (ushort*) (datastart + lineoffsets[y] * 2);
                }
            }

            if (graphic >= 0x2053 && graphic <= 0x2062 || graphic >= 0x206A && graphic <= 0x2079)
            {
                for (int i = 0; i < width; i++)
                {
                    pixels[i] = 0;
                    pixels[(height - 1) * width + i] = 0;
                }

                for (int i = 0; i < height; i++)
                {
                    pixels[i * width] = 0;
                    pixels[i * width + width - 1] = 0;
                }
            }
            else if (StaticFilters.IsCave(graphic) && ProfileManager.Current != null && ProfileManager.Current.EnableCaveBorder)
            {
                for (int yy = 0; yy < height; yy++)
                {
                    int startY = yy != 0 ? -1 : 0;
                    int endY = yy + 1 < height ? 2 : 1;

                    for (int xx = 0; xx < width; xx++)
                    {
                        ref var pixel = ref pixels[yy * width + xx];

                        if (pixel == 0)
                            continue;

                        int startX = xx != 0 ? -1 : 0;
                        int endX = xx + 1 < width ? 2 : 1;

                        for (int i = startY; i < endY; i++)
                        {
                            int currentY = yy + i;

                            for (int j = startX; j < endX; j++)
                            {
                                int currentX = xx + j;

                                ref var currentPixel = ref pixels[currentY * width + currentX];

                                if (currentPixel == 0u) 
                                    pixel = 0xFF_00_00_00;;
                            }
                        }
                    }
                }
            }

            int pos1 = 0;

            for (y = 0; y < height; y++)
            {
                for (x = 0; x < width; x++)
                {
                    if (pixels[pos1++] != 0)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            imageRectangle.X = minX;
            imageRectangle.Y = minY;
            imageRectangle.Width = maxX - minX;
            imageRectangle.Height = maxY - minY;

            entry.Width = (short) ((width >> 1) - 22);
            entry.Height = (short) (height - 44);

            texture = new ArtTexture(imageRectangle, width, height);
            texture.PushData(pixels);
        }
        
        private void ReadLandArt(ref UOTexture32 texture, ushort graphic)
        {
            const int SIZE = 44 * 44;

            graphic &= _graphicMask;
            ref var entry = ref GetValidRefEntry(graphic);

            if (entry.Length == 0)
            {
                texture = new UOTexture32(44,44);
                return;
            }

            _file.Seek(entry.Offset);

            uint[] data = new uint[SIZE];

            for (int i = 0; i < 22; i++)
            {
                int start = 22 - (i + 1);
                int pos = i * 44 + start;
                int end = start + ((i + 1) << 1);

                for (int j = start; j < end; j++)
                {
                    data[pos++] = Utility.HuesHelper.Color16To32(_file.ReadUShort()) | 0xFF_00_00_00;
                }
            }

            for (int i = 0; i < 22; i++)
            {
                int pos = (i + 22) * 44 + i;
                int end = i + ((22 - i) << 1);

                for (int j = i; j < end; j++)
                {
                    data[pos++] = Utility.HuesHelper.Color16To32(_file.ReadUShort()) | 0xFF_00_00_00;
                }
            }

            texture = new UOTexture32(44, 44);
            // we don't need to store the data[] pointer because
            // land is always hoverable
            texture.SetData(data);
        }
    }
}