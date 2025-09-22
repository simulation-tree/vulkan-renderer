using Shaders;
using Vortice.Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledPushConstant
    {
        public readonly int componentType;
        public readonly int componentSize;
        public readonly ShaderType shaderType;
        public readonly VkShaderStageFlags stageFlags;

        public CompiledPushConstant(int componentType, int componentSize, ShaderType shaderType, VkShaderStageFlags stageFlags)
        {
            this.componentType = componentType;
            this.componentSize = componentSize;
            this.shaderType = shaderType;
            this.stageFlags = stageFlags;
        }
    }
}