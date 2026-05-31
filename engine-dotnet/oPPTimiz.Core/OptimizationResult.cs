//oPPTimiz is a tool that reduces Office documents size.
//Copyright (C) 2025 EDF
//This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//You should have received a copy of the GNU General Public License along with this program. If not, see https://www.gnu.org/licenses/.

using System;

namespace oPPTimiz.Core
{
    public sealed class OptimizationResult
    {
        public OptimizationResult(long initialSize, long newSize, int imagesOptimized, int mediaRemoved)
        {
            InitialSize = initialSize;
            NewSize = newSize;
            ImagesOptimized = imagesOptimized;
            MediaRemoved = mediaRemoved;
        }

        public long InitialSize { get; }
        public long NewSize { get; }
        public int ImagesOptimized { get; }
        public int MediaRemoved { get; }

        public long Gain { get { return InitialSize - NewSize; } }

        public double Percentage
        {
            get { return InitialSize == 0 ? 0 : Math.Round((double)Gain / InitialSize * 100, 2); }
        }
    }
}
