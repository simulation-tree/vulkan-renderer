using Rendering.Vulkan;
using Shaders;
using Types;
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

        public VertexInputAttribute(byte location, byte binding, TypeMetadata type)
        {
            this.location = location;
            this.binding = binding;
            this.format = type.GetFormat();
            this.size = (byte)type.Size;
        }

        public VertexInputAttribute(ShaderVertexInputAttribute shaderVertexAttribute)
        {
            location = shaderVertexAttribute.location;
            binding = shaderVertexAttribute.binding;
            format = shaderVertexAttribute.type.GetFormat();
            size = (byte)shaderVertexAttribute.type.Size;
        }
    }
}