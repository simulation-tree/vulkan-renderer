using System;
using System.Numerics;
using Types;
using Vortice.Vulkan;

namespace Rendering.Vulkan
{
    public static class TypeLayoutExtensions
    {
        public static VkFormat GetFormat(this Types.Type type)
        {
            if (type.Is<Vector2>())
            {
                return VkFormat.R32G32Sfloat;
            }
            else if (type.Is<Vector3>())
            {
                return VkFormat.R32G32B32Sfloat;
            }
            else if (type.Is<Vector4>())
            {
                return VkFormat.R32G32B32A32Sfloat;
            }
            else
            {
                throw new NotSupportedException($"Unsupported type {type}");
            }
        }
    }
}