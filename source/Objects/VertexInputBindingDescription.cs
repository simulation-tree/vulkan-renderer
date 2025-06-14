using Vortice.Vulkan;

namespace Vulkan
{
    public readonly struct VertexInputBindingDescription
    {
        public readonly uint binding;
        public readonly uint stride;
        public readonly VkVertexInputRate inputRate;

        public VertexInputBindingDescription(uint binding, uint stride, VkVertexInputRate inputRate)
        {
            this.binding = binding;
            this.stride = stride;
            this.inputRate = inputRate;
        }
    }
}