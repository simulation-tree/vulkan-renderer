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

        public VertexInputAttribute(byte location, byte binding, TypeLayout type, byte size)
        {
            this.location = location;
            this.binding = binding;
            this.format = type.GetFormat();
            this.size = size;
        }

        public VertexInputAttribute(ShaderVertexInputAttribute shaderVertexAttribute)
        {
            location = shaderVertexAttribute.location;
            binding = shaderVertexAttribute.binding;
            format = shaderVertexAttribute.Type.GetFormat();
            size = shaderVertexAttribute.size;
        }
    }
}