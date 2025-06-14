using Vortice.Vulkan;

namespace Vulkan
{
    public struct DescriptorPoolSize
    {
        public VkDescriptorType type;
        public uint count;

        public DescriptorPoolSize(VkDescriptorType type, uint count)
        {
            this.type = type;
            this.count = count;
        }
    }
}