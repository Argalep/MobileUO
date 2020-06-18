#region license
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

using System.Collections.Generic;

using ClassicUO.IO.Resources;
using ClassicUO.Utility.Collections;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    internal class UOTexture16 : UOTexture
    {
        private ushort[] _data;

        public UOTexture16(int width, int height) : base(width, height, SurfaceFormat.Bgra5551)
        {

        }

        public void PushData(ushort[] data, bool keepData = false)
        {
            if (keepData)
            {
                _data = data;
            }

            SetData(data);
        }

        public ushort[] Data => _data;

        public override bool Contains(int x, int y, bool pixelCheck = true)
        {
            if (UnityTexture != null && x >= 0 && y >= 0 && x < Width && y < Height)
            {
                if (!pixelCheck)
                    return true;

                int pos = y * Width + x;
                
                return GetDataAtPos(pos) != 0;
            }

            return false;
        }
        
        //Used for Contains checks in texture using Unity's own texture data, instead of keeping a copy of the data in _data field
        private uint GetDataAtPos(int pos)
        {
            //The index calculation here is the same as in Texture2D.SetData
            var width = UnityTexture.width;
            int x = pos % width;
            int y = pos / width;
            y *= width;
            var index = y + (width - x - 1);
            
            var data = (UnityTexture as UnityEngine.Texture2D).GetRawTextureData<uint>();
            //We reverse the index because we had already reversed it in Texture2D.SetData
            var reversedIndex = data.Length - index - 1;
            if (reversedIndex < data.Length && reversedIndex >= 0)
            {
                return data[reversedIndex];
            }

            return 0;
        }
    }

    internal class UOTexture32 : UOTexture
    {
        private uint[] _data;

        public UOTexture32(int width, int height) : base(width, height, SurfaceFormat.Color)
        {

        }

        public void PushData(uint[] data)
        {
            _data = data;
            SetData(data);
        }

        public override bool Contains(int x, int y, bool pixelCheck = true)
        {
            if (_data != null && x >= 0 && y >= 0 && x < Width && y < Height)
            {
                if (!pixelCheck)
                    return true;

                int pos = y * Width + x;

                if (pos < _data.Length)
                    return _data[pos] != 0;
            }

            return false;
        }
    }

    internal abstract class UOTexture : Texture2D
    {
        protected UOTexture(int width, int height, SurfaceFormat format) : base(Client.Game.GraphicsDevice, width, height, false, format)
        {
            Ticks = Time.Ticks + 3000;
        }
        public long Ticks { get; set; }

        public abstract bool Contains(int x, int y, bool pixelCheck = true);
    }

    internal class FontTexture : UOTexture32
    {
        public FontTexture(int width, int height, int linescount, RawList<WebLinkRect> links) : base(width, height)
        {
            LinesCount = linescount;
            Links = links;
        }

        public int LinesCount { get; set; }

        public RawList<WebLinkRect> Links { get; }
    }

    internal class AnimationFrameTexture : UOTexture16
    {
        public AnimationFrameTexture(int width, int height) : base(width, height)
        {
        }

        public short CenterX { get; set; }

        public short CenterY { get; set; }
    }

    internal class ArtTexture : UOTexture16
    {
        public ArtTexture(int offsetX, int offsetY, int offsetW, int offsetH, int width, int height) : base(width, height)
        {
            ImageRectangle = new Rectangle(offsetX, offsetY, offsetW, offsetH);
        }

        public ArtTexture(Rectangle rect, int width, int height) : base(width, height)
        {
            ImageRectangle = rect;
        }

        public readonly Rectangle ImageRectangle;
    }
}