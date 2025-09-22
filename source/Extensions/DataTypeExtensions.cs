using System;
using Vortice.Vulkan;
using Worlds;

namespace Rendering.Vulkan
{
    public static class DataTypeExtensions
    {
        public static VkFormat GetFormat(this DataType dataType)
        {
            if (dataType.size == 1)
            {
                return VkFormat.R8Uint;
            }
            else if (dataType.size == 2)
            {
                return VkFormat.R16Sfloat;
            }
            else if (dataType.size == 4)
            {
                return VkFormat.R32Sfloat;
            }
            else if (dataType.size == 8)
            {
                return VkFormat.R32G32Sfloat;
            }
            else if (dataType.size == 12)
            {
                return VkFormat.R32G32B32Sfloat;
            }
            else if (dataType.size == 16)
            {
                return VkFormat.R32G32B32A32Sfloat;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(dataType), $"Unsupported data type size: {dataType.size}");
            }
        }
    }
}