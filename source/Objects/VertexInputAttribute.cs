using Shaders;
using System.Numerics;
using Unmanaged;
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

        public VertexInputAttribute(byte location, byte binding, RuntimeType type)
        {
            this.location = location;
            this.binding = binding;
            this.format = GetFormat(type);
            this.size = (byte)type.Size;
        }

        public VertexInputAttribute(ShaderVertexInputAttribute shaderVertexAttribute)
        {
            location = shaderVertexAttribute.location;
            binding = shaderVertexAttribute.binding;
            format = GetFormat(shaderVertexAttribute.type);
            size = (byte)shaderVertexAttribute.type.Size;
        }

        public static VkFormat GetFormat(RuntimeType type)
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
                throw new System.NotSupportedException($"Unsupported type {type}");
            }
        }
    }
}
