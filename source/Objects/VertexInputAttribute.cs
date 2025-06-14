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
        public readonly uint location;
        public readonly uint binding;
        public readonly uint byteLength;
        public readonly VkFormat format;

        public VertexInputAttribute(uint location, uint binding, uint byteLength, VkFormat format)
        {
            this.location = location;
            this.binding = binding;
            this.format = format;
            this.byteLength = byteLength;
        }

        public VertexInputAttribute(uint location, uint binding, TypeMetadata type)
        {
            this.location = location;
            this.binding = binding;
            this.format = type.GetFormat();
            this.byteLength = (byte)type.Size;
        }

        public VertexInputAttribute(ShaderVertexInputAttribute shaderVertexAttribute)
        {
            location = shaderVertexAttribute.location;
            binding = shaderVertexAttribute.binding;
            format = shaderVertexAttribute.type.GetFormat();
            byteLength = (byte)shaderVertexAttribute.type.Size;
        }
    }
}