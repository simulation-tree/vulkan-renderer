using Shaders;
using System;
using System.Numerics;
using Vortice.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// Describes a vertex input attribute inside a collection, where its
    /// index is the location.
    /// </summary>
    public readonly struct VertexInputAttribute
    {
        public readonly byte location;
        public readonly byte binding;
        public readonly byte size;
        public readonly VkFormat format;

        public VertexInputAttribute(byte location, byte binding, byte size, VkFormat format)
        {
            this.location = location;
            this.binding = binding;
            this.format = format;
            this.size = size;
        }

        public VertexInputAttribute(byte location, byte binding, Type type, byte size)
        {
            this.location = location;
            this.binding = binding;
            this.format = GetFormat(type);
            this.size = size;
        }

        public VertexInputAttribute(ShaderVertexInputAttribute shaderVertexAttribute)
        {
            location = shaderVertexAttribute.location;
            binding = shaderVertexAttribute.binding;
            format = GetFormat(shaderVertexAttribute.Type);
            size = shaderVertexAttribute.size;
        }

        public static VkFormat GetFormat(Type type)
        {
            if (type == typeof(Vector2))
            {
                return VkFormat.R32G32Sfloat;
            }
            else if (type == typeof(Vector3))
            {
                return VkFormat.R32G32B32Sfloat;
            }
            else if (type == typeof(Vector4))
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
