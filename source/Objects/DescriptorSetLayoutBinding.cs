using Vortice.Vulkan;

namespace Vulkan
{
    public unsafe struct DescriptorSetLayoutBinding
    {
        public uint binding;
        public VkDescriptorType descriptorType;
        public uint descriptorCount;
        public VkShaderStageFlags shaderFlags;
        internal VkSampler* immutableSamplers;

        public DescriptorSetLayoutBinding(uint binding, VkDescriptorType descriptorType, uint descriptorCount, VkShaderStageFlags shaderFlags)
        {
            this.binding = binding;
            this.descriptorType = descriptorType;
            this.descriptorCount = descriptorCount;
            this.shaderFlags = shaderFlags;
        }
    }
}